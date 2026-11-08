// Core11 fixture: .NET 11 RC3 (SDK 11.0.100-rc.3.26522.148), C# 15 unions + closed hierarchies + JSONL.
// Every observation is one JSON line: {"id":...,"outcome":...,"actual":...}.
// Check(...) throws AssertionFailed on mismatch; Capture(...) never swallows AssertionFailed.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

namespace JsonContracts;

public sealed record OrderCreated(string OrderId);
public sealed record FailureNotice(string FailureCode);

public union ApiValue(int, string, OrderCreated);
public union PlainOutcome(OrderCreated, FailureNotice);

[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
public union ClassifiedOutcome(OrderCreated, FailureNotice);

public closed record ApiEvent;
public sealed record OrderEvent(string OrderId) : ApiEvent;
public sealed record FailureEvent(string FailureCode) : ApiEvent;

// Open (non-closed) hierarchy asking for inference: reflection only, never in a generated context here.
[JsonPolymorphic(InferClosedTypePolymorphism = true)]
public record OpenEvent;
public sealed record OpenChild(int N) : OpenEvent;

public sealed record EventStreamItem(int Seq, string Kind);

[JsonSourceGenerationOptions(InferClosedTypePolymorphism = true)]
[JsonSerializable(typeof(ApiEvent))]
internal partial class InferredEventContext : JsonSerializerContext { }

[JsonSerializable(typeof(ApiEvent))]
internal partial class UninferredEventContext : JsonSerializerContext { }

[JsonSerializable(typeof(ApiValue))]
[JsonSerializable(typeof(ClassifiedOutcome))]
internal partial class UnionContext : JsonSerializerContext { }

public static class Core11
{
    sealed class AssertionFailed(string message) : Exception(message);

    static readonly JsonSerializerOptions Reflection = new() { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
    static readonly JsonSerializerOptions InferReflection = new() { InferClosedTypePolymorphism = true, TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

    public static async Task Main()
    {
        Emit("runtime.framework", "info", RuntimeInformation.FrameworkDescription);
        Emit("runtime.version", "info", Environment.Version.ToString());
        Emit("stj.informational-version", "info",
            typeof(JsonSerializer).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "missing");
        Unions();
        ClosedHierarchies();
        await Streams();
        Emit("core11.complete", "info", "all assertions passed");
    }

    static void Unions()
    {
        var cases = new (string Id, ApiValue Value, string Json, Type Type)[]
        {
            ("union.int", (ApiValue)42, "42", typeof(int)),
            ("union.string", (ApiValue)"text", "\"text\"", typeof(string)),
            ("union.object", (ApiValue)new OrderCreated("o-1"), "{\"OrderId\":\"o-1\"}", typeof(OrderCreated)),
        };
        foreach (var c in cases)
        {
            var s = Capture(c.Id + ".serialize", () => JsonSerializer.Serialize(c.Value, Reflection));
            Check(c.Id + ".serialize.expected", s.Ok && s.Actual == c.Json, s.Actual);
            var d = Capture(c.Id + ".deserialize", () => JsonSerializer.Deserialize<ApiValue>(c.Json, Reflection) is { } u ? Describe(u.Value) : "null-union");
            Check(c.Id + ".deserialize.active-type", d.Ok && d.Actual.StartsWith(c.Type.FullName + ":"), d.Actual);
        }

        var def = Capture("union.default.serialize", () => JsonSerializer.Serialize(default(ApiValue), Reflection));
        Check("union.default.serialize.expected-null", def.Ok && def.Actual == "null", def.Actual);
        var nonnullableNull = Capture("union.json-null.nonnullable", () => JsonSerializer.Deserialize<ApiValue>("null", Reflection) is { } u ? Describe(u.Value) : "null-union");
        Check("union.json-null.nonnullable.default", nonnullableNull.Ok && nonnullableNull.Actual == "null", nonnullableNull.Actual);
        var nullableNull = Capture("union.json-null.nullable", () => JsonSerializer.Deserialize<ApiValue?>("null", Reflection) is { } u ? Describe(u.Value) : "null-union");
        Check("union.json-null.nullable.absent", nullableNull.Ok && nullableNull.Actual == "null-union", nullableNull.Actual);
        var web = Capture("union.web.string-ambiguity", () => Describe(JsonSerializer.Deserialize<ApiValue>("\"text\"", JsonSerializerOptions.Web).Value));
        Check("union.web.string-ambiguity.json-exception", !web.Ok && web.Actual.StartsWith("System.Text.Json.JsonException:"), web.Actual);

        // Two object cases without a classifier: writing is by runtime value, reading cannot choose.
        var ps1 = Capture("union.unclassified.serialize.order", () => JsonSerializer.Serialize((PlainOutcome)new OrderCreated("o-1"), Reflection));
        Check("union.unclassified.serialize.order.expected", ps1.Ok && ps1.Actual == "{\"OrderId\":\"o-1\"}", ps1.Actual);
        var ps2 = Capture("union.unclassified.serialize.failure", () => JsonSerializer.Serialize((PlainOutcome)new FailureNotice("F-9"), Reflection));
        Check("union.unclassified.serialize.failure.expected", ps2.Ok && ps2.Actual == "{\"FailureCode\":\"F-9\"}", ps2.Actual);
        var pd = Capture("union.unclassified.deserialize", () => JsonSerializer.Deserialize<PlainOutcome>("{\"OrderId\":\"o-1\"}", Reflection) is { } u ? Describe(u.Value) : "null-union");
        Check("union.unclassified.deserialize.fails", !pd.Ok, pd.Actual);

        // Structural classifier: root property names select the case.
        ClassifiedExpect("union.classified.order", "{\"OrderId\":\"o-1\"}", typeof(OrderCreated), JsonDeserializeClassifiedReflection);
        ClassifiedExpect("union.classified.failure", "{\"FailureCode\":\"F-9\"}", typeof(FailureNotice), JsonDeserializeClassifiedReflection);
        // Value text equal to the other case's property name must not change classification (names, not values).
        ClassifiedExpect("union.classified.names-not-values", "{\"OrderId\":\"FailureCode\"}", typeof(OrderCreated), JsonDeserializeClassifiedReflection);
        var unknown = Capture("union.classified.unknown", () => JsonDeserializeClassifiedReflection("{\"Other\":1}"));
        Check("union.classified.unknown.fails", !unknown.Ok, unknown.Actual);
        var ambiguous = Capture("union.classified.ambiguous", () => JsonDeserializeClassifiedReflection("{\"OrderId\":\"o-1\",\"FailureCode\":\"F-9\"}"));
        Check("union.classified.ambiguous.fails", !ambiguous.Ok, ambiguous.Actual);

        // Source-generated unions.
        var gs = Capture("union.sourcegen.int.serialize", () => JsonSerializer.Serialize((ApiValue)42, UnionContext.Default.ApiValue));
        Check("union.sourcegen.int.serialize.expected", gs.Ok && gs.Actual == "42", gs.Actual);
        ClassifiedExpect("union.sourcegen.classified.failure", "{\"FailureCode\":\"F-9\"}", typeof(FailureNotice),
            json => JsonSerializer.Deserialize(json, UnionContext.Default.ClassifiedOutcome) is { } u ? Describe(u.Value) : "null-union");
    }

    static string JsonDeserializeClassifiedReflection(string json) =>
        JsonSerializer.Deserialize<ClassifiedOutcome>(json, Reflection) is { } u ? Describe(u.Value) : "null-union";

    static void ClassifiedExpect(string id, string json, Type expected, Func<string, string> read)
    {
        var r = Capture(id, () => read(json));
        Check(id + ".active-type", r.Ok && r.Actual.StartsWith(expected.FullName + ":"), r.Actual);
    }

    static void ClosedHierarchies()
    {
        ClosedRoundTrip("closed.reflection", () => (JsonTypeInfo<ApiEvent>)InferReflection.GetTypeInfo(typeof(ApiEvent)));
        ClosedRoundTrip("closed.sourcegen", () => InferredEventContext.Default.ApiEvent);

        var open = Capture("open.reflection.serialize", () => JsonSerializer.Serialize<OpenEvent>(new OpenChild(1), Reflection));
        Check("open.reflection.invalid-operation", !open.Ok && open.Actual.StartsWith("System.InvalidOperationException"), open.Actual);

        // Observation only: does the runtime option affect a context generated without inference?
        Capture("closed.uninferred-context.baseline", () => JsonSerializer.Serialize<ApiEvent>(new OrderEvent("o-1"), UninferredEventContext.Default.ApiEvent));
        Capture("closed.uninferred-context.runtime-option", () => JsonSerializer.Serialize<ApiEvent>(new OrderEvent("o-1"),
            new JsonSerializerOptions { InferClosedTypePolymorphism = true, TypeInfoResolver = UninferredEventContext.Default }));
    }

    static void ClosedRoundTrip(string id, Func<JsonTypeInfo<ApiEvent>> info)
    {
        var derived = Capture(id + ".derived-types", () => string.Join(",",
            info().PolymorphismOptions?.DerivedTypes.Select(d => $"{d.DerivedType.Name}={d.TypeDiscriminator ?? "none"}") ?? new[] { "no-polymorphism" }));
        Check(id + ".derived-types.expected", derived.Ok && derived.Actual.Contains("OrderEvent") && derived.Actual.Contains("FailureEvent"), derived.Actual);
        Capture(id + ".discriminator-property", () => info().PolymorphismOptions?.TypeDiscriminatorPropertyName ?? "no-polymorphism");
        foreach (ApiEvent e in new ApiEvent[] { new OrderEvent("o-1"), new FailureEvent("F-9") })
        {
            string name = e.GetType().Name, payload = e is OrderEvent ? "\"OrderId\":\"o-1\"" : "\"FailureCode\":\"F-9\"";
            var s = Capture($"{id}.{name}.serialize", () => JsonSerializer.Serialize(e, info()));
            Check($"{id}.{name}.serialize.derived-payload", s.Ok && s.Actual.Contains(payload), s.Actual);
            var d = Capture($"{id}.{name}.roundtrip", () => JsonSerializer.Deserialize(s.Actual, info()) is { } back ? $"{back.GetType().Name}:{back}" : "null");
            Check($"{id}.{name}.roundtrip.derived-type", d.Ok && d.Actual == $"{name}:{e}", d.Actual);
        }
    }

    static async IAsyncEnumerable<EventStreamItem> Items()
    {
        yield return new EventStreamItem(1, "order");
        await Task.Yield();
        yield return new EventStreamItem(2, "failure");
    }

    static async Task Streams()
    {
        const string array = "[{\"Seq\":1,\"Kind\":\"order\"},{\"Seq\":2,\"Kind\":\"failure\"}]";
        const string lines = "{\"Seq\":1,\"Kind\":\"order\"}\n{\"Seq\":2,\"Kind\":\"failure\"}\n";

        var baseline = await CaptureStream("stream.serializeasync.array", s => JsonSerializer.SerializeAsync(s, Items(), Reflection));
        Check("stream.serializeasync.array.expected", baseline.Ok && baseline.Actual == array, baseline.Actual);

        var arr = await CaptureStream("stream.enumerable.array", s => JsonSerializer.SerializeAsyncEnumerable(s, Items(), options: Reflection));
        Check("stream.enumerable.array.expected", arr.Ok && arr.Actual == array, arr.Actual);

        var jsonl = new JsonSerializerOptions { WriteIndented = true, NewLine = "\r\n", TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var tl = await CaptureStream("stream.enumerable.top-level", s => JsonSerializer.SerializeAsyncEnumerable(s, Items(), topLevelValues: true, options: jsonl));
        Check("stream.enumerable.top-level.exact-compact-lf", tl.Ok && tl.Actual == lines, tl.Actual);
        Check("stream.enumerable.top-level.no-cr", tl.Ok && !tl.Actual.Contains('\r'), tl.Actual);
        Check("stream.enumerable.top-level.final-lf", tl.Ok && tl.Actual.EndsWith("\n"), tl.Actual);
    }

    static async Task<(bool Ok, string Actual)> CaptureStream(string id, Func<Stream, Task> write)
    {
        try
        {
            using var ms = new MemoryStream();
            await write(ms);
            byte[] bytes = ms.ToArray();
            string text = Encoding.UTF8.GetString(bytes);
            Emit(id, "ok", text);
            Emit(id + ".hex", "info", Convert.ToHexString(bytes));
            return (true, text);
        }
        catch (AssertionFailed) { throw; }
        catch (Exception ex) { return Failed(id, ex); }
    }

    static (bool Ok, string Actual) Capture(string id, Func<string> action)
    {
        try { string v = action(); Emit(id, "ok", v); return (true, v); }
        catch (AssertionFailed) { throw; }
        catch (Exception ex) { return Failed(id, ex); }
    }

    static (bool, string) Failed(string id, Exception ex)
    {
        string v = $"{ex.GetType().FullName}: {ex.Message}";
        Emit(id, "exception", v);
        return (false, v);
    }

    static void Check(string id, bool condition, string actual)
    {
        Emit(id, condition ? "assert-pass" : "assert-fail", actual);
        if (!condition) throw new AssertionFailed($"{id}: {actual}");
    }

    static string Describe(object? value) => value is null ? "null" : $"{value.GetType().FullName}:{value}";

    static string E(string s) => JsonEncodedText.Encode(s, JavaScriptEncoder.UnsafeRelaxedJsonEscaping).ToString();

    static void Emit(string id, string outcome, string actual) =>
        Console.WriteLine($"{{\"id\":\"{E(id)}\",\"outcome\":\"{E(outcome)}\",\"actual\":\"{E(actual)}\"}}");
}

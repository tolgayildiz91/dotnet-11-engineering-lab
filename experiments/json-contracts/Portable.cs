// Portable fixture: identical source for net8.0 / net10.0 / net11.0.
// Models and the generated context live in different namespaces/types so private members are truly inaccessible to generated code.
// Generated calls pass PortableContext.Default.<Type> directly; reflection uses its own DefaultJsonTypeInfoResolver options.
// Old runtimes: outcomes are captured only. NET11_0_OR_GREATER: exact success and values are asserted.
using System;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using JsonContracts.PortableGeneration;
using JsonContracts.PortableModels;

namespace JsonContracts.PortableModels
{
    public sealed class IncludePrivateSetter
    {
        [JsonInclude] public string Name { get; private set; } = "unset";
    }

    public sealed class IncludePrivateField
    {
        [JsonInclude] private string secret = "unset";
        public string ReadSecret() => secret;
    }

    public sealed class PrivateConstructorShape
    {
        [JsonConstructor] private PrivateConstructorShape(string id) { Id = id; }
        public string Id { get; }
    }

    public sealed class InitDefaultShape
    {
        public string Mode { get; init; } = "synthetic-default";
        public int Count { get; set; }
    }
}

namespace JsonContracts.PortableGeneration
{
    [JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
    [JsonSerializable(typeof(IncludePrivateSetter))]
    [JsonSerializable(typeof(IncludePrivateField))]
    [JsonSerializable(typeof(PrivateConstructorShape))]
    [JsonSerializable(typeof(InitDefaultShape))]
    internal partial class PortableContext : JsonSerializerContext { }
}

namespace JsonContracts
{
    public static class Portable
    {
        sealed class AssertionFailed : Exception { public AssertionFailed(string m) : base(m) { } }

        static readonly JsonSerializerOptions Reflection = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

        public static async Task<int> Main()
        {
            Emit("runtime.framework", "info", RuntimeInformation.FrameworkDescription);
            Emit("runtime.version", "info", Environment.Version.ToString());
            Emit("target.framework", "info", AppContext.TargetFrameworkName ?? "unknown");
            Emit("stj.informational-version", "info",
                typeof(JsonSerializer).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "missing");

            Shape("include-private-setter", "{\"Name\":\"n-1\"}", "Name=n-1 | out={\"Name\":\"n-1\"}",
                () => PortableContext.Default.IncludePrivateSetter, v => "Name=" + v.Name);
            Shape("include-private-field", "{\"secret\":\"s-1\"}", "secret=s-1 | out={\"secret\":\"s-1\"}",
                () => PortableContext.Default.IncludePrivateField, v => "secret=" + v.ReadSecret());
            Shape("private-json-constructor", "{\"Id\":\"c-1\"}", "Id=c-1 | out={\"Id\":\"c-1\"}",
                () => PortableContext.Default.PrivateConstructorShape, v => "Id=" + v.Id);
            Shape("init-default.omitted", "{\"Count\":3}", "Mode=synthetic-default;Count=3 | out={\"Mode\":\"synthetic-default\",\"Count\":3}",
                () => PortableContext.Default.InitDefaultShape, v => $"Mode={v.Mode ?? "<null>"};Count={v.Count}");
            Shape("init-default.explicit", "{\"Mode\":\"explicit\",\"Count\":1}", "Mode=explicit;Count=1 | out={\"Mode\":\"explicit\",\"Count\":1}",
                () => PortableContext.Default.InitDefaultShape, v => $"Mode={v.Mode ?? "<null>"};Count={v.Count}");

            using var stream = new MemoryStream();
            await JsonSerializer.SerializeAsync(stream, Items(), Reflection);
            var array = Encoding.UTF8.GetString(stream.ToArray());
            Check("serializeasync.array", array == "[1,2]", array);
            Emit("portable.complete", "info", "finished");
            return 0;
        }

        static async IAsyncEnumerable<int> Items()
        {
            yield return 1;
            await Task.Yield();
            yield return 2;
        }

        static void Shape<T>(string id, string json, string expected, Func<JsonTypeInfo<T>> generated, Func<T, string> describe)
        {
            var g = Probe("generated." + id, generated, json, describe);
            var r = Probe("reflection." + id, () => (JsonTypeInfo<T>)Reflection.GetTypeInfo(typeof(T)), json, describe);
#if NET11_0_OR_GREATER
            Check("generated." + id + ".expected", g.Ok && g.Actual == expected, g.Actual);
            Check("reflection." + id + ".expected", r.Ok && r.Actual == expected, r.Actual);
#else
            Emit("generated." + id + ".matches-net11-expectation", "info", (g.Ok && g.Actual == expected).ToString());
            Emit("reflection." + id + ".matches-net11-expectation", "info", (r.Ok && r.Actual == expected).ToString());
#endif
        }

        static (bool Ok, string Actual) Probe<T>(string id, Func<JsonTypeInfo<T>> info, string json, Func<T, string> describe)
        {
            try
            {
                JsonTypeInfo<T> ti = info();
                T value = JsonSerializer.Deserialize(json, ti)!;
                string actual = describe(value) + " | out=" + JsonSerializer.Serialize(value, ti);
                Emit(id, "ok", actual);
                return (true, actual);
            }
            catch (AssertionFailed) { throw; }
            catch (Exception ex)
            {
                string actual = $"{ex.GetType().FullName}: {ex.Message}";
                Emit(id, "exception", actual);
                return (false, actual);
            }
        }

        static void Check(string id, bool condition, string actual)
        {
            Emit(id, condition ? "assert-pass" : "assert-fail", actual);
            if (!condition) throw new AssertionFailed($"{id}: {actual}");
        }

        static string E(string s) => JsonEncodedText.Encode(s, JavaScriptEncoder.UnsafeRelaxedJsonEscaping).ToString();

        static void Emit(string id, string outcome, string actual) =>
            Console.WriteLine($"{{\"id\":\"{E(id)}\",\"outcome\":\"{E(outcome)}\",\"actual\":\"{E(actual)}\"}}");
    }
}

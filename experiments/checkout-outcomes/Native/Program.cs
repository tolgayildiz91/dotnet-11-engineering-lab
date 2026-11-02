using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Checkout.Model;
using Microsoft.AspNetCore.Http.HttpResults;

var selfTest = args.Contains("--self-test");
var builder = WebApplication.CreateBuilder(args.Where(a => a != "--self-test").ToArray());
if (selfTest) builder.Logging.SetMinimumLevel(LogLevel.Warning);
var app = builder.Build();
app.MapPost("/checkout/union", (CheckoutRequest request) => CheckoutFlow.ToHttp(CheckoutFlow.Evaluate(request)));

if (!selfTest)
{
    app.Run();
    return 0;
}

var asm = typeof(CheckoutFlow).Assembly;
string Meta(string key) => asm.GetCustomAttributes<AssemblyMetadataAttribute>().Single(m => m.Key == key).Value ?? "";
Console.WriteLine($"native tfm={AppContext.TargetFrameworkName} runtime={System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription} LangVersion={Meta("LangVersion")} RuntimeFrameworkVersion={Meta("RuntimeFrameworkVersion")}");
Console.WriteLine($"process architecture={System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture} os architecture={System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}");

var failures = 0;
void Check(bool condition, string name)
{
    Console.WriteLine($"check {name}: {(condition ? "ok" : "FAIL")}");
    if (!condition) failures++;
}
string Try(Func<string> f)
{
    try { return f(); }
    catch (Exception e) { return $"{e.GetType().FullName}: {e.Message}"; }
}

// Four outcomes: the union's Value is exactly the case produced by the shared rules.
foreach (var c in HttpChecks.Cases)
{
    var outcome = CheckoutFlow.Evaluate(c.Request);
    Check(outcome.Value?.GetType().Name == c.Name, $"outcome {c.Name} type={outcome.Value?.GetType().Name}");
    var expectedDescription = c.Name switch
    {
        "OrderCreated" => "201 ord-SKU-1-2",
        "ValidationFailed" => "400 quantity",
        "InventoryUnavailable" => "409 SKU-1",
        "PaymentDeclined" => "402 synthetic_decline",
        _ => throw new InvalidOperationException(c.Name),
    };
    Check(Consumer.Describe(outcome) == expectedDescription, $"exact complete fixture Consumer.cs executes {c.Name}: {expectedDescription}");
}
Check(CheckoutFlow.Evaluate(HttpChecks.Cases[0].Request) is OrderCreated created && created == Rules.Create(HttpChecks.Cases[0].Request), "success payload equality");

// Implicit case conversion and the switch-arm selection.
CheckoutOutcome converted = new PaymentDeclined("synthetic_decline", 1m);
Check(converted is PaymentDeclined { Amount: 1m }, "implicit conversion PaymentDeclined");
Check(CheckoutFlow.Describe(converted) == "PaymentDeclined", "switch selects PaymentDeclined arm");

// default and null contents.
var empty = default(CheckoutOutcome);
Check(empty.Value is null, "default(CheckoutOutcome).Value is null");
Check(empty is null, "default union matches null pattern");
Check(empty is { }, "default union matches empty property pattern {} (checks the struct instance)");
Check(CheckoutFlow.Describe(empty) == "empty", "switch with null arm handles default");
Console.WriteLine($"observe switch-without-null-arm on default: {Try(() => CheckoutFlow.DescribeNoNullArm(empty))}");
CheckoutOutcome fromNull = (OrderCreated)null!;
Check(fromNull.Value is null, "union created from null OrderCreated has null Value");

// JSON: serialization writes the active case directly.
var web = JsonSerializerOptions.Web;
foreach (var c in HttpChecks.Cases)
{
    var outcome = CheckoutFlow.Evaluate(c.Request);
    var json = JsonSerializer.Serialize(outcome, web);
    Console.WriteLine($"json serialize {c.Name}: {json}");
    Check(json == c.Body, $"json union body equals HTTP body {c.Name}");
}
Console.WriteLine($"observe json serialize default union: {Try(() => JsonSerializer.Serialize(empty, web))}");

// JSON deserialization: four object-shaped cases without a classifier, then with the structural classifier.
foreach (var c in HttpChecks.Cases)
{
    Console.WriteLine($"observe json deserialize CheckoutOutcome {c.Name} (no classifier): {Try(() => JsonSerializer.Deserialize<CheckoutOutcome>(c.Body, web).Value?.GetType().Name ?? "null")}");
    var classified = Try(() => JsonSerializer.Deserialize<ClassifiedOutcome>(c.Body, web).Value?.GetType().Name ?? "null");
    Check(classified == c.Name, $"json deserialize ClassifiedOutcome {c.Name} -> {classified}");
    var copy = JsonSerializer.Deserialize<ClassifiedOutcome>(c.Body, web);
    Check(Equals(copy.Value, CheckoutFlow.Evaluate(c.Request).Value), $"json deserialized payload equality {c.Name}");
    Check(JsonSerializer.Serialize(copy, web) == c.Body, $"json byte-exact roundtrip {c.Name}");
}
Console.WriteLine($"observe json deserialize ClassifiedOutcome unknown shape: {Try(() => JsonSerializer.Deserialize<ClassifiedOutcome>("""{"other":1}""", web).Value?.GetType().Name ?? "null")}");

// Primitive union: token type distinguishes int and string with default options.
Check(JsonSerializer.Deserialize<Amount>("42").Value is 42, "json deserialize Amount number -> int");
Check(JsonSerializer.Deserialize<Amount>("\"42\"").Value is "42", "json deserialize Amount string -> string (default options)");
Console.WriteLine($"observe json deserialize Amount string with Web options: {Try(() => JsonSerializer.Deserialize<Amount>("\"42\"", web).Value?.GetType().Name ?? "null")}");

// Reflection: what the compiler actually generated.
foreach (var t in new[] { typeof(CheckoutOutcome), typeof(Amount), typeof(IntOrText) })
{
    Console.WriteLine($"reflect {t.Name}: IsValueType={t.IsValueType} attributes=[{string.Join(", ", t.CustomAttributes.Select(a => a.AttributeType.FullName))}] interfaces=[{string.Join(", ", t.GetInterfaces().Select(i => i.FullName))}]");
    foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        Console.WriteLine($"reflect {t.Name} field {f.Name}: {f.FieldType.FullName} {f.Attributes} [{string.Join(", ", f.CustomAttributes.Select(a => a.AttributeType.Name))}]");
    foreach (var p in t.GetProperties())
        Console.WriteLine($"reflect {t.Name} property {p.Name}: {p.PropertyType.FullName}");
    foreach (var k in t.GetConstructors())
        Console.WriteLine($"reflect {t.Name} ctor ({string.Join(", ", k.GetParameters().Select(p => p.ParameterType.FullName))})");
}
Check(typeof(CheckoutOutcome).IsValueType && typeof(IUnion).IsAssignableFrom(typeof(CheckoutOutcome)), "CheckoutOutcome is struct implementing IUnion");

// Value-type case storage: generated union boxes int into its object field; custom union does not.
Amount boxed = 42;
var storage = typeof(Amount).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Single();
var stored = storage.GetValue(boxed);
Check(storage.FieldType == typeof(object) && stored is int and 42, $"generated Amount stores int in object field {storage.Name} (runtime type {stored?.GetType().Name})");
IntOrText custom = 42;
Check(custom switch { int i => i == 42, string => false }, "custom IntOrText matches int");
const int N = 100_000;
long Measure(Action<int> body)
{
    body(1);
    var before = GC.GetAllocatedBytesForCurrentThread();
    for (var i = 0; i < N; i++) body(i);
    return GC.GetAllocatedBytesForCurrentThread() - before;
}
var generatedBytes = Measure(i => { Sink.Amount = i + 1000; });
var customBytes = Measure(i => { Sink.Custom = i + 1000; });
Console.WriteLine($"alloc generated Amount from int: {generatedBytes} bytes / {N} = {(double)generatedBytes / N:F1} per conversion");
Console.WriteLine($"alloc custom IntOrText from int: {customBytes} bytes / {N} = {(double)customBytes / N:F1} per conversion");
Check(generatedBytes >= N * 8L && customBytes < 1024, "int case boxes in generated union, not in custom non-boxing union");

failures += await HttpChecks.RunAsync(app, "/checkout/union");
Console.WriteLine($"native self-test failures={failures}");
return failures == 0 ? 0 : 1;

public union CheckoutOutcome(OrderCreated, ValidationFailed, InventoryUnavailable, PaymentDeclined);

// Same cases; the attribute only selects a JSON classifier for deserializing object-shaped cases.
[JsonUnion(TypeClassifier = typeof(JsonUnionTypeStructuralClassifier))]
public union ClassifiedOutcome(OrderCreated, ValidationFailed, InventoryUnavailable, PaymentDeclined);

public union Amount(int, string);

// Hand-written custom union using the non-boxing access pattern (not compiler-generated).
[Union]
public readonly struct IntOrText : IUnion
{
    private readonly int _number;
    private readonly string? _text;
    private readonly byte _tag; // 0 none, 1 int, 2 string

    public IntOrText(int value) => (_number, _tag) = (value, 1);
    public IntOrText(string? value) => (_text, _tag) = (value, value is null ? (byte)0 : (byte)2);

    public object? Value => _tag switch { 1 => _number, 2 => _text, _ => null };
    public bool HasValue => _tag != 0;
    public bool TryGetValue(out int value) { value = _number; return _tag == 1; }
    public bool TryGetValue(out string value) { value = _text!; return _tag == 2; }
}

static class Sink
{
    public static Amount Amount;
    public static IntOrText Custom;
}

public static class CheckoutFlow
{
    public static CheckoutOutcome Evaluate(CheckoutRequest r)
    {
        if (Rules.Validate(r) is { } invalid) return invalid;
        if (Rules.Reserve(r) is { } unavailable) return unavailable;
        if (Rules.Charge(r) is { } declined) return declined;
        return Rules.Create(r);
    }

    public static string Describe(CheckoutOutcome outcome) => outcome switch
    {
        OrderCreated => "OrderCreated",
        ValidationFailed => "ValidationFailed",
        InventoryUnavailable => "InventoryUnavailable",
        PaymentDeclined => "PaymentDeclined",
        null => "empty",
    };

    public static string DescribeNoNullArm(CheckoutOutcome outcome) => outcome switch
    {
        OrderCreated => "OrderCreated",
        ValidationFailed => "ValidationFailed",
        InventoryUnavailable => "InventoryUnavailable",
        PaymentDeclined => "PaymentDeclined",
    };

    // The HTTP status mapping is this switch, written by us; the union only makes it checkable.
    public static Results<Created<OrderCreated>, BadRequest<ValidationFailed>, Conflict<InventoryUnavailable>, JsonHttpResult<PaymentDeclined>>
        ToHttp(CheckoutOutcome outcome) => outcome switch
        {
            OrderCreated o => TypedResults.Created($"/orders/{o.OrderId}", o),
            ValidationFailed v => TypedResults.BadRequest(v),
            InventoryUnavailable i => TypedResults.Conflict(i),
            PaymentDeclined p => TypedResults.Json(p, statusCode: StatusCodes.Status402PaymentRequired),
            null => throw new InvalidOperationException("Empty (default) CheckoutOutcome"),
        };
}

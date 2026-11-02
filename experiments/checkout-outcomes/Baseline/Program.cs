using System.Diagnostics;
using System.Reflection;
using Checkout.Baseline;
using Checkout.Model;
using Microsoft.AspNetCore.Http.HttpResults;

var selfTest = args.Contains("--self-test");
var builder = WebApplication.CreateBuilder(args.Where(a => a != "--self-test").ToArray());
if (selfTest) builder.Logging.SetMinimumLevel(LogLevel.Warning);
var app = builder.Build();
app.MapPost("/checkout/result", (CheckoutRequest request) => CheckoutFlow.ToHttp(CheckoutFlow.Evaluate(request)));

if (!selfTest)
{
    app.Run();
    return 0;
}

var asm = typeof(CheckoutFlow).Assembly;
string Meta(string key) => asm.GetCustomAttributes<AssemblyMetadataAttribute>().Single(m => m.Key == key).Value ?? "";
Console.WriteLine($"baseline tfm={AppContext.TargetFrameworkName} runtime={System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription} LangVersion={Meta("LangVersion")} RuntimeFrameworkVersion={Meta("RuntimeFrameworkVersion")}");

var failures = 0;
void Check(bool condition, string name)
{
    Console.WriteLine($"check {name}: {(condition ? "ok" : "FAIL")}");
    if (!condition) failures++;
}

// Four outcomes, identical payloads to the shared rules; Match invokes exactly one branch.
foreach (var c in HttpChecks.Cases)
{
    var result = CheckoutFlow.Evaluate(c.Request);
    int okCalls = 0, failCalls = 0;
    var payload = result.Match<object>(ok => { okCalls++; return ok; }, error => { failCalls++; return error; });
    Check(payload.GetType().Name == c.Name, $"outcome {c.Name} type={payload.GetType().Name}");
    Check(okCalls + failCalls == 1 && result.IsSuccess == (okCalls == 1), $"match-one-branch {c.Name}");
}
Check(CheckoutFlow.Evaluate(HttpChecks.Cases[0].Request).Match(o => o, _ => null!) == Rules.Create(HttpChecks.Cases[0].Request), "success payload equality");

// Guarded construction: no null payloads, no empty state (class has no default instance).
Check(Throws<ArgumentNullException>(() => Result<OrderCreated, CheckoutError>.Ok(null!)), "Ok(null) rejected");
Check(Throws<ArgumentNullException>(() => Result<OrderCreated, CheckoutError>.Fail(null!)), "Fail(null) rejected");
Result<OrderCreated, CheckoutError>? defaultResult = default;
Check(defaultResult is null, "default Result class reference is null");

// Bind short-circuits: later stages never run after a failure.
var stagesRun = 0;
var chained = Result<int, CheckoutError>.Fail(new ValidationFailed("x", "y"))
    .Bind(v => { stagesRun++; return Result<int, CheckoutError>.Ok(v + 1); });
Check(stagesRun == 0 && !chained.IsSuccess, "bind short-circuit");

failures += await HttpChecks.RunAsync(app, "/checkout/result");
Console.WriteLine($"baseline self-test failures={failures}");
return failures == 0 ? 0 : 1;

static bool Throws<TException>(Action action) where TException : Exception
{
    try { action(); return false; }
    catch (TException) { return true; }
}

public static class CheckoutFlow
{
    public static Result<OrderCreated, CheckoutError> Evaluate(CheckoutRequest request) =>
        Validate(request).Bind(Reserve).Bind(Pay);

    static Result<CheckoutRequest, CheckoutError> Validate(CheckoutRequest r)
    {
        if (Rules.Validate(r) is { } error) return error;
        return r;
    }

    static Result<CheckoutRequest, CheckoutError> Reserve(CheckoutRequest r)
    {
        if (Rules.Reserve(r) is { } error) return error;
        return r;
    }

    static Result<OrderCreated, CheckoutError> Pay(CheckoutRequest r)
    {
        if (Rules.Charge(r) is { } error) return error;
        return Rules.Create(r);
    }

    // The HTTP mapping is ours, not the Result type's. TError is an open hierarchy, so the
    // compiler cannot prove this switch exhaustive; a fallback arm is required.
    public static Results<Created<OrderCreated>, BadRequest<ValidationFailed>, Conflict<InventoryUnavailable>, JsonHttpResult<PaymentDeclined>>
        ToHttp(Result<OrderCreated, CheckoutError> result) =>
        result.Match<Results<Created<OrderCreated>, BadRequest<ValidationFailed>, Conflict<InventoryUnavailable>, JsonHttpResult<PaymentDeclined>>>(
            ok => TypedResults.Created($"/orders/{ok.OrderId}", ok),
            error => error switch
            {
                ValidationFailed v => TypedResults.BadRequest(v),
                InventoryUnavailable i => TypedResults.Conflict(i),
                PaymentDeclined p => TypedResults.Json(p, statusCode: StatusCodes.Status402PaymentRequired),
                _ => throw new UnreachableException($"Unmapped checkout error {error.GetType().Name}"),
            });
}

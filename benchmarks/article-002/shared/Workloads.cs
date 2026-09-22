using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace EngineeringLab.Article002;

public readonly record struct PaymentAuthorizationResult(
    int AuthorizedAmount, int Checksum, int OperationCount, string? Correlation);

public sealed record DiagnosticStacks(string BeforeSuspension, string AfterSuspension, string ExceptionStack);

public static class Workloads
{
    public static readonly AsyncLocal<string?> Correlation = new();

    public static async Task<PaymentAuthorizationResult> TaskSynchronous(int amount, int salt)
    {
        await Task.CompletedTask;
        return Authorize(amount, salt);
    }

    public static async ValueTask<PaymentAuthorizationResult> ValueTaskSynchronous(int amount, int salt)
    {
        await Task.CompletedTask;
        return Authorize(amount, salt);
    }

    public static async Task<PaymentAuthorizationResult> TaskSuspended(int amount, int salt, Task gate)
    {
        await gate;
        return Authorize(amount, salt);
    }

    // Four small async methods, including the authorization leaf.
    public static async Task<PaymentAuthorizationResult> LayeredSynchronous(int amount, int salt) =>
        CountLayer(await SyncLayer2(amount, salt));

    private static async Task<PaymentAuthorizationResult> SyncLayer2(int amount, int salt) =>
        CountLayer(await SyncLayer3(amount, salt));

    private static async Task<PaymentAuthorizationResult> SyncLayer3(int amount, int salt) =>
        CountLayer(await TaskSynchronous(amount, salt));

    public static async Task<PaymentAuthorizationResult> LayeredSuspended(int amount, int salt, Task gate) =>
        CountLayer(await SuspendedLayer2(amount, salt, gate));

    private static async Task<PaymentAuthorizationResult> SuspendedLayer2(int amount, int salt, Task gate) =>
        CountLayer(await SuspendedLayer3(amount, salt, gate));

    private static async Task<PaymentAuthorizationResult> SuspendedLayer3(int amount, int salt, Task gate) =>
        CountLayer(await TaskSuspended(amount, salt, gate));

    private static PaymentAuthorizationResult CountLayer(PaymentAuthorizationResult result) =>
        result with { OperationCount = result.OperationCount + 1 };

    private static PaymentAuthorizationResult Authorize(int amount, int salt)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Synthetic authorization amount must be positive.");
        return new(amount, unchecked((amount * 397) ^ salt), 1, Correlation.Value);
    }

    // Default TCS permits inline continuations. Invocation happens before release,
    // proving an incomplete await without timers, thread-pool work or I/O.
    public static PaymentAuthorizationResult RunSuspended(int amount, int salt, bool layered)
    {
        var gate = new TaskCompletionSource<bool>();
        var pending = layered
            ? LayeredSuspended(amount, salt, gate.Task)
            : TaskSuspended(amount, salt, gate.Task);
        if (pending.IsCompleted)
            throw new InvalidOperationException("Controlled gate did not suspend the workload.");
        gate.SetResult(true);
        if (!pending.IsCompleted)
            throw new InvalidOperationException("Measured operation did not complete during controlled gate release.");
        return pending.GetAwaiter().GetResult();
    }

    public static DiagnosticStacks CaptureDiagnosticStacks()
    {
        var gate = new TaskCompletionSource<bool>();
        var pending = DiagnosticOuter(gate.Task);
        if (pending.IsCompleted)
            throw new InvalidOperationException("Diagnostic chain did not suspend.");
        gate.SetResult(true);
        var live = pending.GetAwaiter().GetResult();

        var exceptionGate = new TaskCompletionSource<bool>();
        var fault = DiagnosticExceptionOuter(exceptionGate.Task);
        if (fault.IsCompleted)
            throw new InvalidOperationException("Exception diagnostic did not suspend.");
        exceptionGate.SetResult(true);
        try
        {
            fault.GetAwaiter().GetResult();
            throw new InvalidOperationException("Diagnostic exception was not observed.");
        }
        catch (SyntheticDiagnosticException error)
        {
            return new(live.Before, live.After, error.StackTrace ?? string.Empty);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<(string Before, string After)> DiagnosticOuter(Task gate) =>
        await DiagnosticMiddle(gate);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<(string Before, string After)> DiagnosticMiddle(Task gate) =>
        await DiagnosticLeaf(gate);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<(string Before, string After)> DiagnosticLeaf(Task gate)
    {
        var before = new StackTrace(false).ToString();
        await gate;
        return (before, new StackTrace(false).ToString());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task DiagnosticExceptionOuter(Task gate) => await DiagnosticExceptionMiddle(gate);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task DiagnosticExceptionMiddle(Task gate) => await DiagnosticExceptionLeaf(gate);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task DiagnosticExceptionLeaf(Task gate)
    {
        await gate;
        DiagnosticThrow();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DiagnosticThrow() => throw new SyntheticDiagnosticException();

    private sealed class SyntheticDiagnosticException : Exception { }
}

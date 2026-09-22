namespace EngineeringLab.Article002;

public static class SemanticTests
{
    public static object Run()
    {
        var previous = Workloads.Correlation.Value;
        try
        {
            Workloads.Correlation.Value = null;
            var random = new Random(20260922);
            var amount = random.Next(10_000, 100_000);
            var salt = random.Next();
            var expected = new PaymentAuthorizationResult(amount, unchecked((amount * 397) ^ salt), 1, null);
            var task = Workloads.TaskSynchronous(amount, salt);
            Require(task.IsCompletedSuccessfully, "Task synchronous fast path");
            Require(task.GetAwaiter().GetResult() == expected, "Task result");
            var anotherTask = Workloads.TaskSynchronous(amount, salt);
            Require(!ReferenceEquals(task, anotherTask), "Noncached custom struct Task result");
            Require(anotherTask.GetAwaiter().GetResult() == expected, "Repeated Task result");

            var valueTask = Workloads.ValueTaskSynchronous(amount, salt);
            Require(valueTask.IsCompletedSuccessfully, "ValueTask synchronous fast path");
            Require(valueTask.GetAwaiter().GetResult() == expected, "ValueTask single consumption result");
            var layered = Workloads.LayeredSynchronous(amount, salt);
            Require(layered.IsCompletedSuccessfully, "Layered synchronous fast path");
            Require(layered.GetAwaiter().GetResult() == expected with { OperationCount = 4 }, "Layered result/count");

            foreach (var useLayers in new[] { false, true })
            {
                var gate = new TaskCompletionSource<bool>();
                var pending = Start(amount, salt, gate.Task, useLayers);
                Require(!pending.IsCompleted, "Incomplete before gate release");
                gate.SetResult(true);
                Require(pending.IsCompletedSuccessfully, "Measured chain complete immediately after release");
                Require(pending.GetAwaiter().GetResult() == expected with { OperationCount = useLayers ? 4 : 1 }, "Suspended result/count");
                CheckCancellation(amount, salt, useLayers);
                CheckException(amount, salt, useLayers);
            }

            var releaseThread = Environment.CurrentManagedThreadId;
            var threadGate = new TaskCompletionSource<bool>();
            var threadProbe = ProbeSuspensionThread(threadGate.Task);
            Require(!threadProbe.IsCompleted, "Thread probe starts suspended");
            threadGate.SetResult(true);
            var threads = threadProbe.GetAwaiter().GetResult();
            Require(threads.Before == releaseThread && threads.After == releaseThread, "Same-thread gate suspension/resumption");

            // Two outstanding logical calls retain their own context even when
            // released in the opposite order under a third caller context.
            Workloads.Correlation.Value = "synthetic-a";
            var firstGate = new TaskCompletionSource<bool>();
            var first = Workloads.LayeredSuspended(amount, salt, firstGate.Task);
            Workloads.Correlation.Value = "synthetic-b";
            var secondGate = new TaskCompletionSource<bool>();
            var second = Workloads.LayeredSuspended(amount, salt, secondGate.Task);
            Require(!first.IsCompleted && !second.IsCompleted, "Context calls really suspend");
            Workloads.Correlation.Value = "synthetic-caller";
            secondGate.SetResult(true);
            firstGate.SetResult(true);
            Require(first.IsCompletedSuccessfully && second.IsCompletedSuccessfully, "Context chains complete immediately after release");
            Require(first.GetAwaiter().GetResult() == expected with { OperationCount = 4, Correlation = "synthetic-a" }, "First context propagation");
            Require(second.GetAwaiter().GetResult() == expected with { OperationCount = 4, Correlation = "synthetic-b" }, "Second context propagation");
            Require(Workloads.Correlation.Value == "synthetic-caller", "No context leakage to caller");
            Workloads.Correlation.Value = null;
            Require(Workloads.RunSuspended(amount, salt, true) == expected with { OperationCount = 4 }, "Absent context after active context");

            CheckInvalidAmount(() => Workloads.TaskSynchronous(0, salt).GetAwaiter().GetResult());
            CheckInvalidAmount(() => Workloads.ValueTaskSynchronous(0, salt).GetAwaiter().GetResult());
            CheckInvalidAmount(() => Workloads.LayeredSynchronous(0, salt).GetAwaiter().GetResult());
            CheckInvalidAmount(() => Workloads.RunSuspended(0, salt, false));
            CheckInvalidAmount(() => Workloads.RunSuspended(0, salt, true));

            return new
            {
                schemaVersion = 1,
                status = "PASS",
                seed = 20260922,
                amount,
                salt,
                expected.Checksum,
                singleOperationCount = 1,
                layeredOperationCount = 4,
                synchronousCompletion = true,
                noncachedTaskResult = true,
                incompleteBeforeRelease = true,
                sameThreadGateResumption = true,
                contextPropagation = true,
                contextIsolation = true,
                cancellationTokenPreserved = true,
                gateExceptionIdentityPreserved = true,
                invalidAmountRejected = true
            };
        }
        finally
        {
            Workloads.Correlation.Value = previous;
        }
    }

    private static Task<PaymentAuthorizationResult> Start(int amount, int salt, Task gate, bool layered) =>
        layered ? Workloads.LayeredSuspended(amount, salt, gate) : Workloads.TaskSuspended(amount, salt, gate);

    private static async Task<(int Before, int After)> ProbeSuspensionThread(Task gate)
    {
        var before = Environment.CurrentManagedThreadId;
        await gate;
        return (before, Environment.CurrentManagedThreadId);
    }

    private static void CheckCancellation(int amount, int salt, bool layered)
    {
        using var cancellation = new CancellationTokenSource();
        var gate = new TaskCompletionSource<bool>();
        var pending = Start(amount, salt, gate.Task, layered);
        Require(!pending.IsCompleted, "Cancellation starts suspended");
        cancellation.Cancel();
        gate.SetCanceled(cancellation.Token);
        try
        {
            pending.GetAwaiter().GetResult();
            throw new InvalidOperationException("Expected cancellation.");
        }
        catch (OperationCanceledException error)
        {
            Require(error.CancellationToken == cancellation.Token && pending.IsCanceled, "Cancellation token and status");
        }
    }

    private static void CheckException(int amount, int salt, bool layered)
    {
        var expectedError = new InvalidOperationException("synthetic-gate-failure");
        var gate = new TaskCompletionSource<bool>();
        var pending = Start(amount, salt, gate.Task, layered);
        Require(!pending.IsCompleted, "Exception starts suspended");
        gate.SetException(expectedError);
        try
        {
            pending.GetAwaiter().GetResult();
            throw new Exception("Expected gate failure.");
        }
        catch (InvalidOperationException error)
        {
            Require(ReferenceEquals(error, expectedError) && pending.IsFaulted, "Exception identity and status");
        }
    }

    private static void CheckInvalidAmount(Func<PaymentAuthorizationResult> action)
    {
        try
        {
            action();
            throw new InvalidOperationException("Expected amount validation failure.");
        }
        catch (ArgumentOutOfRangeException error)
        {
            Require(error.ParamName == "amount", "Amount exception parameter");
        }
    }

    private static void Require(bool condition, string check)
    {
        if (!condition)
            throw new InvalidOperationException("Semantic check failed: " + check);
    }
}

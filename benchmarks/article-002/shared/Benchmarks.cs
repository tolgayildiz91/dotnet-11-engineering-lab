using BenchmarkDotNet.Attributes;

namespace EngineeringLab.Article002;

[MemoryDiagnoser]
public class AsyncBenchmarks
{
    private int _amount;
    private int _salt;

    [GlobalSetup]
    public void Setup()
    {
        RuntimeAttestation.VerifyAndWrite();
        Capability.VerifyAndWrite();
        var random = new Random(20260922);
        _amount = random.Next(10_000, 100_000);
        _salt = random.Next();
    }

    [Benchmark]
    public PaymentAuthorizationResult TaskSynchronous() =>
        Workloads.TaskSynchronous(_amount, _salt).GetAwaiter().GetResult();

    [Benchmark]
    public PaymentAuthorizationResult ValueTaskSynchronous() =>
        Workloads.ValueTaskSynchronous(_amount, _salt).GetAwaiter().GetResult();

    [Benchmark]
    public PaymentAuthorizationResult TaskSuspension() => Workloads.RunSuspended(_amount, _salt, false);

    [Benchmark]
    public PaymentAuthorizationResult LayeredSynchronous() =>
        Workloads.LayeredSynchronous(_amount, _salt).GetAwaiter().GetResult();
}

[MemoryDiagnoser]
public class LayeredSuspensionBenchmarks
{
    private int _amount;
    private int _salt;

    [Params(false, true)]
    public bool CorrelationState { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        RuntimeAttestation.VerifyAndWrite();
        Capability.VerifyAndWrite();
        var random = new Random(20260922);
        _amount = random.Next(10_000, 100_000);
        _salt = random.Next();
    }

    // Setting/restoring AsyncLocal and gate allocation/release belong to this
    // end-to-end synthetic operation and are included in its measurements.
    [Benchmark]
    public PaymentAuthorizationResult LayeredSuspension()
    {
        var previous = Workloads.Correlation.Value;
        Workloads.Correlation.Value = CorrelationState ? "synthetic-payment-002" : null;
        try
        {
            return Workloads.RunSuspended(_amount, _salt, true);
        }
        finally
        {
            Workloads.Correlation.Value = previous;
        }
    }
}

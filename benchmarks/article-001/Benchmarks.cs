using BenchmarkDotNet.Attributes;

namespace EngineeringLab.Article001;

[MemoryDiagnoser]
public class BaselineBenchmarks
{
    private SyntheticData data = null!;

    [GlobalSetup]
    public void Setup()
    {
        RuntimeAttestation.VerifyAndWrite();
        data = new SyntheticData();
    }

    [Benchmark] public long ParseParcelScans() => Workloads.ParseParcelScans(data.ScanRecords);
    [Benchmark] public long ResolveInventory() => Workloads.ResolveInventory(data.Inventory, data.Requests);
    [Benchmark] public long EvaluatePaymentRisk() => Workloads.EvaluatePaymentRisk(data.Payments);
    [Benchmark] public long AggregateWindTelemetry() => Workloads.AggregateWindTelemetry(data.WindReadings);
    [Benchmark] public ShipmentCheckpoint[] TransformCheckpoints() => Workloads.TransformCheckpoints(data.Events);
    [Benchmark] public int[] SortScanSequence() => Workloads.SortScanSequence(data.SequenceKeys);
}

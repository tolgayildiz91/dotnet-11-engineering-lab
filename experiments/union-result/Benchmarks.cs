using BenchmarkDotNet.Attributes;

namespace UnionResultLab;
[MemoryDiagnoser]
public class OutcomeBenchmarks
{
    private Inputs data = null!;
    [ParamsSource(nameof(SelectedPercentage))] public int SuccessPercent { get; set; }
    public IEnumerable<int> SelectedPercentage => [Program.SelectedPercentage()];
    [GlobalSetup] public void Setup()
    {
        Attestation.Verify();
        data = new(SuccessPercent, Environment.GetEnvironmentVariable("ARTICLE003_METHOD"));
        Console.WriteLine($"INPUT percentage={SuccessPercent};count={Inputs.Count};successes={data.Success.Count(x=>x)};sequenceSha256={data.SequenceHash};toolchain=InProcessEmit;methodFilter={Environment.GetEnvironmentVariable("ARTICLE003_METHOD")}");
    }
    [Benchmark(OperationsPerInvoke = Inputs.Count)] public long ReferenceUnionCreate()
    {
        long sum = 0;
        for (int i=0;i<Inputs.Count;i++) { var x=Operations.CreateReferenceUnion(data.Success[i],data.Values[i]); sum+=Operations.HandleReferenceUnion(in x); }
        return sum;
    }
    [Benchmark(OperationsPerInvoke = Inputs.Count)] public long ReferenceResultCreate()
    {
        long sum = 0;
        for (int i=0;i<Inputs.Count;i++) { var x=Operations.CreateReferenceResult(data.Success[i],data.Values[i]); sum+=Operations.HandleReferenceResult(in x); }
        return sum;
    }
    [Benchmark(OperationsPerInvoke = Inputs.Count)] public long ValueUnionCreate()
    {
        long sum = 0;
        for (int i=0;i<Inputs.Count;i++) { var x=Operations.CreateValueUnion(data.Success[i],data.Values[i]); sum+=Operations.HandleValueUnion(in x); }
        return sum;
    }
    [Benchmark(OperationsPerInvoke = Inputs.Count)] public long ValueResultCreate()
    {
        long sum = 0;
        for (int i=0;i<Inputs.Count;i++) { var x=Operations.CreateValueResult(data.Success[i],data.Values[i]); sum+=Operations.HandleValueResult(in x); }
        return sum;
    }
    [Benchmark(OperationsPerInvoke = Inputs.Count)] public long ReferenceUnionHandle()
    {
        long sum=0; for(int i=0;i<Inputs.Count;i++) sum+=Operations.HandleReferenceUnion(in data.ReferenceUnions[i]); return sum;
    }
    [Benchmark(OperationsPerInvoke = Inputs.Count)] public long ReferenceResultHandle()
    {
        long sum=0; for(int i=0;i<Inputs.Count;i++) sum+=Operations.HandleReferenceResult(in data.ReferenceResults[i]); return sum;
    }
    [Benchmark(OperationsPerInvoke = Inputs.Count)] public long ValueUnionHandle()
    {
        long sum=0; for(int i=0;i<Inputs.Count;i++) sum+=Operations.HandleValueUnion(in data.ValueUnions[i]); return sum;
    }
    [Benchmark(OperationsPerInvoke = Inputs.Count)] public long ValueResultHandle()
    {
        long sum=0; for(int i=0;i<Inputs.Count;i++) sum+=Operations.HandleValueResult(in data.ValueResults[i]); return sum;
    }
}

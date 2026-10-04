using System.Diagnostics;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Perfolizer.Models;
using Perfolizer.Horology;
using Perfolizer.Mathematics.OutlierDetection;

namespace UnionResultLab;
public static class Attestation
{
    public const string Runtime = "11.0.0-rc.1.26425.128";
    public static object Verify(bool diagnostic=false,string stage="global-setup")
    {
        string actual=RuntimeInformation.FrameworkDescription.Replace(".NET ", "");
        string? loadedDirectory=Path.GetFileName(Path.GetDirectoryName(typeof(object).Assembly.Location));
        var debug=typeof(Attestation).Assembly.GetCustomAttribute<DebuggableAttribute>();
        var required=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {
            ["gcServer"]="0",["gcConcurrent"]="1",["TieredCompilation"]="1",["TieredPGO"]="1",["ReadyToRun"]="1"};
        foreach(var pair in required)
        {
            if(Environment.GetEnvironmentVariable("DOTNET_"+pair.Key)!=pair.Value)
                throw new InvalidOperationException("Missing or mismatched DOTNET_"+pair.Key);
            string? alternate=Environment.GetEnvironmentVariable("COMPlus_"+pair.Key);
            if(alternate is not null && alternate!=pair.Value) throw new InvalidOperationException("Mismatched COMPlus_"+pair.Key);
        }
        foreach(System.Collections.DictionaryEntry variable in Environment.GetEnvironmentVariables())
        {
            string name=(string)variable.Key;
            string? suffix=name.StartsWith("DOTNET_",StringComparison.OrdinalIgnoreCase)?name[7..]:name.StartsWith("COMPlus_",StringComparison.OrdinalIgnoreCase)?name[8..]:null;
            if(suffix is null || required.ContainsKey(suffix)) continue;
            if(diagnostic && suffix is "JitDisasm" or "JitStdOutFile") continue;
            if(name.StartsWith("COMPlus_",StringComparison.OrdinalIgnoreCase) || System.Text.RegularExpressions.Regex.IsMatch(suffix,"^(Jit|Tier|TC_|gc|GC|ReadyToRun|Zap|EnableHW|Stress|PGO|OSR|PROCESSOR_COUNT|Thread_|GCTotalPhysicalMemory|GCHeapHardLimit)",System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                throw new InvalidOperationException("Unreviewed runtime override: "+name);
        }
        var knobs=new Dictionary<string, object?>();
        foreach(var (name,expected) in new (string,bool)[] {
            ("System.GC.Server",false),("System.GC.Concurrent",true),
            ("System.Runtime.TieredCompilation",true),("System.Runtime.TieredPGO",true),
            ("System.Runtime.ReadyToRun",true) })
        {
            var value=AppContext.GetData(name); knobs[name]=value;
            if(value is null || Convert.ToBoolean(value)!=expected)
                throw new InvalidOperationException($"Runtime setting mismatch: {name}={value}");
        }
        if(actual!=Runtime || loadedDirectory!=Runtime || GCSettings.IsServerGC || GCSettings.LatencyMode!=GCLatencyMode.Interactive ||
           RuntimeInformation.ProcessArchitecture!=Architecture.X64 || Debugger.IsAttached ||
           debug?.IsJITOptimizerDisabled==true || SynchronizationContext.Current is not null)
            throw new InvalidOperationException("Runtime/build attestation mismatch");
        using var process=Process.GetCurrentProcess();
        if(!OperatingSystem.IsWindows() || process.ProcessorAffinity.ToInt64()!=1)
            throw new InvalidOperationException("Expected Windows logical processor 0 affinity mask 1");
        var payload=new {stage,affinityOrigin="Main explicitly sets mask1; BDN job also requests mask1",actual,environmentVersion=Environment.Version.ToString(),loadedDirectory,
            coreLib=typeof(object).Assembly.Location,serverGC=GCSettings.IsServerGC,
            latencyMode=GCSettings.LatencyMode.ToString(),architecture=RuntimeInformation.ProcessArchitecture.ToString(),
            debugger=Debugger.IsAttached,jitOptimizationsDisabled=debug?.IsJITOptimizerDisabled ?? false,
            priority=process.PriorityClass.ToString(),affinity=(OperatingSystem.IsWindows() || OperatingSystem.IsLinux()) ? process.ProcessorAffinity.ToInt64().ToString("X") : "unavailable",
            nativeContext=NativeContext.Capture(process.Handle),
            currentProcessor=Thread.GetCurrentProcessorId(),
            knobs,toolchain="InProcessEmit in independently launched per-case host",
            environment=new {gcServer=Environment.GetEnvironmentVariable("DOTNET_gcServer"),gcConcurrent=Environment.GetEnvironmentVariable("DOTNET_gcConcurrent"),tiering=Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),pgo=Environment.GetEnvironmentVariable("DOTNET_TieredPGO"),readyToRun=Environment.GetEnvironmentVariable("DOTNET_ReadyToRun")}};
        Console.WriteLine("ARTICLE003_RUNTIME "+JsonSerializer.Serialize(payload));
        return payload;
    }
}
public static class Program
{
    public static int SelectedPercentage()
    {
        if(!int.TryParse(Environment.GetEnvironmentVariable("ARTICLE003_SUCCESS_PERCENT"),out int value) || value is not (50 or 95))
            throw new InvalidOperationException("ARTICLE003_SUCCESS_PERCENT must explicitly be 50 or 95");
        return value;
    }
    public static int Main(string[] args)
    {
        try {
            if(!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("This measured protocol pins Windows logical processor 0");
            using var process=Process.GetCurrentProcess();
            process.ProcessorAffinity=new IntPtr(1);
            return Run(args);
        }
        catch(Exception ex) { Console.Error.WriteLine("VALIDATION_FAILURE "+ex.GetType().Name+": "+ex.Message); return 1; }
    }
    private static int Run(string[] args)
    {
        Attestation.Verify(args.Contains("--jit-probe"),"startup");
        if(args.Contains("--self-test")) { Console.WriteLine("ARTICLE003_SEMANTICS "+JsonSerializer.Serialize(SemanticTests.Run())); return 0; }
        if(args.Contains("--jit-probe"))
        {
            long sum=0;
            for(int repeat=0;repeat<300;repeat++) foreach(var percentage in new[]{50,95})
            {
                var data=new Inputs(percentage);
                for(int i=0;i<Inputs.Count;i++)
                {
                    var ru=Operations.CreateReferenceUnion(data.Success[i],data.Values[i]);
                    var rr=Operations.CreateReferenceResult(data.Success[i],data.Values[i]);
                    var vu=Operations.CreateValueUnion(data.Success[i],data.Values[i]);
                    var vr=Operations.CreateValueResult(data.Success[i],data.Values[i]);
                    sum+=Operations.HandleReferenceUnion(in ru)+Operations.HandleReferenceResult(in rr)
                        +Operations.HandleValueUnion(in vu)+Operations.HandleValueResult(in vr);
                }
                if(repeat%25==0) Thread.Sleep(20);
            }
            Console.WriteLine("JIT_PROBE_CHECKSUM "+sum); return 0;
        }
        string? method=Environment.GetEnvironmentVariable("ARTICLE003_METHOD");
        int selectedPercentage=SelectedPercentage();
        if(string.IsNullOrWhiteSpace(method) && !args.Contains("--list"))
            throw new InvalidOperationException("Select exactly one method with ARTICLE003_METHOD and --filter");
        // CPU metadata is outside the timed workload. Restricted Windows sessions
        // can deny BDN's WMI detector. Keep that denial visible and report Unknown;
        // never substitute invented hardware properties or change WMI permissions.
        var host=HostEnvironmentInfo.GetCurrent();
        string? bdnVersion=typeof(HostEnvironmentInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if(bdnVersion?.Split('+')[0]!="0.16.0-preview.1")
            throw new InvalidOperationException("BDN metadata compatibility code requires pinned version");
        try { _=host.Cpu.Value; }
        catch(System.Management.ManagementException ex) when(OperatingSystem.IsWindows() && ex.ErrorCode==System.Management.ManagementStatus.AccessDenied)
        {
            var setter=typeof(HostEnvironmentInfo).GetProperty(nameof(HostEnvironmentInfo.Cpu))?.GetSetMethod(true)
                ?? throw new InvalidOperationException("Pinned BDN CPU metadata setter unavailable",ex);
            setter.Invoke(host,[new Lazy<CpuInfo>(()=>CpuInfo.Unknown)]);
            Console.WriteLine("ARTICLE003_CPU_METADATA "+JsonSerializer.Serialize(new {
                status="Unknown",reason=ex.Message,scope="BDN report metadata only; independent launcher captures registry identity"}));
        }
        try { _=host.DotNetSdkVersion.Value; }
        catch(UnauthorizedAccessException ex) when(OperatingSystem.IsWindows())
        {
            var setter=typeof(HostEnvironmentInfo).GetProperty(nameof(HostEnvironmentInfo.DotNetSdkVersion))?.GetSetMethod(true)
                ?? throw new InvalidOperationException("Pinned BDN SDK metadata setter unavailable",ex);
            setter.Invoke(host,[new Lazy<string>(()=>string.Empty)]);
            Console.WriteLine("ARTICLE003_SDK_METADATA "+JsonSerializer.Serialize(new {
                status="Unavailable",reason=ex.Message,scope="BDN report metadata only; independent launcher captures dotnet --version"}));
        }
        var job=Job.Default.WithId("RC1-generated-storage").WithToolchain(InProcessEmitToolchain.Default)
            .WithAffinity(new IntPtr(1))
            .WithPlatform(Platform.X64).WithGcServer(false).WithGcConcurrent(true).DontEnforcePowerPlan()
            .WithWarmupCount(5).WithIterationCount(10).WithIterationTime(TimeInterval.FromMilliseconds(200))
            .WithLaunchCount(1).WithUnrollFactor(1).WithOutlierMode(OutlierMode.DontRemove);
        var config=ManualConfig.Create(DefaultConfig.Instance).AddJob(job).AddExporter(JsonExporter.Full)
            .WithOptions(ConfigOptions.KeepBenchmarkFiles);
        var summaries=BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args,config).ToArray();
        if(args.Contains("--list")) return 0;
        var reports=summaries.SelectMany(x=>x.Reports).ToArray();
        bool valid=summaries.All(s=>!s.HasCriticalValidationErrors) && reports.Length==1 &&
            reports.All(r=>r.Success && r.BenchmarkCase.Descriptor.WorkloadMethod.Name==method &&
                Convert.ToInt32(r.BenchmarkCase.Parameters.Items.Single(p=>p.Name=="SuccessPercent").Value)==SelectedPercentage() &&
                r.AllMeasurements.Count(m=>m.IterationMode==IterationMode.Workload && m.IterationStage==IterationStage.Actual)==10 &&
                r.AllMeasurements.Count(m=>m.IterationMode==IterationMode.Workload && m.IterationStage==IterationStage.Warmup)==5);
        Console.WriteLine("ARTICLE003_REPORT_VALIDATION "+JsonSerializer.Serialize(new{valid,method,percentage=SelectedPercentage(),reportCount=reports.Length}));
        return valid ? 0 : 1;
    }
}

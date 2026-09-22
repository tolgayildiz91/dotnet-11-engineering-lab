using System.Diagnostics;
using System.Reflection;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.CsProj;
using BenchmarkDotNet.Toolchains.DotNetCli;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Perfolizer.Horology;

namespace EngineeringLab.Article002;

public static class RuntimeAttestation
{
    public static string TargetFramework => typeof(Program).Assembly
        .GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()!.FrameworkName switch
    {
        ".NETCoreApp,Version=v8.0" => "net8.0",
        ".NETCoreApp,Version=v10.0" => "net10.0",
        ".NETCoreApp,Version=v11.0" => "net11.0",
        _ => throw new InvalidOperationException("Unsupported target")
    };
    public static string ExpectedVersion => TargetFramework switch
    {
        "net8.0" => "8.0.31", "net10.0" => "10.0.12", _ => "11.0.0-rc.1.26425.128"
    };
    public static void VerifyAndWrite()
    {
        string actual = RuntimeInformation.FrameworkDescription.Replace(".NET ", "", StringComparison.Ordinal);
        string? loadedDirectory = Path.GetFileName(Path.GetDirectoryName(typeof(object).Assembly.Location));
        if (actual != ExpectedVersion || loadedDirectory != ExpectedVersion || GCSettings.IsServerGC ||
            Debugger.IsAttached || RuntimeInformation.ProcessArchitecture != Architecture.X64 ||
            SynchronizationContext.Current is not null)
            throw new InvalidOperationException("Runtime/GC/architecture/debugger attestation failed");
        Console.WriteLine("ARTICLE002_RUNTIME " + JsonSerializer.Serialize(new {
            expected = ExpectedVersion, actual, loadedDirectory, targetFramework = TargetFramework,
            serverGC = GCSettings.IsServerGC, debugger = Debugger.IsAttached,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(), synchronizationContext = "null",
            coreLibVersion = typeof(object).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        }));
    }
}

public static class Capability
{
#if RUNTIME_ASYNC
    public const bool ExpectedRuntimeAsync = true;
#else
    public const bool ExpectedRuntimeAsync = false;
#endif
    public static void VerifyAndWrite()
    {
        var methods = typeof(Workloads).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(m => m.Name is "TaskSynchronous" or "ValueTaskSynchronous" or "TaskSuspended" or
                "LayeredSynchronous" or "LayeredSuspended" or "SyncLayer2" or "SyncLayer3" or
                "SuspendedLayer2" or "SuspendedLayer3" or "DiagnosticOuter" or "DiagnosticMiddle" or
                "DiagnosticLeaf" or "DiagnosticExceptionOuter" or "DiagnosticExceptionMiddle" or "DiagnosticExceptionLeaf").Select(m => new {
                    name = m.Name, flags = (int)m.GetMethodImplementationFlags(),
                    runtimeAsync = ((int)m.GetMethodImplementationFlags() & 0x2000) != 0,
                    stateMachine = m.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType.FullName
                }).OrderBy(m => m.name).ToArray();
        if (methods.Length != 15 || methods.Any(m => m.runtimeAsync != ExpectedRuntimeAsync ||
            (m.stateMachine is null) != ExpectedRuntimeAsync))
            throw new InvalidOperationException("Application async transformation does not match requested configuration");
        Console.WriteLine("ARTICLE002_CAPABILITY " + JsonSerializer.Serialize(new { expectedRuntimeAsync = ExpectedRuntimeAsync, methods }));
    }
}

public static class Program
{
    public static int Main(string[] args)
    {
        RuntimeAttestation.VerifyAndWrite();
        Capability.VerifyAndWrite();
        if (args.Contains("--self-test", StringComparer.Ordinal))
        {
            Console.WriteLine("ARTICLE002_SEMANTICS " + JsonSerializer.Serialize(SemanticTests.Run()));
            Console.WriteLine("ARTICLE002_STACKS " + JsonSerializer.Serialize(Workloads.CaptureDiagnosticStacks()));
            return 0;
        }
        bool inProcess = Environment.GetEnvironmentVariable("ARTICLE002_INPROCESS") == "1";
        string version = RuntimeAttestation.ExpectedVersion;
        var settings = new NetCoreAppSettings(RuntimeAttestation.TargetFramework, version, $"Pinned-{version}");
        var job = Job.Default.WithId($"{version}-{(Capability.ExpectedRuntimeAsync ? "runtime" : "conventional")}")
            .WithToolchain(inProcess ? InProcessEmitToolchain.Default : CsProjCoreToolchain.From(settings))
            .WithPlatform(Platform.X64).WithGcServer(false).WithGcConcurrent(true).DontEnforcePowerPlan()
            .WithWarmupCount(5).WithIterationCount(12).WithIterationTime(TimeInterval.FromMilliseconds(250))
            .WithLaunchCount(1)
            .WithArguments([new MsBuildArgument("/p:RollForward=Disable"),new MsBuildArgument("/p:LangVersion=12.0")]);
        var config = ManualConfig.Create(DefaultConfig.Instance).AddJob(job)
            .AddColumn(StatisticColumn.Median).WithOptions(ConfigOptions.KeepBenchmarkFiles);
        var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config).ToArray();
        return summaries.Length > 0 && summaries.All(s => !s.HasCriticalValidationErrors && s.Reports.All(r => r.Success)) ? 0 : 1;
    }
}

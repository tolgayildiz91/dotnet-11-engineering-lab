using System.Diagnostics;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.CsProj;
using BenchmarkDotNet.Toolchains.DotNetCli;

namespace EngineeringLab.Article001;

public static class RuntimeAttestation
{
    public static string TargetFramework => typeof(Program).Assembly
        .GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()?.FrameworkName switch
    {
        ".NETCoreApp,Version=v8.0" => "net8.0",
        ".NETCoreApp,Version=v10.0" => "net10.0",
        ".NETCoreApp,Version=v11.0" => "net11.0",
        _ => throw new InvalidOperationException("Unsupported compiled target.")
    };
    public static string ExpectedVersion => TargetFramework switch
    {
        "net8.0" => "8.0.31", "net10.0" => "10.0.12", "net11.0" => "11.0.0-rc.1.26425.128",
        _ => throw new InvalidOperationException("Unsupported compiled target.")
    };

    public static void VerifyAndWrite()
    {
        string actual = RuntimeInformation.FrameworkDescription.Replace(".NET ", "", StringComparison.Ordinal);
        string? loadedDirectory = Path.GetFileName(Path.GetDirectoryName(typeof(object).Assembly.Location));
        string expected = ExpectedVersion;
        string? jobExpected = Environment.GetEnvironmentVariable("ARTICLE001_EXPECTED_RUNTIME");
        if (jobExpected is not null && jobExpected != expected)
            throw new InvalidOperationException("Job runtime does not match compiled target.");
        if (actual != expected || loadedDirectory != expected || GCSettings.IsServerGC || Debugger.IsAttached ||
            RuntimeInformation.ProcessArchitecture != Architecture.X64)
            throw new InvalidOperationException("Runtime, architecture, GC or debugger attestation failed.");
        Console.WriteLine("ARTICLE001_RUNTIME " + JsonSerializer.Serialize(new
        {
            expected, actual, loadedDirectory, targetFramework = TargetFramework, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            serverGC = GCSettings.IsServerGC, debugger = Debugger.IsAttached,
            coreLibVersion = typeof(object).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        }));
    }
}

public static class Program
{
    public static int Main(string[] args)
    {
        RuntimeAttestation.VerifyAndWrite();
        if (args.Contains("--self-test", StringComparer.Ordinal))
        {
            Console.WriteLine("ARTICLE001_SEMANTICS " + JsonSerializer.Serialize(SemanticTests.Run()));
            return 0;
        }
        string version = RuntimeAttestation.ExpectedVersion;
        string tfm = RuntimeAttestation.TargetFramework;
        var settings = new NetCoreAppSettings(tfm, version, $"Pinned-{version}");
        var job = Job.Default.WithId($"Runtime-{version}")
            .WithToolchain(CsProjCoreToolchain.From(settings))
            .WithPlatform(Platform.X64).WithGcServer(false).WithGcConcurrent(true)
            .DontEnforcePowerPlan()
            .WithArguments([new MsBuildArgument("/p:RollForward=Disable"), new MsBuildArgument("/p:LangVersion=12.0")])
            .WithEnvironmentVariable("ARTICLE001_EXPECTED_RUNTIME", version)
            .WithEnvironmentVariable("DOTNET_ROLL_FORWARD", "Disable")
            .WithEnvironmentVariable("DOTNET_TieredCompilation", "1")
            .WithEnvironmentVariable("DOTNET_TieredPGO", "1");
        var config = ManualConfig.Create(DefaultConfig.Instance).AddJob(job)
            .AddColumn(StatisticColumn.Median)
            .WithOptions(ConfigOptions.KeepBenchmarkFiles);
        var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config).ToArray();
        return summaries.Length > 0 && summaries.All(s => !s.HasCriticalValidationErrors && s.Reports.All(r => r.Success)) ? 0 : 1;
    }
}

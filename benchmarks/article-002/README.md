# Runtime Async experiments

Two comparison families use identical linked workload source:

| Family | Configurations | Application compiler feature |
| --- | --- | --- |
| A | .NET 8.0.31, 10.0.12, 11.0.0-rc.1.26425.128 | `Features=runtime-async=off` |
| B | .NET 11.0.0-rc.1.26425.128, OFF versus ON | `Features=runtime-async=off` / `Features=runtime-async=on` |

Install exact x64 SDK **11.0.100-rc.1.26425.128** and all three exact x64 runtimes.
Run these commands from `benchmarks/article-002`, where `global.json` applies.
Both families use C# 12 and BenchmarkDotNet **0.16.0-preview.1**. SDK and runtime
roll-forward are disabled. Conventional projects also set `UseRuntimeAsync=false`;
the enabled project selects the compiler feature directly. Neither preview language
mode nor `EnablePreviewFeatures` is set. Application feature selection does not
control how the installed runtime libraries were built.

## Semantic and stack checks

Run all five checks before timing. Every command also captures live stacks before
and after controlled suspension, and a separately caught exception stack.

```powershell
dotnet run --project family-a/Article002FamilyA.csproj -c Release -f net8.0 -- --self-test
dotnet run --project family-a/Article002FamilyA.csproj -c Release -f net10.0 -- --self-test
dotnet run --project family-a/Article002FamilyA.csproj -c Release -f net11.0 -- --self-test
dotnet run --project family-b/conventional/Article002FamilyBConventional.csproj -c Release -- --self-test
dotnet run --project family-b/runtime-async/Article002FamilyBRuntimeAsync.csproj -c Release -- --self-test
```

Output includes `ARTICLE002_RUNTIME`, `ARTICLE002_CAPABILITY`,
`ARTICLE002_SEMANTICS` and `ARTICLE002_STACKS`. Checks enforce the exact loaded
runtime, x64, Workstation GC, no debugger and a null SynchronizationContext.
Method metadata distinguishes runtime-async transformation from conventional
`AsyncStateMachineAttribute` lowering for workload and diagnostic methods;
compilation alone is not the activation check. Semantic checks cover values,
operation counts, synchronous completion, actual incomplete awaits, correlation
propagation/isolation, cancellation and exceptions.

These semantic checks execute the outer project binaries built from the same
source as the benchmarks; they do not execute byte-identical BenchmarkDotNet
child binaries. Generated children perform their own runtime and method-metadata
checks during benchmark setup.

## Timing commands

The selected dataset uses the normal **out-of-process** `CsProjCoreToolchain`,
with generated BenchmarkDotNet child executables. Set these shell variables:

```powershell
$env:ARTICLE002_INPROCESS='0'
$env:DOTNET_ROLL_FORWARD='Disable'
$env:DOTNET_TieredCompilation='1'
$env:DOTNET_TieredPGO='1'
$pass='pass1'
```

Run these five commands sequentially, without other heavy work. Each configuration
and pass has a distinct artifact directory:

```powershell
dotnet run --project family-a/Article002FamilyA.csproj -c Release -f net8.0 -- --filter '*' --exporters json csv --artifacts "BenchmarkDotNet.Artifacts/reproduction/$pass/a8"
dotnet run --project family-a/Article002FamilyA.csproj -c Release -f net10.0 -- --filter '*' --exporters json csv --artifacts "BenchmarkDotNet.Artifacts/reproduction/$pass/a10"
dotnet run --project family-a/Article002FamilyA.csproj -c Release -f net11.0 -- --filter '*' --exporters json csv --artifacts "BenchmarkDotNet.Artifacts/reproduction/$pass/a11"
dotnet run --project family-b/conventional/Article002FamilyBConventional.csproj -c Release -- --filter '*' --exporters json csv --artifacts "BenchmarkDotNet.Artifacts/reproduction/$pass/b-off"
dotnet run --project family-b/runtime-async/Article002FamilyBRuntimeAsync.csproj -c Release -- --filter '*' --exporters json csv --artifacts "BenchmarkDotNet.Artifacts/reproduction/$pass/b-on"
```

For the second independent process pass, set `$pass='pass2'` and execute the same
commands in reverse order: b-on, b-off, a11, a10, a8. Do not pool passes or families.
Each invocation starts a fresh host, which starts generated workload children.
The job selects x64, Workstation concurrent GC, tiered compilation and tiered PGO,
five warmup iterations, twelve target iterations with a 250 ms target iteration
time, and one launch. Adaptive pilot work precedes measurement. The job preserves
the current power plan; the recorded study used Balanced. Retained N can be below
twelve after BenchmarkDotNet outlier handling. Preserve raw exports and warnings.

The child processes inherit the recorded `DOTNET_TieredCompilation` and
`DOTNET_TieredPGO` environment variables. Their generated runtimeconfig files do
not independently establish those tiering settings.

`ARTICLE002_INPROCESS=1` selects an experimental fallback retained in the source.
It was not used for the selected results and must not be mixed into their comparison.

## Operation boundaries

Six rows per configuration cover Task synchronous completion, ValueTask synchronous
completion, Task controlled suspension, four-layer synchronous completion, and
four-layer suspension with absent or active AsyncLocal correlation. Results use a
custom struct, avoiding small-integer Task result-cache cases. ValueTask is consumed
once. Inputs are independently synthetic, with seed 20260922.

For suspension, a newly allocated TaskCompletionSource starts incomplete. The async
call must return an incomplete task before the gate is released. Default continuations
permit inline execution on the releasing thread; the workload must complete before
`SetResult` returns. Timing includes gate allocation, release, continuation work and
result consumption. No timer forces suspension. This does not measure thread-pool
dispatch, network I/O or service latency.

The layered correlation benchmark includes reading the previous AsyncLocal value,
setting the selected correlation state and restoring the previous value in `finally`.
Its absent/active difference does not isolate ExecutionContext capture cost.
Diagnostic methods use NoInlining and run outside timing; their live and exception
stacks are different observations, not interchangeable evidence.

See the [dataset and methodology guide](../../docs/article-002/README.md),
[all 60 numeric rows](../../docs/article-002/all-values.csv) and
[raw-result inventory](../../results/article-002/rc1-async-002/raw-files.json).

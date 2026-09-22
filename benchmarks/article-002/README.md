# Runtime Async experiments

Two distinct comparisons use identical linked source files:

- Family A: conventional application async on .NET 8.0.31, 10.0.12 and 11 RC1.
- Family B: conventional versus runtime-async application code on .NET 11 RC1.

The SDK is pinned by global.json. Runtime roll-forward is disabled. Conventional
projects explicitly use `Features=runtime-async=off`; the enabled project uses
`Features=runtime-async=on`. No preview language or API flag is required here.
The .NET 11 libraries are independently built; conventional application code does
not imply that runtime libraries use conventional async.

```powershell
dotnet run --project family-a/Article002FamilyA.csproj -c Release -f net8.0 -- --self-test
dotnet run --project family-a/Article002FamilyA.csproj -c Release -f net10.0 -- --self-test
dotnet run --project family-a/Article002FamilyA.csproj -c Release -f net11.0 -- --self-test
dotnet run --project family-b/conventional/Article002FamilyBConventional.csproj -c Release -- --self-test
dotnet run --project family-b/runtime-async/Article002FamilyBRuntimeAsync.csproj -c Release -- --self-test
```

Each invocation verifies exact runtime, x64, Workstation GC, absent debugger,
null SynchronizationContext and async metadata for timing and diagnostic methods.
Semantic tests verify results, operation counts, real incomplete awaits, completed
fast paths, context isolation, cancellation and exception behavior. The diagnostic
output separates live stacks before/after suspension from exception stacks.

Benchmarks use BenchmarkDotNet 0.16.0-preview.1. The default toolchain generates a
separate process. Set `ARTICLE002_INPROCESS=1` to select the explicit InProcessEmit
mode, then use a fresh `dotnet run` process per pass/configuration. Never mix the two
toolchains in one comparison. In-process mode uses a dedicated benchmark thread;
repeatability requires separate host processes. Example:

```powershell
$env:ARTICLE002_INPROCESS='1'
$env:DOTNET_ROLL_FORWARD='Disable'
$env:DOTNET_TieredCompilation='1'
$env:DOTNET_TieredPGO='1'
dotnet run --project family-b/runtime-async/Article002FamilyBRuntimeAsync.csproj -c Release -- --filter '*' --exporters json csv
```

The controlled suspension gate is incomplete at method entry and is released only
after the async call returns an incomplete task. It must finish by gate release
return; all results are consumed. This measures the complete synthetic gate/task
operation, including gate construction, not network I/O or production latency.
AsyncLocal cases include setting/restoring correlation state. Results are custom
structs and are not small-integer Task-cache cases. Data is independently synthetic,
generated with seed 20260922. No production data, database or external service is used.

Five warmup and twelve 250ms target iterations follow adaptive pilot work. Keep
every exported observation and warning; compare process passes separately.
Documentation and measured results are separate. This directory initially provides
the implementation and semantic protocol; results require completed local runs.

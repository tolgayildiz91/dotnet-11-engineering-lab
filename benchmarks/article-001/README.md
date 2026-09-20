# Article 001 runtime and BCL baseline

Six independently designed synthetic backend workloads, built with the same SDK and C# language version. .NET 11 is **RC1, not GA**. GA retesting will preserve the original RC evidence.

## Reproduce

Install SDK `11.0.100-rc.1.26425.128` and x64 runtimes `8.0.31`, `10.0.12`, and `11.0.0-rc.1.26425.128`. Run from this directory. `global.json` disables SDK roll-forward. The project and BDN toolchain select exact runtimes; runtime roll-forward is disabled. No .NET 8 SDK is needed.

```powershell
dotnet restore Article001.csproj --locked-mode --configfile NuGet.Config
dotnet build Article001.csproj -c Release --no-restore
dotnet run -c Release -f net8.0 --no-build -- --self-test
dotnet run -c Release -f net10.0 --no-build -- --self-test
dotnet run -c Release -f net11.0 --no-build -- --self-test
dotnet run -c Release -f net8.0 --no-build -- --filter '*' --exporters json csv --artifacts BenchmarkDotNet.Artifacts/pass1-net8
dotnet run -c Release -f net10.0 --no-build -- --filter '*' --exporters json csv --artifacts BenchmarkDotNet.Artifacts/pass1-net10
dotnet run -c Release -f net11.0 --no-build -- --filter '*' --exporters json csv --artifacts BenchmarkDotNet.Artifacts/pass1-net11
```

Repeat the three benchmark commands in reverse runtime order, with new `pass2` artifact paths. Run serially on an otherwise quiet workstation. Retain the console logs and full JSON. A nonzero exit or missing child runtime attestation invalidates a run. Each worker checks its compiled target against loaded CoreLib, x64 architecture, Workstation GC and debugger state. The harness preserves the current Windows power plan; record it before and after execution.

BenchmarkDotNet `0.16.0-preview.1` is an explicitly pinned prerelease dependency. Its adaptive pilot, warmup and measurement defaults are retained. Median is included; sample counts and raw measurements are available in JSON. MemoryDiagnoser reports allocations. The benchmark returns observable scalar values or escaping output arrays. An invocation means **one batch**, not one record. Ratios between different workload methods are meaningless; compare the same method across runtimes.

## Workloads

| Method | Batch | Measured boundary |
|---|---:|---|
| ParseParcelScans | 256 records | Span separators, invariant integer validation and checksum |
| ResolveInventory | 1024 requests, 4096 entries | Ordinal dictionary probes, 768 hits and 256 misses |
| EvaluatePaymentRisk | 1024 records | Fictional branch-heavy rules and weighted result checksum |
| AggregateWindTelemetry | 4096 readings | Scalar integer sum and energy, designated control |
| TransformCheckpoints | 256 records | New array and 256 escaping immutable objects; existing ID strings reused |
| SortScanSequence | 2048 keys | Clone unsorted array, sort clone and return it |

Inputs use a specified uint LCG, seed 12026. Every fourth inventory request is a miss. Equal-content hit strings are distinct objects from dictionary keys. Data creation is outside measurements. Semantic tests use independent references, 495 payment boundary/precedence cases, empty/malformed/boundary inputs, source fingerprints and repeated-call freshness checks. All target outputs must match.

## Interpretation limits

This is a warmed, repeated-batch, single-threaded runtime/BCL comparison on one machine. The fixed input distribution may train branches and populate caches. Workstation concurrent GC and consistent enabled tiered compilation/PGO are deliberate; these measurements cannot establish ASP.NET throughput, tail latency, production traffic behavior or Server GC performance. Sorting includes copying and its allocation, but does not include a second complete traversal of the returned output. Same compiler/source does not mean identical target reference assemblies or isolate JIT causality. No actual financial or proprietary business rules are represented.

Results will be accompanied by exact environment, original observations, variability and provenance. No workload was selected to promise a winner.

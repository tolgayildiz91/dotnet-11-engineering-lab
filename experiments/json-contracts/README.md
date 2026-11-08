# JSON contract experiments

System.Text.Json behavior in .NET 11 RC3 for backend payloads. Deterministic,
independently invented events; no production data. Functional contract tests,
not a performance benchmark. Observed 2026-11-08 on Windows x64.

## Reproduction

Install .NET SDK 11.0.100-rc.3.26522.148 and runtimes 8.0.33, 10.0.14 and
11.0.0-rc.3.26522.148. Run from this directory using PowerShell 7:

```powershell
pwsh ./run.ps1 -SourceDirectory . -EvidenceDirectory ./artifacts/repro-01
```

Use a fresh directory. The runner writes isolated SDK projects with exact
RuntimeFrameworkVersion and RollForward=Disable, builds Release with C#15.0,
no shared compiler/analyzers, and launches positive executables through
dotnet exec --fx-version. No PackageReference or third-party package dependency is used. NuGet.Config clears configured sources; the SDK library-packs feed can still appear during restore.
Compiler identity capture uses the standard Windows SDK installation path;
adapt that path for a different installation. Runner portability is untested.

Core11.cs owns union, closed-polymorphism and Stream JSONL assertions.
Portable.cs is identical across net8.0/net10.0/net11.0 with target-conditional
expectation checks; reflection and generated metadata calls are separate.
Old-target failures are observations, not substituted implementations.
ApiProbe.cs deliberately fails to compile for net8/net10. OpenGeneration.cs
deliberately fails source generation on net11. Expected failures are required
by the runner. A successful script exit means expected exits matched;
consult per-case outputs. Concise observations are in results.md.

The same SDK/compiler builds all targets, but target-specific reference packs
provide different serializer generators. This is not historical SDK8 versus
SDK10 versus SDK11 benchmarking. Union/closed fixtures run on net11 only.
Reference packs: 8.0.33, 10.0.14 and 11.0.0-rc.3.26522.148.
Generator file/assembly versions respectively: 8.0.15.2219, 10.0.15.2326,
11.0.15.2348. Their product versions match the corresponding runtime builds.
Normal-verbosity build logs record the actual analyzer DLL paths.

## Limits

Only inbox serializer versions were compared, not a newer NuGet package on an
older target. Portable generated tests use metadata mode without reflection
fallback. No Native AOT/trimming deployment was tested. JSONL uses a finite
MemoryStream: no HTTP flush, client latency, cancellation, backpressure,
memory or throughput conclusions. Generic/nested closed types and discriminator
rename stability are untested. Union classifications depend on serializer options.
RC3 results are not GA results. Rerun after GA with new pins and a fresh output
directory; preserve this baseline.

## Official references

- [Union JSON](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/union-types)
- [Async serialization](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/supported-types)
- [Polymorphism](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/polymorphism)
- [Generated contracts](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation-modes)

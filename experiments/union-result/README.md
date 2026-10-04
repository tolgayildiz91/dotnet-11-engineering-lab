# Generated C# union storage versus an explicit Result

A synthetic climbing-course booking outcome has accepted and rejected cases.
This experiment compares a C# 15 `union` declaration with one readonly Result
implementation, using reference and value payloads independently. It tests
compiler diagnostics, defaults, JSON behavior and matched construction/handling.
This is a hobby experiment, with no database or external application data.

See [measured results and limitations](RESULTS.md) for both passes and allocation.

## Build and check semantics

Run from this directory on Windows x64 with the exact SDK/runtime in `global.json`
and `UnionResult.csproj`. C# 15 features require `LangVersion=preview`. The package
lock pins BenchmarkDotNet 0.16.0-preview.1.

```powershell
dotnet restore --locked-mode
dotnet build -c Release --no-restore
$env:DOTNET_gcServer='0'
$env:DOTNET_gcConcurrent='1'
$env:DOTNET_TieredCompilation='1'
$env:DOTNET_TieredPGO='1'
$env:DOTNET_ReadyToRun='1'
dotnet bin/Release/net11.0/UnionResult.dll --self-test
```

The executable fails on runtime, GC or relevant environment mismatches and
optimization-disabled builds. Remove other DOTNET_/COMPlus_ JIT/GC overrides;
diagnostic JIT logging is permitted only for the separate `--jit-probe` mode.
The semantic runner compares every input across all eight paths, factory/default
states and explicit reference-family response DTO bytes/validation. Native union
JSON probes are separate from that DTO contract.

See [compiler fixtures](fixtures/README.md) for positive/negative examples that
must be compiled separately; their expected failures are excluded from this project.

## Repeat the per-case measurement

```powershell
$methods=@('ReferenceUnionCreate','ReferenceResultCreate','ValueUnionCreate',
 'ValueResultCreate','ReferenceUnionHandle','ReferenceResultHandle',
 'ValueUnionHandle','ValueResultHandle')
$cases=@(foreach($percentage in @(50,95)) {
 foreach($method in $methods) { [pscustomobject]@{percentage=$percentage;method=$method} }
})
foreach($pass in @(1,2)) {
 $order=@($cases)
 if($pass -eq 2) { [array]::Reverse($order) }
 foreach($case in $order) {
  $env:ARTICLE003_SUCCESS_PERCENT=[string]$case.percentage
  $env:ARTICLE003_METHOD=$case.method
  $out="measurements/pass-$pass/$($case.percentage)/$($case.method)"
  dotnet bin/Release/net11.0/UnionResult.dll --filter "*.$($case.method)*" --artifacts $out --exporters json csv
  if($LASTEXITCODE -ne 0) { throw 'Measurement or attestation failed' }
 }
}
```

Each command creates a fresh process for exactly one method and percentage.
The Windows host and BDN job both pin affinity mask1 (logical processor0), verified
at startup and GlobalSetup. Results characterize that single processor placement.
BenchmarkDotNet uses InProcessEmit inside that host, five warmups and ten measured
iterations, 200 ms target iteration time, adaptive pilot invocation counts,
UnrollFactor 1, no outlier deletion, and 1024 operations per invocation. Inputs
use a documented fixed LCG and multiply-high Fisher-Yates shuffle. Exact counts
are 512 and 972 accepted inputs, rather than exactly 95% of 1024.

The boundary methods deliberately use NoInlining and `in` handling parameters.
Construction includes one new reference record on both sides; generated value
unions box their cases. Native layout, return ABI, validation, locality and GC
contribute to results. Custom nonboxing unions and other Result representations
are unmeasured. These runs do not establish endpoint latency, a universal winner,
GA behavior or optimizer causality. JIT dumps from `--jit-probe` are illustrative,
because their profile differs from case-isolated measurements.

The local SDK compiler-launch named-pipe restriction required direct exact compiler
replay followed by successful normal incremental MSBuild completion. This local
build limitation does not change the public standard build commands. Measurements
use the same optimized final binary; the fallback and environment limits are
described with the curated measured results.

The pinned BDN version has no public detector override. In this restricted Windows
host, CPU WMI access and the SDK detector's redirected process launch can fail.
Before timing, the executable catches the recorded permission failures and uses
the protected host metadata setters through reflection: CPU becomes `Unknown`,
SDK metadata becomes unavailable. This is a version-specific compatibility
workaround, not a supported BDN configuration. It changes report metadata only.
The private launcher independently records the registry CPU identity and actual
`dotnet --version`; no hardware properties are inferred from the unknown fields.

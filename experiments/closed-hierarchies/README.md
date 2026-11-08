# Closed hierarchy compiler experiments

What can the compiler prove about a bounded payment outcome hierarchy?
The model is independently invented, with deterministic synthetic inputs.
This is a compiler and maintenance experiment, with no performance benchmark.

## Toolchain

Observed on 2026-11-08, Windows x64. Baseline: SDK 10.0.403,
runtime 10.0.14, C# 14.0, compiler 5.9.0-1.26523.112.
Closed/control cases: SDK 11.0.100-rc.3.26522.148,
runtime 11.0.0-rc.3.26522.148, C# 15.0, compiler 5.12.0-1.26522.148.
The SDK is prerelease; language mode is explicitly 15.0, never preview.
No NuGet dependencies; package sources are cleared. Install these exact SDKs
and runtimes before reproducing. GA results are not claimed.

## Reproduce

From this directory, in PowerShell 7:

```powershell
dotnet build Consumer/Consumer.csproj -c Release --disable-build-servers -m:1 -p:UseSharedCompilation=false
dotnet run --project Consumer/Consumer.csproj -c Release --no-build --no-restore
pwsh ./run.ps1 -SourceDirectory . -EvidenceDirectory ./artifacts/repro-01
```

Use a new evidence directory each time. The script generates small separate
Model, Consumer and Foreign SDK projects, exact global.json files, and captures
commands, exits, compiler diagnostics and runtime assertions locally. Build uses
Release, nullable analysis, no shared compiler, no analyzers, no SDK roll-forward
and exact runtime selection. Model.dll owns the hierarchy; Foreign.dll is an
actual different assembly. Consumer references Model and, in the indirect case,
Foreign. The source code is shared across scenarios using preprocessor symbols.
All are public fixtures; generated outputs are ignored, not repository artifacts.
The script verifies expected diagnostic severity as well as exit codes.

## Observed results

| Scenario | Build exit | Diagnostic | Observation |
| --- | --- | --- | --- |
| open, C# 14 | 0 | warning CS8509 | Known subtypes do not exhaust open base |
| open-fallback, C# 14 | 0 | none | Competent fallback handles unknown inputs, including null |
| open-csharp15 | 0 | warning CS8509 | Same-compiler open control still needs fallback |
| closed | 0 | none | All branches covered across assembly reference |
| missing | 0 | warning CS8509 | ReviewRequired missing |
| added | 0 | warning CS8509 | Pending missing from unchanged recompiled consumer |
| missing-werror | 1 | error CS8509 | Warning policy promotes incomplete match to build failure |
| direct | 1 | error CS9382 | ForeignOutcome cannot directly derive from closed PaymentOutcome |
| open-direct | 0 | none | Same foreign direct declaration compiles against open base |
| indirect | 0 | none | ForeignReview derives from open ReviewRequired and matches its arm |
| intermediate | 0 | none | Closed ReviewRequired permits exhaustive ManualReview/RiskReview leaf arms |
| intermediate-foreign | 1 | error CS9382 | Foreign direct subclass of closed intermediate rejected |
| nullable | 0 | warning CS8655 | Nullable input with no null arm |
| null-handled | 0 | none | Explicit null arm returns absent |

Ten executable configurations passed deterministic assertions. Nonnullable
closed input forced to null with null! threw System.Runtime.CompilerServices.SwitchExpressionException.
The nullable fixture behaved the same at runtime until its explicit null arm
returned absent. Pending is added only to the model; the consumer switch source
is unchanged. That test recompiles the consumer and does not execute Pending.
It does not test replacement of a model beneath an already compiled consumer.

Exact concise diagnostics from these builds:

```text
warning CS8509: The switch expression does not handle all possible values of its input type (it is not exhaustive). For example, the pattern 'ClosedPayments.ReviewRequired' is not covered.
warning CS8509: The switch expression does not handle all possible values of its input type (it is not exhaustive). For example, the pattern 'ClosedPayments.Pending' is not covered.
error CS9382: 'ForeignOutcome': cannot use a closed type 'PaymentOutcome' from another assembly as a base type.
warning CS8655: The switch expression does not handle some null inputs (it is not exhaustive). For example, the pattern 'null' is not covered.
```

## Interpretation and limits

The extra C#15 OPEN control isolates the modifier from the SDK/language change
in the required C#14 baseline. The OPEN direct-derivation control isolates the
assembly restriction in these fixtures. C# 14 already offers constructor
accessibility patterns that can constrain external derivation; record copy
constructors need separate care. This experiment does not compare every such
design. It measures exhaustive matching plus a dedicated declaration-level rule.
The intermediate case is one positive observation of documented behavior, with
no open-abstract-intermediate control. Warnings are not hard errors without an appropriate build
policy. The direct descendant set is bounded; an open child still admits new
indirect descendants. Matching that child handles its descendants too.
Use sealed leaves when extension is unwanted; closed intermediates are implicitly
abstract and cannot be instantiated. A fallback can intentionally hide new-case
warnings. Compiler protection requires consumer recompilation. Binary deployment
compatibility is a separate concern not exercised here. Generic constraints,
inaccessible descendants, multiple modules, other languages and arbitrary IL
were not tested. No serializer, throughput, allocation or security-boundary claim.

Initial sandbox attempts encountered SDK first-use access/Service Control Manager
restrictions and a Windows Application Control DLL block. Complete observations
above were obtained in normal-user context. First-use certificate/PATH/workload
notices do not establish language behavior; compiler CS diagnostics and semantic
assertions do. Raw machine dumps and local troubleshooting logs are not included.

## Primary references

- [C# 15 availability](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15)
- [closed declaration rules](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/closed)
- [Closed hierarchy patterns and nullable input](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/patterns#closed-hierarchy-patterns)
- [Feature design specification and versioning](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-15.0/closed-hierarchies)

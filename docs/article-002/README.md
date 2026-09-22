# Runtime Async results

This dataset contains two separate comparisons: conventional application
async across three runtime versions (Family A), and application Runtime Async OFF/ON
on the same .NET 11 RC1 runtime (Family B). It makes no general performance or
release-readiness claim.

[Implementation and exact reproduction commands](../../benchmarks/article-002/README.md)
include five semantic/stack checks and five timing configurations. The selected
study has two independent process passes, ten configuration runs and sixty rows
(six workloads per configuration per pass). Pass two reverses configuration order.

## Recorded environment

| Setting | Value |
| --- | --- |
| SDK / language | 11.0.100-rc.1.26425.128 / C# 12 |
| Runtimes | 8.0.31; 10.0.12; 11.0.0-rc.1.26425.128 |
| BenchmarkDotNet | 0.16.0-preview.1 |
| Toolchain | Normal generated benchmark child processes; `ARTICLE002_INPROCESS=0` |
| GC | Workstation, concurrent |
| JIT settings | Tiered compilation and tiered PGO enabled |
| Architecture / debugger | x64 / absent |
| Roll-forward | Disabled |
| Machine | AMD Ryzen 9 9950X3D; 64 GB installed RAM (61.56 GiB reported usable) |
| OS / power plan | Windows 11 25H2 build 26200.9457 / Balanced |
| Timing | 5 warmups; 12 target iterations; 250 ms target iteration time; adaptive pilot |
| Affinity / thermal telemetry | Not pinned / not measured |
| Measured implementation commit | `74be9cdf1739f05b5c5015a5e800be5dfe8bb3ef` |

The compiler feature is explicitly `runtime-async=off` for Family A and Family B
OFF, and `runtime-async=on` for Family B ON. Activation is checked by method metadata.
Both families link identical workload source. Family A includes runtime, JIT and
library differences; application OFF does not imply that .NET 11 libraries were
built with the feature off. Family B is limited to this application transformation
comparison, on this runtime and these workloads.

Semantic checks ran on identically sourced outer project binaries, not the exact
generated child binaries used for timing. Children separately check runtime and
method metadata during setup. Child tiering settings come from the recorded
`DOTNET_TieredCompilation` and `DOTNET_TieredPGO` environment variables; the generated
child runtimeconfig files do not independently establish those values.

## Results

[all-values.csv](all-values.csv) contains all 60 configuration/pass/workload rows without display rounding: mean, error, standard deviation, median, retained N, bytes per operation and GC collections per 1,000 operations. `a8`, `a10` and `a11` are Family A's conventional application configurations; `b-off` and `b-on` are Family B's .NET 11 RC1 compiler-feature configurations. Times are nanoseconds per complete measured operation. Passes and families remain separate.

Each retained timing sample is a BenchmarkDotNet Workload/Result iteration's
nanoseconds divided by operations. Error is its 99.9% Student-t confidence margin
for retained iteration samples. It is not request latency, a prediction interval,
or a replacement for independent process repetition. N is the actual retained
sample count. For local exports, use Workload/Result samples when calculating comparable statistics. GC columns report collections
per 1,000 operations, not per operation. No request P95 or P99 is estimated.

Any SMALL/INCONCLUSIVE classification accompanying these data is an engineering
interpretation of effect size and sign reversals between process passes. It is
not a statistical significance classification.

## Measurement boundaries

Synthetic authorization results depend on runtime inputs and include an operation
count and correlation value. Controlled suspension is established by an incomplete
task before releasing a newly allocated gate. Continuations run inline on the
releasing thread and must finish before gate release returns. Allocation, release
and consumption are included. This does not model I/O, thread-pool scheduling or
production latency.

The active/absent correlation comparison includes AsyncLocal set/restore and the
suspended chain; it cannot isolate a universal ExecutionContext overhead. Task
results use a custom struct, so the synchronous Task/ValueTask comparison is not
a cached small-integer result test. Diagnostic live stacks and separately caught
exception stacks run outside benchmarks; frame counts do not measure CPU cost or
debugger quality. NoInlining is limited to diagnostic methods.

These runs cover framework-dependent JIT execution. They do not test ASP.NET Core
request throughput, NativeAOT, ReadyToRun, async iterators, custom builders or debugger
integration. RC1 measurements do not establish .NET 11 GA behavior. Before/after
background-process CPU samples cannot rule out interference during timing; no
thermal telemetry or fixed processor affinity was collected.

## Figure

[Runtime Async timing change](figures/family-b-time-change.png) compares ON with OFF for each process pass. Negative values indicate lower time; use the CSV for absolute values and full precision.

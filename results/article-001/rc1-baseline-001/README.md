# Runtime baseline results

Measured on 20 September 2026 from source commit `03232fb0cf7e5dc68a83a3a4ca0b06216f1ddf9f`. The .NET 11 version is **11.0.0-rc.1.26425.128**, a prerelease build. [Code and reproduction commands](../../../benchmarks/article-001/README.md).

## Environment and units

| Setting | Value |
| --- | --- |
| CPU / memory | AMD Ryzen 9 9950X3D, 16 cores / 32 threads; 64 GB installed RAM |
| OS / architecture | Windows 11 25H2 build 26200.9457 / x64 |
| SDK / language | 11.0.100-rc.1.26425.128 / C# 12 |
| Runtimes | 8.0.31; 10.0.12; 11.0.0-rc.1.26425.128 |
| BenchmarkDotNet | 0.16.0-preview.1; adaptive pilot, warmup and measurement defaults |
| Build / GC / JIT | Release; Workstation concurrent GC; tiered compilation and tiered PGO enabled |
| Host controls | Existing power plan retained; no debugger, fixed affinity or thermal telemetry |

[summary.csv](summary.csv) contains all 36 primary/replication rows without display rounding. Each invocation measures one complete batch: time is **ns/batch**, allocation is **B/batch**, and GC rates are collections per 1,000 batches. Error is the original BDN confidence-interval margin; `n` is the retained Workload/Result iteration count. These are within-process statistics, not request percentiles or independent launches.

## Primary pass1

The columns below are mean ns/batch. Allocations are identical across all three runtimes for each method in this pass.

| Workload | .NET 8.0.31 | .NET 10.0.12 | .NET 11 RC1 | B/batch |
|---|---:|---:|---:|---:|
| ParseParcelScans | 3815.269 | 3701.429 | 3602.758 | 0 |
| ResolveInventory | 11524.439 | 6069.130 | 5952.336 | 0 |
| EvaluatePaymentRisk | 809.176 | 1037.322 | 1037.339 | 0 |
| AggregateWindTelemetry | 1191.609 | 1147.756 | 1152.443 | 0 |
| TransformCheckpoints | 861.270 | 827.775 | 841.469 | 10,264 |
| SortScanSequence | 7533.190 | 7318.236 | 6967.554 | 8,216 |

## Replication pass2

The columns below are mean ns/batch. Allocations are identical across all three runtimes for each method in this pass.

| Workload | .NET 8.0.31 | .NET 10.0.12 | .NET 11 RC1 | B/batch |
|---|---:|---:|---:|---:|
| ParseParcelScans | 3826.018 | 3690.071 | 3572.667 | 0 |
| ResolveInventory | 11537.502 | 6042.124 | 5951.820 | 0 |
| EvaluatePaymentRisk | 807.057 | 1039.155 | 710.042 | 0 |
| AggregateWindTelemetry | 1224.194 | 1147.570 | 1148.534 | 0 |
| TransformCheckpoints | 867.276 | 831.793 | 836.248 | 10,264 |
| SortScanSequence | 7503.616 | 7138.703 | 6951.517 | 8,216 |

Pass1 ran .NET 8, .NET 10, .NET 11; pass2 reversed that order. These passes are never pooled. For a comparison within one pass, ratio = target mean / baseline mean and time change% = `(ratio - 1) * 100`. A negative time change is not the same percentage as the reciprocal throughput gain.

## Supplemental results and generated code

[supplemental.csv](supplemental.csv) contains 18 per-launch summaries for dictionary lookup and payment evaluation: three launches per runtime and workload. These supplement the two main passes and are not pooled into them. Values come from Workload/Result samples grouped by launch, with sample standard deviation (N-1).

**Payment comparisons are inconclusive for both .NET 10 and .NET 11 RC1 versus .NET 8.** Newer-runtime supplemental launches occupy faster and slower timing bands. Selecting the fastest launch or combining the discrepancy into one mean would conceal this variability.

Dictionary lookup remains in a lower time band on both newer runtimes in the two main passes. [Selected disassembly excerpts](dictionary-disassembly.md) show a separate FindValue call with indirect comparer calls on .NET 8 and an OrdinalComparer guard with hash arithmetic on the newer runtimes. This is a generated-code observation, not a single-change causal attribution or universal Dictionary.TryGetValue speedup.

## Limitations

- The first .NET 11 main pass had concurrent background CPU/file-I/O activity with unmeasured impact; the reverse-order second pass remains separate replication.
- All .NET 8 supplemental rows are marked `potential_interference` because another process overlapped that run and per-launch impact is unknown. Do not use those rows as a clean baseline, to infer .NET 8 stability, or to quantify a diagnostic cross-runtime benefit. The two main .NET 8 passes are unaffected by that overlap.
- Background applications were present. Short CPU spot samples cannot rule out interference; no fixed affinity, thermal telemetry or hardware branch counters were collected.
- One disassembly listing per workload/helper does not identify the code executed in every measured launch or explain the payment timing bands.
- These warm, single-threaded batches use Workstation GC and fixed synthetic distributions. They do not measure endpoint latency, tail latency, production throughput or Server GC behavior. RC1 results do not establish GA performance.
- TransformCheckpoints allocates 10,264 B/batch and returns an escaping object graph. SortScanSequence allocates 8,216 B/batch and includes cloning plus sorting, with no timed second traversal. The scalar control shows no material .NET 11 versus .NET 10 gain.

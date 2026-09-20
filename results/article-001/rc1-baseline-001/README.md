# Article 001: RC1 baseline results

Measured on 20 September 2026, using source commit `03232fb0cf7e5dc68a83a3a4ca0b06216f1ddf9f`. .NET 11 is **11.0.0-rc.1.26425.128**, a prerelease build; these observations do not establish GA performance. [Reproduction commands and workload boundaries](../../../benchmarks/article-001/README.md) describe the exact implementation.

## Environment and measurement

| Setting | Recorded value |
|---|---|
| CPU | AMD Ryzen 9 9950X3D; 1 processor, 16 physical cores, 32 logical cores |
| OS | Windows 11 25H2, build 10.0.26200.9457 |
| Architecture / JIT | x64 / RyuJIT; no debugger attached |
| SDK / language | 11.0.100-rc.1.26425.128 / C# 12.0 |
| Runtimes | 8.0.31; 10.0.12; 11.0.0-rc.1.26425.128 |
| BenchmarkDotNet | 0.16.0-preview.1, pinned prerelease dependency |
| Build / GC | Release; Workstation GC, concurrent enabled |
| Runtime settings | Tiered compilation and TieredPGO enabled; exact runtime selection; roll-forward disabled |
| Host controls | Existing power plan retained; no affinity pinning; thermal state unobserved |

Each invocation measures one complete synthetic batch. Means are **ns/batch**, allocations are managed **B/batch**. Batch sizes range from 256 to 4,096 elements; compare a method only with itself across runtimes. Setup is excluded; cloning, sorting and the returned array are included in SortScanSequence, with no timed second traversal.

[summary.json](summary.json) and [summary.csv](summary.csv) retain mean, original BDN error margin, standard deviation, median, retained N, allocation and GC rates. Error is BDN `ConfidenceInterval.Margin`; N counts retained warm Workload/Result iterations within a process, not independent launches or business requests. GC rates are collections per 1,000 batches, not pause durations. Within-launch precision does not bound variation between fresh processes.

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

## Supplemental launches and generated code

The two original .NET 11 payment means disagree materially. The subsequent diagnostic selection comprises two methods and three fresh process launches per runtime. [diagnostics/summary.json](diagnostics/summary.json) preserves launch indices, retained samples, N, mean, sample standard deviation, median, minimum and maximum. The original diagnostic full JSON preserves BDN's pooled statistics, but the table below uses separate launch means.

| Method | Runtime | Launch 1 ns/batch | Launch 2 ns/batch | Launch 3 ns/batch |
|---|---|---:|---:|---:|
| ResolveInventory | 8.0.31 † | 11941.36 | 11969.93 | 12127.58 |
| ResolveInventory | 10.0.12 | 6201.99 | 6185.84 | 6176.40 |
| ResolveInventory | 11 RC1 | 6077.40 | 6124.52 | 6177.13 |
| EvaluatePaymentRisk | 8.0.31 † | 809.68 | 848.00 | 819.07 |
| EvaluatePaymentRisk | 10.0.12 | 771.22 | 770.98 | 1061.02 |
| EvaluatePaymentRisk | 11 RC1 | 1061.41 | 730.53 | 1061.35 |

† All .NET 8 supplemental launch rows are potentially affected by an overlapping runtime-selection test process. The affected workload or launch cannot be identified. Preserve these values, but do not use them as a clean baseline, as evidence of .NET 8 stability/instability, or to quantify a diagnostic cross-runtime benefit. See the measurement-hygiene details below.

**Payment comparisons are INCONCLUSIVE for both .NET 10 and .NET 11 RC1 versus .NET 8.** The newer runtimes show faster and slower launch bands. Two similar .NET 10 suite means do not establish a robust regression when subsequent launches reverse the ranking. Selecting the fastest launch or pooling the discrepancy into one mean would hide this uncertainty.

Dictionary lookup stays in a lower time band on .NET 10 and .NET 11 across the two suites and these selected launches. In the captured [.NET 8 listing](diagnostics/raw/8.0.31.asm.md), ResolveInventory calls a separate FindValue with indirect comparer calls. The [.NET 10](diagnostics/raw/10.0.12.asm.md) and [.NET 11](diagnostics/raw/11.0.0-rc.1.26425.128.asm.md) listings contain an OrdinalComparer guard, string-hash arithmetic and dictionary traversal; fallback comparer calls remain. This is a generated-code observation alongside the measurements, without a single-PR causal attribution or a general Dictionary.TryGetValue speedup claim.

The artifact has one listing per workload/helper rather than a matched listing for every measured launch. Payment block-order and instruction differences cannot identify the cause of its timing bands. Three launches, no hardware branch counters and no controlled feature ablation leave scheduling, tiering, code placement, cache state and branch prediction unresolved.

The scalar control establishes no material .NET 11 versus .NET 10 gain. Object transformation allocates the same 10,264 B/batch everywhere; its returned object graph escapes. Sorting allocates the same 8,216 B/batch and measures clone plus sort, so its timings are not isolated Array.Sort timings. These warm, single-threaded synthetic batches do not measure endpoint latency, tail latency, production throughput or Server GC behavior.

## Files and SHA-256 verification

Measurement hygiene limitation: file/hash collection for the completed .NET 8 and .NET 10 builds overlapped the first .NET 11 suite run. Its CPU and file-I/O impact was not measured. The reverse-order second suite did not overlap those captures and remains separate replication; the primary table was not replaced. Short before/after CPU snapshots do not establish a noise-free workstation. No CPU affinity, thermal readings or hardware branch counters were recorded. Interpret small changes with those limits, and retain both payment timing bands.

A failed runtime-selection test process also overlapped the .NET 8 supplemental diagnostic job. Precise per-launch overlap could not be established from the retained timestamps; conservatively treat all .NET 8 diagnostic launch means and spreads as potentially affected, not evidence of .NET 8 stability or instability. The two main suite passes are unaffected by this probe. Background applications remained open; process inventories were capped at 15 entries and CPU deltas covered only three-second spot samples.

- [provenance.json](provenance.json) binds the measured source commit, six original suite JSON reports, three diagnostic JSON reports, three disassembly files, and derived summaries.
- `raw/pass1-<runtime>.json` is primary evidence; `raw/pass2-<runtime>.json` is replication evidence. Diagnostic files under `diagnostics/raw` are supplemental only.
- Original BDN full JSON and disassembly are copied byte-for-byte. Public summaries contain derived statistics and references to these files. The provenance inventory covers the exported data; this README and separately supplied figures are outside that inventory.

Run from this result directory in PowerShell. The first check verifies provenance itself against the digest recorded below; the loop then verifies every data file it lists. SHA-256 checks integrity against this record, not independent authenticity of the repository.

```powershell
$expectedProvenance = '5a870d275a048d024173bafaaf5aabef3409473b16f80d2382a8c3b0d2eb9ff2'
if ((Get-FileHash -LiteralPath ./provenance.json -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expectedProvenance) {
    throw 'Provenance SHA-256 mismatch'
}
$manifest = Get-Content -LiteralPath ./provenance.json -Raw | ConvertFrom-Json
foreach ($entry in $manifest.files) {
    $candidate = Join-Path -Path (Get-Location).Path -ChildPath $entry.path
    $actual = (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $entry.sha256 -or (Get-Item -LiteralPath $candidate).Length -ne $entry.bytes) {
        throw "Result integrity mismatch: $($entry.path)"
    }
}
'All inventoried result files match their SHA-256 and byte lengths.'
```

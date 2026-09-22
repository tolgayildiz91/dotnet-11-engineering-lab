# .NET 11 Engineering Lab

Reproducible C# experiments comparing runtime behavior, execution time and managed allocation. Each experiment includes source, semantic tests, pinned dependencies, reproduction commands and a concise measured dataset.

## Experiments

| Experiment | Question | Reproduce | Results |
| --- | --- | --- | --- |
| Runtime baseline | How do six synthetic batch workloads compare across .NET 8, .NET 10 and .NET 11 RC1? | [Code and commands](benchmarks/article-001/README.md) | [Measurements and limitations](results/article-001/rc1-baseline-001/README.md) |
| Runtime Async | How do conventional async implementations compare across runtimes, and what changes with application Runtime Async OFF/ON on .NET 11 RC1? | [Code and commands](benchmarks/article-002/README.md) | [Measurements and methodology](docs/article-002/README.md) |

## Requirements

The recorded experiments use Windows x64, SDK **11.0.100-rc.1.26425.128**, runtimes **8.0.31**, **10.0.12** and **11.0.0-rc.1.26425.128**, C# 12 and BenchmarkDotNet **0.16.0-preview.1**. Install the exact versions and run each experiment from its benchmark directory so its `global.json` applies. Both SDK and runtime roll-forward are disabled.

Start with the linked restore, build and semantic-test commands, then run the benchmark passes serially in Release without a debugger. The workloads generate deterministic synthetic inputs; no database or external service is required.

Results keep independent process passes separate and include absolute time, variability and allocation. RC1 measurements do not establish GA performance or production throughput. See [measurement methodology](docs/methodology.md) and each experiment's limitations before interpreting differences.

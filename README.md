# .NET 11 Engineering Lab

Reproducible backend experiments in C#, .NET, ASP.NET Core, EF Core and SQL Server, with a focus on performance, concurrency and observability.

The comparison baseline is .NET 8, .NET 10 and the exact .NET 11 prerelease build used by each experiment. Experiments record toolchain versions and workload configuration so results can be assessed in context.

## Repository layout

| Directory | Purpose |
| --- | --- |
| `src` | Executable sample applications and tests |
| `benchmarks` | Benchmark projects and workload configurations |
| `experiments` | Reproduction instructions and experiment definitions |
| `results` | Versioned raw results and derived tables |
| `sql` | Synthetic schemas, datasets and query evidence |
| `docs` | Measurement methods and technical notes |

## Research standards

- Separate documented behavior, local measurements and engineering interpretation.
- Preserve absolute measurements and raw data, including null results and regressions.
- Compare equivalent workloads and disclose runtime, compiler, database and configuration differences.
- Use independently designed fictional domains and synthetic datasets.
- Preserve prerelease evidence when repeating experiments against General Availability builds.

Article 001 now includes a six-workload .NET 8 / .NET 10 / .NET 11 RC1 baseline: [implementation and reproduction](benchmarks/article-001/README.md), [results and provenance](results/article-001/rc1-baseline-001/README.md). Primary and replication passes remain separate, with additional process-launch and disassembly evidence for dictionary lookup and payment-risk evaluation. The .NET 11 results are prerelease measurements; they do not establish GA performance.

See [measurement methodology](docs/methodology.md) and [result provenance](docs/result-provenance.md).

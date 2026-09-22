# Measurement methodology

Build in Release with the experiment's exact SDK, runtime, language and package versions. Run semantic tests before timing. Use x64, Workstation concurrent GC and the specified tiered compilation/PGO settings, with runtime roll-forward disabled and no debugger attached.

Run configurations serially on an otherwise quiet machine. Use a fresh output directory for each execution. Keep the power plan consistent and record CPU, OS, memory, architecture and any uncontrolled interference. The recorded machine used an AMD Ryzen 9 9950X3D, 64 GB installed RAM and Windows 11 25H2 build 26200.9457. Experiment result guides document timing settings and limitations.

Compare the same workload and measured boundary across configurations. A baseline invocation measures a complete batch; an async invocation measures a complete operation. Setup is excluded unless the workload description says otherwise. Returned values and allocated outputs remain observable.

Keep process passes separate. Compute time change as `(target_mean / baseline_mean - 1) * 100`; a negative value means lower time. A time reduction is not the same percentage as reciprocal throughput gain. Retained iteration count and within-process uncertainty do not quantify variation between fresh processes.

The CSV files contain full-precision summary values, units, variability, retained sample counts and allocations. Generate local BenchmarkDotNet exports with the reproduction commands when inspecting individual iterations. Generated output stays in the ignored `BenchmarkDotNet.Artifacts` directories.

Report null results, reversals and limitations alongside improvements. These warm synthetic workloads do not establish request latency, tail latency, service throughput or behavior under production load. Runtime comparisons include JIT and library differences; a shared compiler does not isolate their individual contributions.

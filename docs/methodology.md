# Measurement methodology

Each experiment should state its technical question, executable reproduction steps, workload, exact SDK/runtime/package versions, operating system, CPU, memory, architecture, GC mode, build configuration and power plan.

Use Release builds without a debugger. Control warmup and repeated measurement. Inspect dead-code elimination, constant folding, tiered compilation, database cache effects, network limits and load-generator capacity before interpreting differences. Record observable thermal or background workload interference.

Publish raw observations and absolute units alongside calculated percentages. Explain uncertainty, limitations and cases where the finding does not generalize. Rerun suspicious results; preserve the original run and explain why a rerun was needed.

Official documentation is evidence for documented behavior. Measurements establish only what the stated workload observed. Interpretations must identify their assumptions. A documented improvement that is not reproduced locally remains an honest null result.

SQL examples use synthetic project databases and Windows Authentication. Database scripts must verify the intended target before changing data or schema.

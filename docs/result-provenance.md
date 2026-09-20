# Result provenance

Store each run in a new directory identified by experiment, release phase and run ID. Never overwrite prerelease data with GA data.

A result bundle should include raw output, exact invocation, tool versions, workload configuration, timestamps, outcome and SHA-256 hashes. Derived tables and charts should identify their source files and transformation. Percentages must be reproducible from the retained absolute measurements.

Screenshots must be actual browser renders of retained reports or captures of real local output. Remove user profile paths, host identifiers, credentials and unrelated machine details before contributing public artifacts. Keep hardware and software details necessary to interpret the experiment.

No experimental data exists in this initial repository scaffold.

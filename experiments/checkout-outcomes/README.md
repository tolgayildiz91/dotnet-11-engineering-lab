# Checkout outcomes with Result and C# unions

A compact synthetic checkout experiment comparing an explicit Result design with C#15 native union declarations. It tests case coverage, adding a case, null/default behavior, HTTP response mapping, JSON and generated storage. It is not a throughput or latency benchmark.

## Toolchain

- PowerShell7 required (`pwsh`).
- SDK11.0.100-rc.3.26522.148, roll-forward disabled in global.json.
- Baseline: net8.0, configured C#12, runtime8.0.33; net10.0, configured C#14, runtime10.0.14.
- Native: net11.0, configured C#15, runtime11.0.0-rc.3.26522.148.
- Install both Microsoft.NETCore.App and Microsoft.AspNetCore.App at the pinned versions.

All targets use the common RC3 SDK/Roslyn5.12 compiler. This is not a comparison of historical compiler binaries. Union declarations work without preview language mode in this SDK. No third-party packages are used.

## Run

From this directory:

```powershell
pwsh -NoProfile -File ./verify.ps1
```

The script sets a local CLI home and telemetry opt-out, builds Release without incremental/shared compilation, writes timestamped local evidence, and records source/project SHA256 inventories before and after the run. Expected compiler failures are checked separately from unexpected failures. Exit0 means all expected checks ran; exit1 means failed checks; exit2 means no failed checks but an OS-blocked launch. Generated outputs are ignored by Git.

For the applications alone, after building:

```powershell
dotnet build Baseline/Baseline.csproj -c Release -p:UseSharedCompilation=false
dotnet build Native/Native.csproj -c Release -p:UseSharedCompilation=false
dotnet Baseline/bin/Release/net8.0/Baseline.dll --self-test
dotnet Baseline/bin/Release/net10.0/Baseline.dll --self-test
dotnet Native/bin/Release/net11.0/Native.dll --self-test
```

The self-tests start Kestrel on a loopback ephemeral port and send four real POST requests per target. Both implementations check201/400/409/402, creation Location, JSON content type and identical bodies. To run an interactive local server, omit `--self-test`; the endpoints are `/checkout/result` and `/checkout/union` respectively. Default server mode is provided for exploration; the recorded HTTP checks used the self-test host.

## Method and limits

Shared deterministic rules check positive quantity, stock5 and a synthetic decline flag. Unit price is12.50. No randomness, database, real payment processing or production schema is involved.

The Result baseline has guarded construction, one-branch Match and short-circuiting Bind. Its error hierarchy is intentionally open. This does not characterize every Result implementation or third-party Result library. The native implementation lists four specific case types and maps them explicitly to the same HTTP result types. Neither representation chooses status codes automatically. OpenAPI generation and load behavior were not tested.

Compiler fixtures share unchanged consumer source where appropriate. Case evolution is checked by recompilation, not by deploying different library versions. A fallback hides future cases from exhaustiveness warnings. Default union contents can be null even with nonnullable case types.

The primitive allocation count tests100000 conversions into escaping static fields after warmup. It measures construction allocation only, not time, matching allocation or checkout allocation. The custom union's Value getter may box. See [concise observations](RESULTS.md) for the recorded result and host limitations.

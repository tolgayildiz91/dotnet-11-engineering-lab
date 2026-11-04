# ASP.NET Core loopback load on .NET 8, 10 and 11 RC3

## Reproduce

Use Windows x64, PowerShell 7, the exact SDK in `global.json`, and both shared frameworks at `8.0.33`, `10.0.14`, and `11.0.0-rc.3.26522.148`. The build uses the corresponding targeting packs. Use [k6 v2.3.0](https://github.com/grafana/k6/releases/tag/v2.3.0). The Windows x64 ZIP's SHA-256 is `112276d495e5741c968e2bc09ea6196099c1275bd6db9ee0875d173c7148ce43`.

From this directory, with loopback port 33044 available:

```powershell
pwsh -NoProfile -File .\run.ps1 -K6 C:\Tools\k6.exe -OutputDirectory C:\Temp\aspnet-load
```

Choose a new output directory. Add `-ParityOnly` to build and verify the three exact HTTP contracts without a timed run.

## Method

The host was an AMD Ryzen 9 9950X3D with 64 GB of installed RAM, running Windows 11 Pro x64 (build 26300) on the Balanced power plan. k6 v2.3.0 and Kestrel shared the host. Traffic went to `127.0.0.1` over cleartext HTTP/1.1 with connection reuse.

I built one C# 12 source with SDK `11.0.100-rc.3.26522.148` for all three targets. Each build was framework-dependent Release with Server GC. The startup record confirmed both loaded shared frameworks for every trial. Before measuring, I checked the exact status, protocol, media type, and body on all three routes across all targets.

- **JSON** — `GET /api/status/42` creates a small typed status response.
- **Lookup** — `GET /api/products/42` reads a prebuilt product from a 1,024-entry in-memory dictionary and serializes it. The same key is used throughout, so this is a hot-key test.
- **Async** — `GET /api/orders/42` awaits `Task.Yield()` and then creates a typed order response. This exercises a scheduler continuation without simulating a database or network delay.

Each trial started a fresh server process. The same 32 k6 virtual users ran for 23 seconds, with one in-flight request per user. The first eight seconds warmed the endpoint and connections. Measurements include requests started in the following 15 seconds, including requests that finished during the grace period. I divided the count of those completions by 15 seconds. Runtime order rotated so each version occupied each runtime position once per workload. The workload order also rotated across repetitions.

The latency columns are medians of three per-trial k6 percentiles. They are client-observed HTTP request durations, covering sending, waiting, and receiving. Connection setup and time blocked before sending are outside this metric. The table ranges show the lowest and highest of three trials. With three samples, these ranges describe repeatability on this host.

## Results

| Workload | Runtime | Req/s median [min–max] | p50 ms | p95 ms | p99 ms median [min–max] | Failures |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| JSON | .NET 8 | 12,873 [11,957–13,643] | 2.292 | 3.419 | 3.985 [3.787–4.371] | 0 |
| JSON | .NET 10 | 13,440 [12,014–14,107] | 2.202 | 3.279 | 3.801 [3.655–4.382] | 0 |
| JSON | .NET 11 RC3 | 12,883 [12,646–14,321] | 2.288 | 3.402 | 3.955 [3.495–4.093] | 0 |
| Lookup | .NET 8 | 13,635 [13,607–13,687] | 2.199 | 3.260 | 3.781 [3.753–3.795] | 0 |
| Lookup | .NET 10 | 13,341 [13,185–13,768] | 2.234 | 3.310 | 3.836 [3.747–3.889] | 0 |
| Lookup | .NET 11 RC3 | 13,327 [13,247–13,814] | 2.228 | 3.318 | 3.848 [3.754–3.857] | 0 |
| Async | .NET 8 | 13,758 [13,032–13,936] | 2.188 | 3.242 | 3.754 [3.695–3.889] | 0 |
| Async | .NET 10 | 13,761 [12,601–13,811] | 2.187 | 3.243 | 3.751 [3.737–4.179] | 0 |
| Async | .NET 11 RC3 | 13,877 [12,758–14,162] | 2.180 | 3.230 | 3.737 [3.655–3.999] | 0 |

All 27 trials completed with zero failed requests. Every throughput and latency pair had overlapping run ranges. RC3's JSON median was 4.1% lower than .NET 10's, while the async median was 0.8% higher. These comparisons were inconclusive.

## Limits

This is a closed-loop, same-host test at one concurrency. It can show the client-observed behavior of these runtime and ASP.NET Core combinations under this load. A fixed-rate test, a separate client host, or a service with TLS, database calls, mixed routes, or production traffic may behave differently. A client waiting for a response cannot schedule extra arrivals, so these tails do not characterize queue growth under fixed offered load.

Microsoft documents runtime-async compilation in some ASP.NET Core 11 shared-framework libraries. This comparison changes the full runtime and web stack together and does not isolate that compiler feature. The app's async endpoint uses `Task.Yield()` in all three builds. [ASP.NET Core 11 release notes](https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-11#runtime-async-enabled-for-shared-framework-libraries)

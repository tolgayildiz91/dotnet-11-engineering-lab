#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$K6,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [switch]$ParityOnly
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$K6 = (Resolve-Path -LiteralPath $K6).Path
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Choose a new evidence directory.' }
[void][IO.Directory]::CreateDirectory($OutputDirectory)
$runtime = [ordered]@{
    'net8.0' = '8.0.33'
    'net10.0' = '10.0.14'
    'net11.0' = '11.0.0-rc.3.26522.148'
}
$targets = @($runtime.Keys)
$routes = [ordered]@{ json = 'status'; lookup = 'products'; async = 'orders' }
$bodies = @{
    json = '{"id":42,"status":"ok"}'
    lookup = '{"id":42,"sku":"SYN-0042","name":"Synthetic item 42","priceCents":1042,"currency":"USD","stock":5}'
    async = '{"id":42,"state":"ready","itemCount":3}'
}
$dotnet = (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
$baseUrl = 'http://127.0.0.1:33044'
$startedUtc = [DateTime]::UtcNow.ToString('o')
$previousNoProxy = $env:NO_PROXY
$env:NO_PROXY = '127.0.0.1,localhost'

function Start-Api([string]$Target, [string]$Evidence) {
    $dll = Join-Path $PSScriptRoot "bin/Release/$Target/LoadApi.dll"
    $process = Start-Process -FilePath $dotnet -ArgumentList ('"' + $dll + '"') -WindowStyle Hidden -PassThru -WorkingDirectory $PSScriptRoot -RedirectStandardOutput "$Evidence.server.txt" -RedirectStandardError "$Evidence.server-error.txt"
    return $process
}

function Assert-Api($Process, [string]$Target, [string]$Evidence) {
    $deadline = [DateTime]::UtcNow.AddSeconds(12)
    $response = $null
    do {
        if ($Process.HasExited) { throw "Server $Target exited $($Process.ExitCode). See $Evidence.server-error.txt." }
        try { $response = Invoke-WebRequest "$baseUrl/api/status/42" -NoProxy -TimeoutSec 2 }
        catch { Start-Sleep -Milliseconds 100 }
    } until ($response -or [DateTime]::UtcNow -ge $deadline)
    if (!$response) { throw "Server $Target never responded." }
    $attestation = Get-Content -LiteralPath "$Evidence.server.txt" -Raw | ConvertFrom-Json
    if ($attestation.core -cne $runtime[$Target] -or $attestation.aspnet -cne $runtime[$Target] -or !$attestation.serverGc) {
        throw "Unexpected runtime or GC mode for $Target."
    }
    $observations = foreach ($workload in $routes.Keys) {
        $response = Invoke-WebRequest "$baseUrl/api/$($routes[$workload])/42" -NoProxy -TimeoutSec 2
        $type = [string]($response.Headers['Content-Type'] -join ',')
        if ($response.StatusCode -ne 200 -or $response.BaseResponse.Version.ToString() -ne '1.1' -or
            $type -notmatch '^application/json(?:;|$)' -or $response.Content -cne $bodies[$workload]) {
            throw "Wire parity failed for $Target $workload."
        }
        [ordered]@{workload=$workload; status=$response.StatusCode; content_type=$type; body=$response.Content}
    }
    $observations | ConvertTo-Json | Set-Content -LiteralPath "$Evidence.parity.json"
}

function Stop-Api($Process) {
    if (!$Process.HasExited) {
        $Process.Kill()
        $Process.WaitForExit()
    }
    $Process.Dispose()
}

Push-Location -LiteralPath $PSScriptRoot
try {
    & $K6 version | Set-Content -LiteralPath "$OutputDirectory/k6-version.txt"
    if ($LASTEXITCODE) { throw 'k6 version failed.' }
    $inputs = foreach ($name in 'global.json','LoadApi.csproj','Program.cs','load.js','run.ps1') {
        @{file=$name; sha256=(Get-FileHash -LiteralPath $name -Algorithm SHA256).Hash}
    }
    $inputs | ConvertTo-Json | Set-Content -LiteralPath "$OutputDirectory/inputs.json"
    & $dotnet build LoadApi.csproj -c Release --nologo *> "$OutputDirectory/build.txt"
    if ($LASTEXITCODE) { throw "Build failed. See $OutputDirectory/build.txt." }

    foreach ($target in $targets) {
        $evidence = Join-Path $OutputDirectory "preflight-$target"
        $server = Start-Api $target $evidence
        try { Assert-Api $server $target $evidence }
        finally { Stop-Api $server }
    }
    if ($ParityOnly) { return }

    $index = 0
    $workloads = @($routes.Keys)
    for ($repeat = 0; $repeat -lt 3; $repeat++) {
        for ($slot = 0; $slot -lt 3; $slot++) {
            $workloadIndex = ($slot + $repeat) % 3
            $workload = $workloads[$workloadIndex]
            for ($position = 0; $position -lt 3; $position++) {
                $target = $targets[($position + $repeat + $workloadIndex) % 3]
                $index++
                $evidence = Join-Path $OutputDirectory ('{0:D2}-{1}-{2}-r{3}' -f $index,$workload,$target,($repeat+1))
                $server = Start-Api $target $evidence
                try {
                    Assert-Api $server $target $evidence
                    $server.Refresh()
                    $cpuStart = $server.TotalProcessorTime.TotalSeconds
                    $trialStart = [DateTime]::UtcNow
                    $summary = "$evidence.k6.json"
                    $k6Args = @('run','--quiet','--no-usage-report','--address','127.0.0.1:0','-e',"WORKLOAD=$workload",'-e',('"SUMMARY_PATH=' + $summary + '"'),'load.js')
                    $client = Start-Process -FilePath $K6 -ArgumentList $k6Args -WindowStyle Hidden -PassThru -WorkingDirectory $PSScriptRoot -RedirectStandardOutput "$evidence.k6.txt" -RedirectStandardError "$evidence.k6-error.txt"
                    try {
                        if (!$client.WaitForExit(45000)) {
                            $client.Kill()
                            $client.WaitForExit()
                            throw "k6 did not exit for $evidence."
                        }
                        $clientExit = $client.ExitCode
                        try { $clientCpu = $client.TotalProcessorTime.TotalSeconds }
                        catch { $clientCpu = $null }
                    }
                    finally { $client.Dispose() }
                    $server.Refresh()
                    if ($server.HasExited) { throw "Server exited during $evidence." }
                    $data = Get-Content -LiteralPath $summary -Raw | ConvertFrom-Json -AsHashtable
                    $metrics = $data.metrics
                    $count = $metrics.measured_completed.values.count
                    $attempted = $metrics.measured_started.values.count
                    $errors = $metrics.measured_failed.values.count
                    $latency = $metrics.measured_latency_ms.values
                    if (!$count -or $null -eq $errors -or $null -eq $latency['p(99)']) {
                        throw "Incomplete k6 summary for $evidence."
                    }
                    $row = [ordered]@{
                        index=$index; runtime=$runtime[$target]; workload=$workload; repetition=$repeat+1
                        started_utc=$trialStart.ToString('o'); wall_seconds=([DateTime]::UtcNow-$trialStart).TotalSeconds
                        completed=$count; attempted=$attempted; failed=$errors; k6_exit=$clientExit
                        requests_per_second=$count/15.0
                        p50_ms=$latency['p(50)']; p95_ms=$latency['p(95)']; p99_ms=$latency['p(99)']
                        client_cpu_seconds=$clientCpu; server_cpu_seconds=$server.TotalProcessorTime.TotalSeconds-$cpuStart
                    }
                    $row | ConvertTo-Json | Set-Content -LiteralPath "$evidence.result.json"
                    if ($attempted -ne $count -or $clientExit -ne 0 -or $errors -ne 0 -or $metrics.http_req_failed.values.rate -gt 0) {
                        throw "Invalid trial. See $evidence.result.json and k6 output."
                    }
                    '{0:D2}/27 {1} {2} {3:N0} req/s, p99 {4:N3} ms' -f $index,$workload,$target,$row.requests_per_second,$row.p99_ms
                }
                finally { Stop-Api $server }
            }
        }
    }
    $inputs | ForEach-Object {
        if ((Get-FileHash -LiteralPath $_.file -Algorithm SHA256).Hash -cne $_.sha256) {
            throw "Benchmark source changed during the matrix: $($_.file)."
        }
    }
    [ordered]@{ started_utc=$startedUtc; completed_utc=[DateTime]::UtcNow.ToString('o'); trial_count=$index } |
        ConvertTo-Json | Set-Content -LiteralPath "$OutputDirectory/completion.json"
}
finally {
    $env:NO_PROXY = $previousNoProxy
    Pop-Location
}

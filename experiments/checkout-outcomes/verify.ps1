# Builds apps and compiler fixtures (Release, non-incremental), runs them, and compares against the
# outcomes first discovered on 2026-11-02 with SDK 11.0.100-rc.3.26522.148. Raw logs go to evidence/ (private).
$ErrorActionPreference = 'Continue' # native stderr (unhandled exceptions) must not abort Windows PowerShell 5.1
$lab = $PSScriptRoot
$evidence = Join-Path $lab (Join-Path 'evidence' ([DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffffffZ')))
New-Item -ItemType Directory -Force $evidence | Out-Null
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_HOME = Join-Path $lab '.cli-home'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
$env:MSBUILDDISABLENODEREUSE = '1'
$env:DOTNET_CLI_UI_LANGUAGE = 'en'
Set-Location $lab

function SourceInventory {
    @(Get-ChildItem $lab -Recurse -File | Where-Object {
        $_.FullName -notmatch '[\\/](bin|obj|evidence|\.cli-home)[\\/]' -and
        ($_.Extension -in '.cs','.csproj','.props','.targets' -or $_.Name -in 'global.json','verify.ps1')
    } | Sort-Object FullName | ForEach-Object {
        [ordered]@{ path = [IO.Path]::GetRelativePath($lab, $_.FullName).Replace('\','/'); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
    })
}
$beforeInventory = ConvertTo-Json -InputObject @(SourceInventory) -Depth 5
Set-Content (Join-Path $evidence 'source-inputs-before.json') $beforeInventory -Encoding utf8

$script:failures = 0
$script:blocked = 0
$summary = [System.Collections.Generic.List[string]]::new()
function Note([bool]$ok, [string]$text) {
    $line = $(if ($ok) { "ok      $text" } else { "FAIL    $text" })
    if (-not $ok) { $script:failures++ }
    $summary.Add($line); Write-Host $line
}

function Build([string]$project, [string]$name) {
    $out = & dotnet build $project -c Release -nologo --no-incremental -p:UseSharedCompilation=false 2>&1 | Out-String
    $code = $LASTEXITCODE
    Set-Content (Join-Path $evidence "$name.build.log") $out -Encoding utf8
    $diag = @([regex]::Matches($out, '\b(warning|error) (CS\d{4})\b') | ForEach-Object { "$($_.Groups[1].Value) $($_.Groups[2].Value)" } | Sort-Object -Unique)
    [pscustomobject]@{ Code = $code; Diag = ($diag -join ',') }
}

function Run([string]$dll, [string]$name, [string[]]$arguments) {
    $out = & dotnet $dll @arguments 2>&1 | Out-String
    $code = $LASTEXITCODE
    Set-Content (Join-Path $evidence "$name.run.log") "exit=$code`n$out" -Encoding utf8
    [pscustomobject]@{ Code = $code; Out = $out; Blocked = $out -match '0x800711C7' }
}

function CheckRun($run, [string]$name, $expectedCode, [string]$expectedText) {
    if ($run.Blocked) { $script:blocked++; $line = "BLOCKED $name run: Windows Application Control refused the built assembly (0x800711C7)"; $summary.Add($line); Write-Host $line; return }
    $codeOk = $(if ($expectedCode -eq 'nonzero') { $run.Code -ne 0 } else { $run.Code -eq $expectedCode })
    Note ($codeOk -and $run.Out.Contains($expectedText)) "$name run exit=$($run.Code) (expected $expectedCode) contains '$expectedText'"
}

# Applications.
foreach ($app in @(
        @{ Name = 'Baseline'; Project = 'Baseline/Baseline.csproj'; Dlls = @('Baseline/bin/Release/net8.0/Baseline.dll', 'Baseline/bin/Release/net10.0/Baseline.dll') },
        @{ Name = 'Native'; Project = 'Native/Native.csproj'; Dlls = @('Native/bin/Release/net11.0/Native.dll') })) {
    $b = Build $app.Project $app.Name
    Note ($b.Code -eq 0 -and $b.Diag -eq '') "$($app.Name) build exit=$($b.Code) diagnostics=[$($b.Diag)]"
    foreach ($dll in $app.Dlls) {
        $tfm = Split-Path (Split-Path $dll) -Leaf
        Add-Content (Join-Path $evidence "$($app.Name).build.log") "`n--- $tfm runtimeconfig ---`n$(Get-Content ($dll -replace '\.dll$', '.runtimeconfig.json') -Raw)"
        CheckRun (Run $dll "$($app.Name)-$tfm-self-test" @('--self-test')) "$($app.Name) $tfm --self-test" 0 'self-test failures=0'
    }
}

# Compiler fixtures: expected build exit code, exact CS diagnostic set, and (optionally) run outcome.
$fixtures = @(
    @{ Name = 'native-complete'; Build = 0; Diag = ''; Run = 0; Text = '402 synthetic_decline' },
    @{ Name = 'native-missing-payment'; Build = 0; Diag = 'warning CS8509'; Run = 'nonzero'; Text = 'SwitchExpressionException' },
    @{ Name = 'native-missing-payment-werror'; Build = 1; Diag = 'error CS8509' },
    @{ Name = 'native-fifth-case'; Build = 0; Diag = 'warning CS8509'; Run = 42; Text = 'FraudReviewRequired reached unchanged four-arm switch' },
    @{ Name = 'native-nullable-case'; Build = 0; Diag = 'warning CS8655'; Run = 0; Text = 'created' },
    @{ Name = 'native-default-runtime'; Build = 0; Diag = ''; Run = 42; Text = 'SwitchExpressionException' },
    @{ Name = 'baseline-complete'; Build = 0; Diag = ''; Run = 0; Text = '402 synthetic_decline' },
    @{ Name = 'baseline-no-discard'; Build = 0; Diag = 'warning CS8509'; Run = 0; Text = '402 synthetic_decline' },
    @{ Name = 'baseline-fifth-error'; Build = 0; Diag = ''; Run = 0; Text = '500 unmapped FraudReviewRequired' })
foreach ($f in $fixtures) {
    $n = $f.Name
    $b = Build "fixtures/$n/$n.csproj" "fixture-$n"
    Note ($b.Code -eq $f.Build -and $b.Diag -eq $f.Diag) "fixture $n build exit=$($b.Code) diagnostics=[$($b.Diag)] (expected exit=$($f.Build) [$($f.Diag)])"
    if ($b.Code -eq 0 -and $f.ContainsKey('Run')) {
        $dll = Get-ChildItem "fixtures/$n/bin/Release/*/$n.dll" | Select-Object -First 1
        CheckRun (Run $dll.FullName "fixture-$n" @()) "fixture $n" $f.Run $f.Text
    }
}

$afterInventory = ConvertTo-Json -InputObject @(SourceInventory) -Depth 5
Set-Content (Join-Path $evidence 'source-inputs-after.json') $afterInventory -Encoding utf8
Note ($beforeInventory -ceq $afterInventory) 'source/project hashes unchanged during this verification run'
$summary.Add("failures=$($script:failures) blocked=$($script:blocked)")
Write-Host "failures=$($script:failures) blocked=$($script:blocked)"
Set-Content (Join-Path $evidence 'verify-summary.txt') $summary -Encoding utf8
Write-Host "Evidence: $evidence"
# A partial execution is not a successful full run, even when all compiler checks pass.
exit $(if ($script:failures -ne 0) { 1 } elseif ($script:blocked -ne 0) { 2 } else { 0 })

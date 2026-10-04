# Compiler fixtures

Each `.cs` file is a standalone compilation unit. Compile one at a time;
duplicate type names are intentional. To check all warning waves, replace
`/warn:11` with `/warn:9999` without changing the source. The benchmark project excludes this
directory from its compilation.

The fixtures separate declared-case exhaustiveness, nullable input, unnamed
enum values, fallback behavior and implicit conversion.
`union-third-case-warning-as-error.cs` explicitly promotes CS8509 to an error.
`union-object-string-runtime.cs` verifies actual string/object pattern selection
after successful implicit conversions. The object arm reports the bound value's
type; on the pinned RC1 this broad pattern binds the union wrapper, rather than
proving extraction of the object payload. `union-runtime-default.cs`
also observes default and null-reference payloads in an exhaustive-looking union
switch. Compiler diagnostics are collected at warning level 11, matching the RC1
project command. An additional level-9999 run checks all compiler warning waves.
Earlier private level-4 observations remain historical evidence. Diagnostics
remain warnings unless the reproduction explicitly promotes them to errors. These source files are synthetic examples, separate
from the timed benchmark handlers.

Use SDK `11.0.100-rc.1.26425.128`, reference pack/runtime
`11.0.0-rc.1.26425.128`, nullable analysis enabled, and `langversion:preview`.
The direct compiler route avoids MSBuild/compiler-server named-pipe dependencies.
It does not exercise SDK analyzers or prove a regular project build succeeds.

Example PowerShell reproduction from this directory, writing outputs to `bin`
(choose a different `$fixture` for another test):

```powershell
$fixture = 'union-two-case'
$dotnet = (Get-Command dotnet).Source
$installation = Split-Path $dotnet -Parent
$compiler = Join-Path $installation 'sdk/11.0.100-rc.1.26425.128/Roslyn/bincore/csc.dll'
$referenceRoot = Join-Path $installation 'packs/Microsoft.NETCore.App.Ref/11.0.0-rc.1.26425.128/ref/net11.0'
$outputRoot = Join-Path $PWD 'bin'
[IO.Directory]::CreateDirectory($outputRoot) | Out-Null
$target = if ($fixture -in @('union-runtime-default', 'union-object-string-runtime')) { 'exe' } else { 'library' }
$output = Join-Path $outputRoot "$fixture.dll"
$responseFile = Join-Path $outputRoot "$fixture.rsp"
$lines = @('/nostdlib+', '/nologo', '/nullable:enable', '/langversion:preview',
    '/warn:11', '/warnaserror-', '/optimize+', '/debug-', '/deterministic+',
    "/target:$target", ('/out:"' + $output + '"'))
if ($fixture -eq 'union-third-case-warning-as-error') { $lines += '/warnaserror+:CS8509' }
$lines += Get-ChildItem $referenceRoot -Filter '*.dll' | Sort-Object Name |
    ForEach-Object { '/reference:"' + $_.FullName + '"' }
$lines += '"' + (Join-Path $PWD "$fixture.cs") + '"'
[IO.File]::WriteAllLines($responseFile, $lines)
& $dotnet $compiler /noconfig "@$responseFile"
```

For either runtime fixture, create the matching runtimeconfig before execution:

```powershell
$runtime = '11.0.0-rc.1.26425.128'
$runtimeConfig = @{
    runtimeOptions = @{
        tfm = 'net11.0'
        framework = @{ name = 'Microsoft.NETCore.App'; version = $runtime }
        rollForward = 'Disable'
    }
}
$runtimeConfig | ConvertTo-Json -Depth 6 |
    Set-Content ([IO.Path]::ChangeExtension($output, 'runtimeconfig.json')) -Encoding utf8
& $dotnet exec --fx-version $runtime --roll-forward Disable $output
```

The exact RC compiler behavior should be rechecked when testing another preview
or GA release. A no-warning compilation alone does not establish that default
or null-reference payloads are handled at runtime.

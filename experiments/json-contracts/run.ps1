#requires -Version 7.0
[CmdletBinding()]
param([string]$SourceDirectory="$PSScriptRoot",[Parameter(Mandatory)][string]$EvidenceDirectory)
$ErrorActionPreference='Stop'
$source=(Resolve-Path -LiteralPath $SourceDirectory).Path
$out=[IO.Path]::GetFullPath($EvidenceDirectory)
if(Test-Path -LiteralPath $out){throw 'Use a fresh evidence directory.'}
New-Item -ItemType Directory -Path $out | Out-Null
$records=[Collections.Generic.List[object]]::new()
function Run([string]$label,[string[]]$arguments,[string]$cwd){
 $start=[DateTime]::UtcNow.ToString('o')
 $p=[Diagnostics.ProcessStartInfo]::new('dotnet');$p.WorkingDirectory=$cwd;$p.UseShellExecute=$false;$p.CreateNoWindow=$true;$p.RedirectStandardOutput=$true;$p.RedirectStandardError=$true
 foreach($v in $arguments){$p.ArgumentList.Add($v)}
 $p.Environment['DOTNET_NOLOGO']='1';$p.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1';$p.Environment['DOTNET_SKIP_FIRST_TIME_EXPERIENCE']='1';$p.Environment['DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE']='true'
 $process=[Diagnostics.Process]::Start($p);$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
 if(-not $process.WaitForExit(90000)){$process.Kill($true);throw "Timed out: $label"}
 $text=$stdout.GetAwaiter().GetResult();$err=$stderr.GetAwaiter().GetResult()
 [IO.File]::WriteAllText((Join-Path $out "$label.stdout.txt"),$text);[IO.File]::WriteAllText((Join-Path $out "$label.stderr.txt"),$err)
 $records.Add(@{label=$label;executable='dotnet';arguments=$arguments;working_directory=$cwd;started_at=$start;exit_code=$process.ExitCode;stdout="$label.stdout.txt";stderr="$label.stderr.txt"})
 $records | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $out 'commands.json') -Encoding utf8
 Write-Host "$label exit=$($process.ExitCode)"
 return @{exit=$process.ExitCode;text=$text+$err}
}
$sdk='11.0.100-rc.3.26522.148'
foreach($case in @(
 @{name='core11';file='Core11.cs';tfm='net11.0';runtime='11.0.0-rc.3.26522.148';run=$true},
 @{name='portable8';file='Portable.cs';tfm='net8.0';runtime='8.0.33';run=$true},
 @{name='portable10';file='Portable.cs';tfm='net10.0';runtime='10.0.14';run=$true},
 @{name='portable11';file='Portable.cs';tfm='net11.0';runtime='11.0.0-rc.3.26522.148';run=$true},
 @{name='api8';file='ApiProbe.cs';tfm='net8.0';runtime='8.0.33';run=$false;negative='CS0117'},
 @{name='api10';file='ApiProbe.cs';tfm='net10.0';runtime='10.0.14';run=$false;negative='CS0117'},
 @{name='api11';file='ApiProbe.cs';tfm='net11.0';runtime='11.0.0-rc.3.26522.148';run=$false},
 @{name='open-generation';file='OpenGeneration.cs';tfm='net11.0';runtime='11.0.0-rc.3.26522.148';run=$false;negative='SYSLIB1243'}
)){
 $dir=Join-Path $out $case.name;New-Item -ItemType Directory -Path $dir | Out-Null
 @{sdk=@{version=$sdk;rollForward='disable';allowPrerelease=$true}}|ConvertTo-Json -Depth 4|Set-Content (Join-Path $dir 'global.json')
 '<configuration><packageSources><clear /></packageSources></configuration>'|Set-Content (Join-Path $dir 'NuGet.Config')
 Copy-Item (Join-Path $source $case.file) (Join-Path $dir $case.file)
 $outputType=if($case.run){'Exe'}else{'Library'}
 @"
<Project Sdk="Microsoft.NET.Sdk">
 <PropertyGroup>
  <TargetFramework>$($case.tfm)</TargetFramework><OutputType>$outputType</OutputType>
  <LangVersion>15.0</LangVersion><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings>
  <RuntimeFrameworkVersion>$($case.runtime)</RuntimeFrameworkVersion><RollForward>Disable</RollForward>
  <EnableNETAnalyzers>false</EnableNETAnalyzers><EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>obj/generated</CompilerGeneratedFilesOutputPath>
 </PropertyGroup>
</Project>
"@ | Set-Content (Join-Path $dir 'Fixture.csproj')
 $build=Run ($case.name+'-build') @('build','Fixture.csproj','-c','Release','--disable-build-servers','-m:1','-p:UseSharedCompilation=false','--nologo','-v:normal') $dir
 if($case.ContainsKey('negative')){if($build.exit -eq 0 -or $build.text -notmatch $case.negative){throw "Expected negative diagnostic absent: $($case.name)"}}
 elseif($build.exit -ne 0){throw "Build failed: $($case.name)"}
 if($case.run){$run=Run ($case.name+'-run') @('exec','--fx-version',$case.runtime,(Join-Path $dir "bin/Release/$($case.tfm)/Fixture.dll")) $dir;if($run.exit -ne 0){throw "Assertions failed: $($case.name)"}}
}
$null=Run 'sdk-version' @('--version') (Join-Path $out 'core11')
$null=Run 'compiler-version' @('exec',"C:/Program Files/dotnet/sdk/$sdk/Roslyn/bincore/csc.dll",'-version') (Join-Path $out 'core11')
Write-Host 'All expected build and runtime exits verified.'

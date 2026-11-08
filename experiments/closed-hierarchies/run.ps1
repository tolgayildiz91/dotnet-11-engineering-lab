#requires -Version 7.0
param([Parameter(Mandatory)][string]$SourceDirectory, [Parameter(Mandatory)][string]$EvidenceDirectory)
$ErrorActionPreference='Stop'
$source=[IO.Path]::GetFullPath($SourceDirectory)
$evidence=[IO.Path]::GetFullPath($EvidenceDirectory)
if(Test-Path $evidence){throw 'Use a new evidence directory to preserve previous runs.'}
New-Item -ItemType Directory -Path $evidence | Out-Null
$rows=[Collections.Generic.List[object]]::new()
function Execute([string]$name,[string]$working,[string[]]$arguments){
 $p=[Diagnostics.ProcessStartInfo]::new('dotnet');$p.WorkingDirectory=$working;$p.UseShellExecute=$false
 $p.RedirectStandardOutput=$true;$p.RedirectStandardError=$true
 $p.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1';$p.Environment['DOTNET_SKIP_FIRST_TIME_EXPERIENCE']='1'
 $p.Environment['DOTNET_CLI_HOME']=(Join-Path $evidence '.cli')
 $p.Environment['DOTNET_CLI_UI_LANGUAGE']='en-US';$p.Environment['VSLANG']='1033'
 foreach($arg in $arguments){$p.ArgumentList.Add($arg)}
 $started=[DateTime]::UtcNow.ToString('o');$proc=[Diagnostics.Process]::Start($p)
 $stdout=$proc.StandardOutput.ReadToEndAsync();$stderr=$proc.StandardError.ReadToEndAsync()
 if(-not $proc.WaitForExit(45000)){$proc.Kill($true);throw "Timed out $name; stop at checkpoint rather than debug infrastructure."}
 $out=$stdout.GetAwaiter().GetResult();$err=$stderr.GetAwaiter().GetResult()
 [IO.File]::WriteAllText((Join-Path $evidence "$name.stdout.txt"),$out)
 [IO.File]::WriteAllText((Join-Path $evidence "$name.stderr.txt"),$err)
 $details=@([regex]::Matches($out+' '+$err,'\b(warning|error) (CS\d+)')|ForEach-Object{"$($_.Groups[1].Value) $($_.Groups[2].Value)"}|Sort-Object -Unique)
 $row=@{name=$name;command='dotnet '+($arguments -join ' ');working_directory=$working;started_at=$started;exit_code=$proc.ExitCode;diagnostics=@([regex]::Matches($out+' '+$err,'\b(?:warning|error) (CS\d+)')|ForEach-Object{$_.Groups[1].Value}|Sort-Object -Unique);diagnostic_details=$details}
 $rows.Add($row);$proc.Dispose();return $row
}
$scenarios=@(
 @{name='open';flags='OPEN';sdk='10.0.403';tfm='net10.0';lang='14.0';runtime='10.0.14'},
 @{name='open-fallback';flags='OPEN;FALLBACK';sdk='10.0.403';tfm='net10.0';lang='14.0';runtime='10.0.14'},
 @{name='closed';flags='';sdk='11.0.100-rc.3.26522.148';tfm='net11.0';lang='15.0';runtime='11.0.0-rc.3.26522.148'},
 @{name='missing';flags='MISSING'},@{name='added';flags='ADDED'},
 @{name='missing-werror';flags='MISSING';werror=$true},
 @{name='direct';flags='DIRECT';foreign=$true},
 @{name='indirect';flags='INDIRECT';foreign=$true},
 @{name='intermediate';flags='INTERMEDIATE'},
 @{name='intermediate-foreign';flags='INTERMEDIATE';foreign=$true},
 @{name='nullable';flags='NULLABLE'},@{name='null-handled';flags='NULLABLE;HANDLE_NULL'}
 @{name='open-csharp15';flags='OPEN'},@{name='open-direct';flags='OPEN;DIRECT';foreign=$true}
)
foreach($s in $scenarios){
 foreach($key in @('sdk','tfm','lang','runtime')){if(-not $s.ContainsKey($key)){$s[$key]=$scenarios[2][$key]}}
 $work=Join-Path $evidence $s.name;New-Item -ItemType Directory -Force $work | Out-Null
 @{sdk=@{version=$s.sdk;rollForward='disable';allowPrerelease=$true}}|ConvertTo-Json|Set-Content (Join-Path $work 'global.json')
 '<configuration><packageSources><clear /></packageSources></configuration>' | Set-Content (Join-Path $work 'NuGet.Config')
 foreach($kind in @('Model','Foreign','Consumer')){
  if($kind -eq 'Foreign' -and -not $s.foreign){continue}
  $dir=Join-Path $work $kind;New-Item -ItemType Directory -Force $dir|Out-Null
  $references=if($kind -eq 'Model'){''}else{'<ProjectReference Include="../Model/Model.csproj" />'}
  if($kind -eq 'Consumer' -and $s.name -eq 'indirect'){$references+='<ProjectReference Include="../Foreign/Foreign.csproj" />'}
  $output=if($kind -eq 'Consumer'){'Exe'}else{'Library'}
  $werror=if($s.werror){'true'}else{'false'}
  $props="<TargetFramework>$($s.tfm)</TargetFramework><LangVersion>$($s.lang)</LangVersion><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><OutputType>$output</OutputType><EnableDefaultCompileItems>false</EnableDefaultCompileItems><DefineConstants>$($s.flags)</DefineConstants><TreatWarningsAsErrors>$werror</TreatWarningsAsErrors><RuntimeFrameworkVersion>$($s.runtime)</RuntimeFrameworkVersion><RollForward>Disable</RollForward><EnableNETAnalyzers>false</EnableNETAnalyzers>"
  $file=[Security.SecurityElement]::Escape((Join-Path $source "$kind.cs"))
  "<Project Sdk=`"Microsoft.NET.Sdk`"><PropertyGroup>$props</PropertyGroup><ItemGroup><Compile Include=`"$file`" />$references</ItemGroup></Project>" | Set-Content (Join-Path $dir "$kind.csproj")
 }
 $target=if($s.foreign -and $s.name -ne 'indirect'){'Foreign/Foreign.csproj'}else{'Consumer/Consumer.csproj'}
 $build=Execute "$($s.name)-build" $work @('build',$target,'-c','Release','--nologo','--disable-build-servers','-m:1','-p:UseSharedCompilation=false','-v:minimal')
 if($build.exit_code -eq 0 -and $target -like 'Consumer*'){
  $run=Execute "$($s.name)-run" $work @('run','--project',$target,'-c','Release','--no-build','--no-restore')
  if($run.exit_code -ne 0){throw "Runtime assertion failed: $($s.name)"}
 }
 $rows|ConvertTo-Json -Depth 10|Set-Content (Join-Path $evidence 'commands.json')
 Write-Output "$($s.name): exit=$($build.exit_code) diagnostics=$($build.diagnostics -join ',')"
}
Execute 'sdk11-info' (Join-Path $evidence 'closed') @('--info')|Out-Null
Execute 'sdk10-info' (Join-Path $evidence 'open') @('--info')|Out-Null
$rows|ConvertTo-Json -Depth 10|Set-Content (Join-Path $evidence 'commands.json')
$expected=@{'open'='CS8509';'open-fallback'='';'closed'='';'missing'='CS8509';'added'='CS8509';'missing-werror'='CS8509';'direct'='CS9382';'indirect'='';'intermediate'='';'intermediate-foreign'='CS9382';'nullable'='CS8655';'null-handled'='';'open-csharp15'='CS8509';'open-direct'=''}
foreach($s in $scenarios){
 $build=@($rows|Where-Object name -eq "$($s.name)-build")[0]
 $exit=if($s.name -in @('missing-werror','direct','intermediate-foreign')){1}else{0}
 if($build.exit_code -ne $exit -or ($build.diagnostics -join ',') -cne $expected[$s.name]){throw "Unexpected compiler result for $($s.name)"}
 $detail=if($expected[$s.name]){if($exit -eq 1){'error '+$expected[$s.name]}else{'warning '+$expected[$s.name]}}else{''}
 if(($build.diagnostic_details -join ',') -cne $detail){throw "Unexpected diagnostic severity for $($s.name)"}
}
Write-Output 'All expected compiler outcomes verified.'

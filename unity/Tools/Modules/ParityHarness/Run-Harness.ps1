param([ValidateRange(1,10000)][int]$Repetitions = 5, [switch]$RequireComplete)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$evidence = Join-Path $root 'docs/agent-runs/20261104-three-chats/unity/ParityHarness'
$output = Join-Path $root 'campaign-staging/20261104-three-chats/unity/ParityHarness'
$assets = Join-Path $root 'unity/Assets/Ashfall/Modules/ParityHarness'
$compiler = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
foreach ($directory in @($evidence,$output)) {
    if (!(Test-Path -LiteralPath $directory)) { [void](New-Item -ItemType Directory -Path $directory) }
    if ((Get-Item -LiteralPath $directory).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Refuse reparse output: $directory" }
}
$inputs = @((Join-Path $root 'unity/Assets/Ashfall/Core/Contracts.cs'), (Join-Path $root 'unity/Tools/ToolchainProbe/ConversionProbe.cs'))
$inputs += @(Get-ChildItem -LiteralPath $assets -Filter '*.cs' | Sort-Object Name | ForEach-Object { $_.FullName })
$inputs += @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | Sort-Object Name | ForEach-Object { $_.FullName })
$binary = Join-Path $output 'ParityHarness.exe'
$compilerArgs = @('/nologo','/target:exe','/langversion:5','/warn:4','/warnaserror+','/main:Ashfall.Modules.ParityHarness.HarnessMain','/reference:System.Web.Extensions.dll',('/out:"' + $binary + '"'))
$compilerArgs += @($inputs | ForEach-Object { '"' + $_ + '"' })
('"' + $compiler + '" ' + ($compilerArgs -join ' ')) | Set-Content -LiteralPath (Join-Path $evidence 'compile-command.txt')
& $compiler @compilerArgs 2>&1 | Tee-Object -FilePath (Join-Path $evidence 'compile.log')
if ($LASTEXITCODE -ne 0) { throw "Compiler exit $LASTEXITCODE" }
& $binary '--self-test' 2>&1 | Tee-Object -FilePath (Join-Path $evidence 'self-test.log')
if ($LASTEXITCODE -ne 0) { throw "Self-test exit $LASTEXITCODE" }
$runArgs = @((Join-Path $evidence 'results.json'),[string]$Repetitions)
if ($RequireComplete) { $runArgs += '--strict' }
& $binary @runArgs 2>&1 | Tee-Object -FilePath (Join-Path $evidence 'run.log')
$runExit = $LASTEXITCODE
@{ compiler = $compiler; compilerExit = 0; selfTestExit = 0; runnerExit = $runExit; repetitions = $Repetitions; strict = [bool]$RequireComplete; inputs = @($inputs | ForEach-Object { @{ path = $_; sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash } }) } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $evidence 'receipt.json')
exit $runExit

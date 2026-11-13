$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$output = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$sources = @((Join-Path $root 'Assets/Ashfall/Core/Contracts.cs'))
$sources += @(Get-ChildItem -LiteralPath (Join-Path $root 'Assets/Ashfall/Modules/Modes') -Filter '*.cs' | ForEach-Object { $_.FullName })
$compiler = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$productionArgs = @('/nologo','/langversion:5','/warnaserror+','/target:library',('/out:"' + (Join-Path $output 'Ashfall.Modes.dll') + '"'))
$productionArgs += @($sources | ForEach-Object { '"' + [IO.Path]::GetFullPath($_) + '"' })
$productionArgs | Set-Content -LiteralPath (Join-Path $output 'production.rsp') -Encoding UTF8
& $compiler ('@' + (Join-Path $output 'production.rsp'))
if ($LASTEXITCODE -ne 0) { throw 'Production compile failed' }
@('/nologo','/langversion:5','/warnaserror+','/target:exe',('/out:"' + (Join-Path $output 'Modes.Tests.exe') + '"'),('/reference:"' + (Join-Path $output 'Ashfall.Modes.dll') + '"'),('"' + (Join-Path $PSScriptRoot 'Tests.cs') + '"')) | Set-Content -LiteralPath (Join-Path $output 'tests.rsp') -Encoding UTF8
& $compiler ('@' + (Join-Path $output 'tests.rsp'))
if ($LASTEXITCODE -ne 0) { throw 'Test compile failed' }
& (Join-Path $output 'Modes.Tests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Behavior tests failed' }

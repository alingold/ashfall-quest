$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$outDir = Join-Path $root 'campaign-staging/20261104-three-chats/unity/Construction'
$compiler = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$sources = @((Join-Path $root 'unity/Assets/Ashfall/Core/Contracts.cs'))
$sources += @(Get-ChildItem -LiteralPath (Join-Path $root 'unity/Assets/Ashfall/Modules/Construction') -Filter '*.cs' | Sort-Object Name | ForEach-Object { $_.FullName })
$sources += Join-Path $PSScriptRoot 'ConstructionTests.cs'
$exe = Join-Path $outDir 'ConstructionTests.exe'
$quotedSources = @($sources | ForEach-Object { '"' + $_.Replace('/','\') + '"' })
& $compiler /nologo /langversion:5 /warnaserror+ /target:exe ('/out:"' + $exe.Replace('/','\') + '"') $quotedSources
if ($LASTEXITCODE -ne 0) { throw 'Construction compile failed' }
& $exe
if ($LASTEXITCODE -ne 0) { throw 'Construction tests failed' }

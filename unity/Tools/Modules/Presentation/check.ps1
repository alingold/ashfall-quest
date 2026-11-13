$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$output = Join-Path $PSScriptRoot 'PresentationTests.exe'
$sources = @((Join-Path $root 'Assets/Ashfall/Core/Contracts.cs'))
$sources += @(Get-ChildItem -LiteralPath (Join-Path $root 'Assets/Ashfall/Modules/Presentation') -Filter '*.cs' -File | ForEach-Object FullName)
$sources += Join-Path $PSScriptRoot 'PresentationTests.cs'
$quotedSources = @($sources | ForEach-Object { '"' + $_ + '"' })
& 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe' /nologo /langversion:5 /warnaserror+ /target:exe ('/out:"' + $output + '"') $quotedSources
if ($LASTEXITCODE -ne 0) { throw 'C#5 compilation failed' }
& $output
if ($LASTEXITCODE -ne 0) { throw 'Presentation tests failed' }

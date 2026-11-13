$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$compiler = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$output = Join-Path $PSScriptRoot 'PlayerTests.exe'
$files = @((Join-Path $root 'Assets/Ashfall/Core/Contracts.cs'))
$files += Get-ChildItem (Join-Path $root 'Assets/Ashfall/Modules/Player') -Filter *.cs -Recurse | Where-Object { $_.Directory.Name -ne 'Engine' } | ForEach-Object { $_.FullName }
$files += Join-Path $PSScriptRoot 'PlayerTests.cs'
$response = Join-Path $PSScriptRoot 'compile.rsp'
$arguments = @('/nologo', '/langversion:5', '/warnaserror+', '/target:exe', ('/out:"' + $output + '"'))
$arguments += $files | ForEach-Object { '"' + $_ + '"' }
$arguments | Set-Content -Encoding UTF8 $response
& $compiler "@$response"
if ($LASTEXITCODE -ne 0) { throw 'C#5 compile failed' }
& $output
if ($LASTEXITCODE -ne 0) { throw 'Player behavioral tests failed' }


$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$out = [IO.Path]::GetFullPath((Join-Path $root 'campaign-staging/20261104-three-chats/unity/Sessions'))
$evidence = [IO.Path]::GetFullPath((Join-Path $root 'docs/agent-runs/20261104-three-chats/unity/Sessions'))
$compiler = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$sources = @([IO.Path]::GetFullPath((Join-Path $root 'unity/Assets/Ashfall/Core/Contracts.cs')))
$sources += @(Get-ChildItem -LiteralPath (Join-Path $root 'unity/Assets/Ashfall/Modules/Sessions') -Filter '*.cs' | ForEach-Object { $_.FullName })
$test = Join-Path $PSScriptRoot 'SessionTests.cs'
$exe = Join-Path $out 'SessionTests.exe'
& $compiler /nologo /langversion:5 /warnaserror+ /target:library "/out:$out/Sessions.dll" $sources 2>&1 | Tee-Object -FilePath (Join-Path $evidence 'compile-library.txt')
if ($LASTEXITCODE -ne 0) { throw 'Library compile failed' }
& $compiler /nologo /langversion:5 /warnaserror+ /target:exe "/out:$exe" $sources $test 2>&1 | Tee-Object -FilePath (Join-Path $evidence 'compile-tests.txt')
if ($LASTEXITCODE -ne 0) { throw 'Test compile failed' }
& $exe 2>&1 | Tee-Object -FilePath (Join-Path $evidence 'test-results.txt')
if ($LASTEXITCODE -ne 0) { throw 'Behavior tests failed' }

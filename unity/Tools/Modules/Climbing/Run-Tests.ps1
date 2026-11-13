$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$outputRoot = Join-Path $taskRoot 'campaign-staging/20261104-three-chats/unity/Climbing/r2'
$evidenceRoot = Join-Path $taskRoot 'docs/agent-runs/20261104-three-chats/unity/Climbing/evidence/r2'
New-Item -ItemType Directory -Force -Path $outputRoot,$evidenceRoot | Out-Null
$compiler = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$core = Join-Path $taskRoot 'unity/Assets/Ashfall/Core/Contracts.cs'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'unity/Assets/Ashfall/Modules/Climbing') -Filter '*.cs' | Sort-Object Name | ForEach-Object { $_.FullName })
$exe = Join-Path $outputRoot 'ClimbingTests.exe'
& $compiler /nologo /langversion:5 /warnaserror+ /target:exe ('/out:' + $exe) $core $sources (Join-Path $PSScriptRoot 'ClimbingTests.cs') 2>&1 | Tee-Object -FilePath (Join-Path $evidenceRoot 'compile.log')
if ($LASTEXITCODE -ne 0) { throw "Compiler failed: $LASTEXITCODE" }
& $exe 2>&1 | Tee-Object -FilePath (Join-Path $evidenceRoot 'tests.log')
if ($LASTEXITCODE -ne 0) { throw "Tests failed: $LASTEXITCODE" }

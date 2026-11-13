param([string]$Workspace = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path)
$ErrorActionPreference = 'Stop'
$out = Join-Path $Workspace 'campaign-staging/20261104-three-chats/unity/Persistence/r2'
$evidence = Join-Path $Workspace 'docs/agent-runs/20261104-three-chats/unity/Persistence/r2'
$compiler = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$exe = Join-Path $out 'PersistenceTests.exe'
$sources = @((Join-Path $Workspace 'unity/Assets/Ashfall/Core/Contracts.cs')) + @(Get-ChildItem (Join-Path $Workspace 'unity/Assets/Ashfall/Modules/Persistence/*.cs') | Sort-Object Name | ForEach-Object FullName) + @((Join-Path $Workspace 'unity/Tools/Modules/Persistence/PersistenceTests.cs'))
# PowerShell passes each absolute path as one argument, including paths containing spaces.
& $compiler /nologo /langversion:5 /warnaserror+ /target:exe "/out:$exe" $sources 2>&1 | Tee-Object (Join-Path $evidence 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Compiler failed' }
& $exe $out 2>&1 | Tee-Object (Join-Path $evidence 'tests.log')
if ($LASTEXITCODE -ne 0) { throw 'Behavior checks failed' }

$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$evidence=Join-Path $root 'docs/agent-runs/20261104-three-chats/unity/Audio'
$output=Join-Path $root 'campaign-staging/20261104-three-chats/unity/Audio'
$module=Join-Path $root 'unity/Assets/Ashfall/Modules/Audio'
$csc='C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$files=@((Join-Path $root 'unity/Assets/Ashfall/Core/Contracts.cs'))+@(Get-ChildItem -LiteralPath $module -Filter '*.cs' -File | ForEach-Object FullName)+@((Join-Path $PSScriptRoot 'AudioTests.cs'))
$exe=Join-Path $output 'AudioTests.exe'
$arguments=@('/nologo','/langversion:5','/warnaserror+','/target:exe',('/out:'+$exe))+$files
('Compiler: '+$csc+"`n"+($arguments | ForEach-Object {'"'+$_+'"'}) -join "`n") | Set-Content -LiteralPath (Join-Path $evidence 'compiler-command.txt')
& $csc @arguments 2>&1 | Tee-Object -FilePath (Join-Path $evidence 'compile.txt')
if($LASTEXITCODE -ne 0){throw 'Compile failed'}
& $exe (Join-Path $output 'test-fixtures') 2>&1 | Tee-Object -FilePath (Join-Path $evidence 'tests.txt')
if($LASTEXITCODE -ne 0){throw 'Tests failed'}

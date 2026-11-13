$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$output = Join-Path $root 'campaign-staging/20261104-three-chats/unity/Avatar/AvatarTests.exe'
$evidence = Join-Path $root 'docs/agent-runs/20261104-three-chats/unity/Avatar/checks.txt'
New-Item -ItemType Directory -Force ([IO.Path]::GetDirectoryName($output)),([IO.Path]::GetDirectoryName($evidence)) | Out-Null
$sources = @(Join-Path $root 'unity/Assets/Ashfall/Core/Contracts.cs')
$sources += @(Get-ChildItem (Join-Path $root 'unity/Assets/Ashfall/Modules/Avatar') -Filter '*.cs' -Recurse | ForEach-Object { $_.FullName })
$sources += Join-Path $PSScriptRoot 'AvatarTests.cs'
$compiler = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$arguments = @('/nologo','/langversion:5','/warnaserror+','/target:exe',('/out:' + $output)) + $sources
('COMMAND: "' + $compiler + '" ' + (($arguments | ForEach-Object { '"' + $_ + '"' }) -join ' ')) | Set-Content $evidence
& $compiler @arguments 2>&1 | Tee-Object -FilePath $evidence -Append
if ($LASTEXITCODE -ne 0) { throw 'Avatar C#5 compilation failed' }
& $output 2>&1 | Tee-Object -FilePath $evidence -Append
if ($LASTEXITCODE -ne 0) { throw 'Avatar behavior checks failed' }

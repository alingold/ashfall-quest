$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot/../../../..").Path
$output = Join-Path $repo 'campaign-staging/20261104-three-chats/unity/World/WorldTests.exe'
$compiler = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$files = @((Join-Path $repo 'unity/Assets/Ashfall/Core/Contracts.cs'), (Join-Path $repo 'unity/Assets/Ashfall/Modules/World/Ports.cs'), (Join-Path $repo 'unity/Assets/Ashfall/Modules/World/WorldLoader.cs'), (Join-Path $PSScriptRoot 'WorldTests.cs'))
$files = @($files | ForEach-Object { $_.Replace('/', '\') })
$output = $output.Replace('/', '\')
& $compiler /nologo /langversion:5 /r:System.Runtime.Serialization.dll "/out:$output" $files
if ($LASTEXITCODE -ne 0) { throw 'Compile failed' }
& $output (Join-Path $repo 'campaign-staging/20261104-three-chats/unity/World/alfa-aglae-souza.json') (Join-Path $repo 'campaign-staging/20261104-three-chats/unity/World/battlefield-paintball-gods-pitdevil.json')
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }

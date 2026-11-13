param([switch]$MapsReleased)
$ErrorActionPreference='Stop'
if(-not $MapsReleased){throw 'Requires concrete shared-lane release'}
$vrRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$vrWorkspace=Split-Path -Parent $vrRoot
$vrPython='C:\Users\aling\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
$vrEditor='C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Unity.exe'
$vrLane=Join-Path $vrWorkspace 'campaign-staging\20261104-three-chats\world\engine_lane.py'
$vrStamp=Get-Date -Format 'yyyyMMdd-HHmmss'
$vrEvidence=Join-Path $vrWorkspace ('docs\agent-runs\20261104-three-chats\unity\playable-r23\player-floor-'+$vrStamp)
New-Item -ItemType Directory -Path $vrEvidence | Out-Null
$vrProbe=Join-Path $vrRoot ('Builds\FloorProbe-'+$vrStamp+'\AshfallFloorProbe.exe')
& $vrPython $vrLane -- $vrEditor -batchmode -projectPath $vrRoot -executeMethod Ashfall.Editor.Playable.R23PlayableBuild.BuildWindows -ashfallXRDisabled true -ashfallVRBuild $vrProbe -logFile (Join-Path $vrEvidence 'probe-build.log')
if($LASTEXITCODE -ne 0){throw ('XR-disabled probe build failed: '+$vrEvidence)}
& $vrPython $vrLane -- $vrProbe -batchmode -screen-fullscreen 0 -screen-width 640 -screen-height 360 -ashfallAutomaticFloorProbe (Join-Path $vrEvidence 'player-floor.json') -logFile (Join-Path $vrEvidence 'player.log')
if($LASTEXITCODE -ne 0){throw ('Exported player floor probe failed: '+$vrEvidence)}
$vrResult=Get-Content -LiteralPath (Join-Path $vrEvidence 'player-floor.json') -Raw | ConvertFrom-Json
if(-not $vrResult.probeComplete -or -not $vrResult.collisionMeshesReadable -or $vrResult.expectedCases -lt 80 -or $vrResult.cases.Count -ne $vrResult.expectedCases -or @($vrResult.cases | Where-Object fell).Count -ne 0){throw 'Exported floor case evidence incomplete'}
if(Select-String -LiteralPath (Join-Path $vrEvidence 'player.log') -Pattern "CollisionMeshData couldn't be created" -Quiet){throw 'Player still has inaccessible collision mesh errors'}
$vrOutput=Join-Path $vrRoot ('Builds\R23VR-'+$vrStamp+'-FloorFix\AshfallR23VR.exe')
& $vrPython $vrLane -- $vrEditor -batchmode -projectPath $vrRoot -executeMethod Ashfall.Editor.Playable.R23PlayableBuild.BuildWindows -ashfallVRBuild $vrOutput -logFile (Join-Path $vrEvidence 'vr-build.log')
if($LASTEXITCODE -ne 0){throw ('Corrected VR candidate build failed: '+$vrEvidence)}
Write-Output ('Exported-player floor proof: '+$vrEvidence)
Write-Output ('Separate corrected PCVR candidate: '+$vrOutput)

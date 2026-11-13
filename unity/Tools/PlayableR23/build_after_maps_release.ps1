param([switch]$MapsReleased,[switch]$BuildOnly)
$ErrorActionPreference='Stop'
if(-not $MapsReleased){throw 'Maps has priority. Run only after its concrete engine/device release, with -MapsReleased.'}
$vrRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$vrWorkspace=Split-Path -Parent $vrRoot
$vrPython='C:\Users\aling\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
$vrEditor='C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Unity.exe'
$vrLane=Join-Path $vrWorkspace 'campaign-staging\20261104-three-chats\world\engine_lane.py'
$vrStamp=Get-Date -Format 'yyyyMMdd-HHmmss'
$vrEvidence=Join-Path $vrWorkspace ('docs\agent-runs\20261104-three-chats\unity\playable-r23\run-'+$vrStamp)
New-Item -ItemType Directory -Path $vrEvidence | Out-Null
$vrSmoke=Join-Path $vrEvidence 'smoke.json'
if(-not $BuildOnly){
& $vrPython $vrLane -- $vrEditor -batchmode -projectPath $vrRoot -executeMethod Ashfall.Editor.Playable.R23SmokeControl.Run -ashfallVRSmoke $vrSmoke -logFile (Join-Path $vrEvidence 'smoke-editor.log')
if($LASTEXITCODE -ne 0){throw ('Smoke/import failed or shared lane busy; see '+$vrEvidence)}
$vrResult=Get-Content -LiteralPath $vrSmoke -Raw | ConvertFrom-Json
if(-not ($vrResult.map -and $vrResult.movement -and $vrResult.pickup -and $vrResult.fire -and $vrResult.damage -and $vrResult.restart -and $vrResult.loadout) -or $vrResult.error){throw 'Focused map loop/loadout smoke incomplete'}
}
$vrOutput=Join-Path $vrRoot ('Builds\R23VR-'+$vrStamp+'\AshfallR23VR.exe')
& $vrPython $vrLane -- $vrEditor -batchmode -projectPath $vrRoot -executeMethod Ashfall.Editor.Playable.R23PlayableBuild.BuildWindows -ashfallVRBuild $vrOutput -logFile (Join-Path $vrEvidence 'build-editor.log')
if($LASTEXITCODE -ne 0){throw ('Build failed or shared lane busy; see '+$vrEvidence)}
Write-Output ('Built: '+$vrOutput)
if(-not $BuildOnly){Write-Output ('Focused smoke: '+$vrSmoke)}
Write-Output 'Headset launch and actual tracking remain separate checks; no automatic launch performed.'

param([switch]$MapsReleased,[switch]$SkipReleaseLint,[string]$NativeReceipt,[string]$BaselineReceipt,[string]$FeedbackReceipt,[string]$ArsenalReceipt,[string]$MapEntryReceipt,[string]$SourceFreeze,[string]$BuildOutputRoot,[string]$CheckpointReceipt)
$ErrorActionPreference='Stop'
if(-not $MapsReleased){throw 'Concrete WORLD/Skins release required'}
$vrRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$vrWorkspace=Split-Path -Parent $vrRoot
$vrPython='C:\Users\aling\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
$vrEditor='C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Unity.exe'
$vrLane=Join-Path $vrWorkspace 'campaign-staging\20261104-three-chats\world\engine_lane.py'
$env:GRADLE_OPTS='-Dorg.gradle.daemon=false'
$env:GRADLE_USER_HOME=Join-Path $env:USERPROFILE '.cache\ashfall-unity-gradle'
$vrStamp=Get-Date -Format 'yyyyMMdd-HHmmss'
$vrEvidence=Join-Path $vrWorkspace ('docs\agent-runs\20261104-three-chats\unity\playable-r23\quest-'+$vrStamp)
New-Item -ItemType Directory -Path $vrEvidence | Out-Null
if($ArsenalReceipt){
  $vrArsenal=Get-Content -LiteralPath $ArsenalReceipt -Raw | ConvertFrom-Json
  $vrRequired=@('ActualRootMapSetup','ActualSourceDefaultKit','ActualSourceCarriedFireAndPhysicalReload','ActualFourSmgFireReloadAndLaser','ActualCatalogFiveFireAndPhysicalReload','ActualIndependentWorldCopies','ActualRemoteGripReplacementAndRearm','ActualShakerStackAtomicWorldEviction','ActualNoFloorRefusesInventoryMutation','ActualSharedGripArbitration','ActualRecoveredCacheFloorAndVisibility','ActualAlphaCacheFloorAndMarkers','SixteenDecodedPrivateGalleryPreviews','SceneAndProbeCleanup')
  $vrRequired+=@('ActualR24RoofWarmupRestart','ActualR24PostMotionFreeHands','ActualR24GrenadePickupAndSelfDamage','ActualR24LoadoutAndOutfitCards')
  if(-not $vrArsenal.success -or -not $vrArsenal.nativeExecuted -or $vrArsenal.error -or $vrArsenal.groups.Count -ne $vrRequired.Count){throw 'Actual arsenal native receipt does not pass'}
  foreach($vrGroup in $vrRequired){$vrMatches=@($vrArsenal.groups | Where-Object {$_.name -eq $vrGroup});if($vrMatches.Count -ne 1 -or -not $vrMatches[0].passed -or $vrMatches[0].error){throw ('Arsenal group missing/failed: '+$vrGroup)}}
  Copy-Item -LiteralPath $ArsenalReceipt -Destination (Join-Path $vrEvidence 'arsenal.json')
}
if($FeedbackReceipt){
  $vrFeedback=Get-Content -LiteralPath $FeedbackReceipt -Raw | ConvertFrom-Json
  if(-not $vrFeedback.success -or $vrFeedback.groups.Count -lt 10 -or @($vrFeedback.groups | Where-Object {-not $_.passed -or $_.error}).Count -gt 0){throw 'Current feedback/native receipt does not pass'}
  Copy-Item -LiteralPath $FeedbackReceipt -Destination (Join-Path $vrEvidence 'feedback.json')
}
if($MapEntryReceipt){
  $vrEntry=Get-Content -LiteralPath $MapEntryReceipt -Raw | ConvertFrom-Json
  if($vrEntry.error -or $vrEntry.floorCases -ne 16 -or -not $vrEntry.loaded -or -not $vrEntry.movement -or -not $vrEntry.pickup -or -not $vrEntry.fire -or -not $vrEntry.damage -or -not $vrEntry.restart -or -not $vrEntry.lobbyRestore){throw 'Current explicit map entry/floor receipt does not pass'}
  Copy-Item -LiteralPath $MapEntryReceipt -Destination (Join-Path $vrEvidence 'map-entry.json')
}
if($SourceFreeze){& $vrPython (Join-Path $PSScriptRoot 'freeze_candidate.py') $SourceFreeze --verify;if($LASTEXITCODE -ne 0){throw 'Candidate changed after native smoke'}}
$vrSmoke=Join-Path $vrEvidence 'human-bugs.json'
if($CheckpointReceipt){
  if(-not $SourceFreeze){throw 'Native checkpoint requires its exact source freeze'}
  $vrCheckpoint=Get-Content -LiteralPath $CheckpointReceipt -Raw | ConvertFrom-Json
  $vrFreezeHash=(Get-FileHash -LiteralPath (Join-Path $SourceFreeze 'inputs.json') -Algorithm SHA256).Hash.ToLowerInvariant()
  if($vrCheckpoint.freezeSha256 -ne $vrFreezeHash -or -not $vrCheckpoint.sourcePostverify -or -not $vrCheckpoint.main.nativeExecuted -or -not $vrCheckpoint.main.success -or $vrCheckpoint.main.error -or $vrCheckpoint.productionExceptions -ne 0 -or $vrCheckpoint.shaderCompilerErrorHeadings -ne 0){throw 'Current native checkpoint/source or production qualification failed'}
  foreach($vrGroup in $vrCheckpoint.main.groups){if(-not $vrGroup.passed -or $vrGroup.error){throw 'Failed native checkpoint group'}}
  Copy-Item -LiteralPath $CheckpointReceipt -Destination (Join-Path $vrEvidence 'native-checkpoint.json')
  Write-Output 'Reusing exact-source scoped native checkpoint; its recorded limitations and earlier scoped evidence remain explicit. No historical suite replay.'
}elseif($BaselineReceipt){
  $vrBaseline=Get-Content -LiteralPath $BaselineReceipt -Raw | ConvertFrom-Json
  $vrRequiredBaseline=@('RangeOneGroundKitAndActualCarriedOwners','RangeMenuResumeAndTwoSourceViews','AlphaSortedBaselineSeparateFromCaches','AsylumOneMountedWeaponStreaming','RangeActualRootPickupFiniteOwner','ActualSceneCleanup')
  if(-not $vrBaseline.success -or -not $vrBaseline.nativeExecuted -or $vrBaseline.error -or $vrBaseline.groups.Count -ne $vrRequiredBaseline.Count){throw 'Current baseline native receipt does not pass'}
  foreach($vrGroup in $vrRequiredBaseline){$vrMatches=@($vrBaseline.groups | Where-Object {$_.name -eq $vrGroup});if($vrMatches.Count -ne 1 -or -not $vrMatches[0].passed -or $vrMatches[0].error){throw ('Baseline group missing/failed: '+$vrGroup)}}
  Copy-Item -LiteralPath $BaselineReceipt -Destination (Join-Path $vrEvidence 'baseline-ground.json')
}elseif($NativeReceipt){
  $vrPrior=Get-Content -LiteralPath $NativeReceipt -Raw | ConvertFrom-Json
  if(-not $vrPrior.success -or $vrPrior.groups.Count -ne 4){throw 'Prior focused native receipt does not pass'}
  Copy-Item -LiteralPath $NativeReceipt -Destination (Join-Path $vrEvidence 'prior-human-bugs.json')
}else{
& $vrPython $vrLane -- $vrEditor -batchmode -projectPath $vrRoot -executeMethod Ashfall.Editor.Playable.R23SmokeControl.Run -ashfallHumanBugs true -ashfallVRSmoke $vrSmoke -logFile (Join-Path $vrEvidence 'human-bugs-editor.log')
if($LASTEXITCODE -ne 0){throw ('Human-bug native check failed/shared lane busy: '+$vrEvidence)}
if(-not(Test-Path -LiteralPath $vrSmoke)){throw 'Human-bug receipt missing'}
}
if($BuildOutputRoot){if(-not [IO.Path]::IsPathRooted($BuildOutputRoot)){throw 'Absolute artifact root required'};$vrArtifactRoot=[IO.Path]::GetFullPath($BuildOutputRoot)}else{$vrArtifactRoot=Join-Path $vrRoot 'Builds'}
$vrOutput=Join-Path $vrArtifactRoot ('UnityQuest-'+$vrStamp+'\AshfallUnityQuest.apk')
$vrSdk=Join-Path $env:USERPROFILE '.cache\ashfall-unity-sdk'
# Current incremental Android budget: one regenerated Gradle APK on C and one
# fresh immutable release on the output drive. Never reclaim space here.
$vrCDrive=New-Object IO.DriveInfo('C:\')
$vrArtifactDrive=New-Object IO.DriveInfo([IO.Path]::GetPathRoot($vrOutput))
$vrMinimumC=4294967296L
if($vrArtifactDrive.Name -eq $vrCDrive.Name){$vrMinimumC=5368709120L}
$vrBudget=[ordered]@{utc=[DateTime]::UtcNow.ToString('o');cFreeBytes=$vrCDrive.AvailableFreeSpace;minimumCBytes=$vrMinimumC;artifactDrive=$vrArtifactDrive.Name;artifactFreeBytes=$vrArtifactDrive.AvailableFreeSpace;minimumArtifactBytes=2147483648L;deletionsPerformed=$false}
$vrBudget | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $vrEvidence 'storage-budget.json') -Encoding UTF8
if($vrBudget.cFreeBytes -lt $vrMinimumC -or $vrBudget.artifactFreeBytes -lt 2147483648L){throw 'Insufficient verified Android intermediate/artifact headroom; preserve released builds and coordinate storage before another admission'}
& $vrPython $vrLane -- $vrEditor -batchmode -buildTarget Android -projectPath $vrRoot -executeMethod Ashfall.Editor.Playable.R23PlayableBuild.BuildQuest -ashfallAndroidSdk $vrSdk -ashfallQuestBuild $vrOutput -ashfallCompressGeometry true -ashfallSkipReleaseLint ($SkipReleaseLint.IsPresent.ToString().ToLowerInvariant()) -logFile (Join-Path $vrEvidence 'android-editor.log')
if($LASTEXITCODE -ne 0){throw ('Quest build failed/shared lane busy: '+$vrEvidence)}
if(-not(Test-Path -LiteralPath $vrOutput)){throw 'APK missing'}
if($SourceFreeze){& $vrPython (Join-Path $PSScriptRoot 'freeze_candidate.py') $SourceFreeze --verify;if($LASTEXITCODE -ne 0){throw 'Candidate changed during build; artifact unqualified'}}
$vrQualification=@($vrOutput,'--output',(Join-Path $vrEvidence 'apk-receipt.json'),'--require-compressed-geometry')
if($SourceFreeze){$vrQualification+=@('--source-freeze',$SourceFreeze)}
& $vrPython (Join-Path $PSScriptRoot 'qualify_quest_apk.py') @vrQualification
if($LASTEXITCODE -ne 0){throw 'APK signature/package/ABI qualification failed'}
Write-Output ('APK_BUILT_NOT_INSTALLED: '+$vrOutput)
Write-Output ('EVIDENCE: '+$vrEvidence)

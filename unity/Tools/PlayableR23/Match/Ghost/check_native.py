from pathlib import Path
import hashlib,json,sys,shutil,uuid
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];LIVE=ROOT/'unity/Assets/Ashfall/Editor/Playable/R24GhostSmoke.cs';STAGE=HERE/'NativeStaged/R24GhostSmoke.cs'
p=LIVE if '--live'in sys.argv else STAGE;text=p.read_text()
for forbidden in ['GameObject.CreatePrimitive','new GameObject','new HealthService','new R23RoundState','AddComponent<','CreateSourceMesh(','new Mesh(']:assert forbidden not in text,forbidden
for real in ['UI.RequestMatchStart()','Gameplay.ApplyDirectPlayerDamage(1000)','Rig.SmokeXrMove','Rig.HeadCollision.Sweep','Gameplay.IsSourceActorLayer4','Gameplay.TryReadMeleeTargetCollider','UI.GhostHudVisible','UI.BlocksGameplay','Rig.RelocateSourceGhost','GhostRecoveryUsedSourceFallback','after.Revision==health.Revision','after.DownedUntil==health.DownedUntil','WeaponState()==before','art.AssertRestored(replacement)','new Material(actualGlove)','ActualSceneCleanup']:
 assert real in text,real
assert text.count('public IEnumerator Run()')==1 and 'public bool AutoFinish=true' in text and 'public string Json{get;private set;}'in text
assert 'actual[i].shader.isSupported'in text and 'actual[i].GetTexture("_SourceAlbedo")'in text
assert 'nativeExecuted=true'in text and 'headsetVerified=false'in text
print('PASS actual source-bound Ghost native fixture structure: seven independent groups incl cleanup; no geometry/Health/motor stand-ins. NATIVE UNRUN.')
if '--publish'in sys.argv:
 assert not LIVE.exists(),'Do not overwrite an existing native fixture without a new baseline guard'
 LIVE.write_bytes(STAGE.read_bytes());meta=LIVE.with_name(LIVE.name+'.meta');assert not meta.exists()
 meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'ashfall/R6/ghost/native/R24GhostSmoke.cs').hex+'\nMonoImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
 (HERE/'native-published.json').write_text(json.dumps(dict(files=[str(LIVE.relative_to(ROOT)),str(meta.relative_to(ROOT))],sha256=hashlib.sha256(LIVE.read_bytes()).hexdigest(),nativeExecuted=False),indent=2)+'\n')
 print('Published exact new Editor R24GhostSmoke.cs + meta; no shared editor/launcher changes.')

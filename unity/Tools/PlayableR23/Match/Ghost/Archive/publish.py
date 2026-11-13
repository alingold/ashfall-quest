from pathlib import Path
import hashlib,json,shutil,uuid
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[5];STAGE=HERE/'Staged';plan=json.loads((HERE/'candidate.json').read_text());LIVE=Path(plan['root'])
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
assert sha(LIVE/'Match/R24GhostRecoveryMath.cs')==plan['preservedBuiltinMathSha256']
assert sha(LIVE/'R23VRRig.GhostRecovery.cs')==plan['preservedRigRelocationSha256']
assert sha(HERE/'Payload/maps.json')==plan['payloadSha256']
for row in plan['files']:
 dst=LIVE/row['path'];assert sha(STAGE/row['path'])==row['stagedSha256'],row['path']
 assert (sha(dst)==row['baselineSha256']if row['baselineSha256']else not dst.exists()),('live baseline changed',row['path'])
payload=ROOT/'unity/Assets/StreamingAssets/PlayableR23/TeamLaunch/ArchiveRecovery/maps.json';native=ROOT/'unity/Assets/Ashfall/Editor/Playable/R24ArchiveGhostRecoverySmoke.cs'
assert not payload.exists()and not native.exists(),'New publication target already exists'
changed=[]
def meta(p,folder=False):
 q=p.with_name(p.name+'.meta')
 if q.exists():return
 guid=uuid.uuid5(uuid.NAMESPACE_URL,'ashfall/R6/archive-ghost/'+str(p.relative_to(ROOT))).hex
 body='folderAsset: yes\nDefaultImporter:'if folder else'MonoImporter:'if p.suffix=='.cs'else'DefaultImporter:'
 q.write_text('fileFormatVersion: 2\nguid: '+guid+'\n'+body+'\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n');changed.append(str(q.relative_to(ROOT)))
def copy(src,dst):
 dst.parent.mkdir(parents=True,exist_ok=True);same=dst.exists()and sha(src)==sha(dst)
 if not same:shutil.copyfile(src,dst);changed.append(str(dst.relative_to(ROOT)))
 meta(dst)
for row in plan['files']:copy(STAGE/row['path'],LIVE/row['path'])
copy(HERE/'Payload/maps.json',payload);meta(payload.parent,True)
copy(HERE/'NativeStaged/R24ArchiveGhostRecoverySmoke.cs',native)
assert sha(LIVE/'Match/R24GhostRecoveryMath.cs')==plan['preservedBuiltinMathSha256']and sha(LIVE/'R23VRRig.GhostRecovery.cs')==plan['preservedRigRelocationSha256']
(HERE/'published.json').write_text(json.dumps(dict(changedFiles=changed,sourceFiles=plan['files'],payloadSha256=sha(payload),nativeFixtureSha256=sha(native),nativeExecuted=False),indent=2)+'\n')
print('Published guarded archive ghost recovery:',len(changed),'changed/new paths; builtin math+sole rig relocation byte-unchanged. Native unrun.')

from pathlib import Path
import json,hashlib,subprocess,uuid,sys
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];LIVE=ROOT/'unity/Assets/Ashfall/Integration/Playable';STAGE=HERE/'RecoveryStaged'
if '--live'in sys.argv:STAGE=LIVE
if '--live'not in sys.argv:
 p=LIVE/'R23PlayableLoop.Match.cs';old='PublishMatchLife(false);if(Gameplay.TryRespawnLocalMatch())'
 text=p.read_text();assert text.count(old)==1
 (STAGE/p.name).write_text(text.replace(old,'PublishMatchLife(false);TickGhostRecovery();if(Gameplay.TryRespawnLocalMatch())'))
src=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source');manifest=src.parent/'SOURCE-MANIFEST.json';sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
assert sha(manifest)=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111'
pins={r['path']:r['sha256']for r in json.loads(manifest.read_text())['files']}
for name in ['scripts/net_bridge1000.gd','scripts/net_session1000.gd','scripts/player.gd']:
 assert sha(src/name)==pins[name]
recovery=(STAGE/'R23PlayableLoop.GhostRecovery.cs').read_text();rig=(STAGE/'R23VRRig.GhostRecovery.cs').read_text()
for banned in ['ResetRig(','.Spawn(','.ResetRound(','.Damage(','.ConfigureLocalMatchHealth(']:assert banned not in recovery+rig,banned
for tracked in ['Head.transform.localPosition=','Left.localPosition=','Right.localPosition=','TrackingOrigin.localPosition=']:assert tracked not in rig,tracked
assert 'candidate+Vector3.up*20,Vector3.down,50'in recovery and 'best.normal.y<.65f'in recovery
assert '.27f'in recovery and 'Calibration.Receipt.TargetHeadTop'in recovery and 'ghostRecoveryCheck<.10'in recovery
assert 'Health'not in rig.replace('No Health','No _')
exe=HERE/'RecoveryCases.exe';files=[ROOT/'unity/Assets/Ashfall/Core/Contracts.cs',ROOT/'unity/Assets/Ashfall/Integration/Playable/MatchZone/R24MatchZoneState.cs',STAGE/'Match/R24GhostRecoveryMath.cs',HERE/'RecoveryCases.cs']
p=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/nologo','/warn:4','/warnaserror+','/out:'+str(exe)]+[str(x)for x in files],capture_output=True,text=True);print(p.stdout+p.stderr);assert p.returncode==0
p=subprocess.run([str(exe)],capture_output=True,text=True);(HERE/'recovery-cases.log').write_text(p.stdout+p.stderr);print(p.stdout+p.stderr);assert p.returncode==0
if '--live'not in sys.argv:
 rows=[]
 for p in STAGE.rglob('*.cs'):
  rel=p.relative_to(STAGE).as_posix();dst=LIVE/rel;rows.append(dict(path=rel,baselineSha256=sha(dst)if dst.exists()else None,stagedSha256=sha(p)))
  meta=p.with_name(p.name+'.meta');original=dst.with_name(dst.name+'.meta')
  if original.exists():meta.write_bytes(original.read_bytes())
  else:meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'ashfall/R6/ghost/'+rel).hex+'\nMonoImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
 (HERE/'recovery-candidate.json').write_text(json.dumps(dict(root=str(LIVE),files=rows),indent=2)+'\n')
 # Reuse exact installed source closure with just these four candidate overrides.
 checker=ROOT/'unity/Tools/PlayableR23/check_integration.py';text=checker.read_text().replace("O=R/'campaign-staging/20261104-three-chats/unity/playable-r23/compiler'","O=Path("+repr(str(HERE/'RecoveryApi'))+")")
 text=text.replace("runtime,code=compile('R23Playable.Runtime',src,refs,'UNITY_6000_0_OR_NEWER');assert code==0",'stage=Path('+repr(str(STAGE))+');live=R/"unity/Assets/Ashfall/Integration/Playable"\noverrides={live/p.relative_to(stage):p for p in stage.rglob("*.cs")}\nsrc=sorted(set(overrides.get(p,p)for p in src)|set(overrides.values()))\nruntime,code=compile("R23Playable.Runtime",src,refs,"UNITY_6000_0_OR_NEWER");assert code==0')
 exec(compile(text,str(checker),'exec'),{'__file__':str(checker),'__name__':'__main__'})

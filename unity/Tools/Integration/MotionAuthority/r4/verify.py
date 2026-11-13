from pathlib import Path
import json,hashlib
R=Path.cwd();O=R/'campaign-staging/20261104-three-chats/unity/CertifiedMotionPublicationR4';D=R/'docs/agent-runs/20261104-three-chats/unity/CertifiedMotionPublicationR4'
rows=json.loads((D/'inputs.json').read_text());supp=json.loads((D/'supplementary-inputs.json').read_text());check=[]
for x in rows+supp:
 p=Path(x['snapshot']);src=R/x['source'];check.append(dict(source=x['source'],snapshotValid=hashlib.sha256(p.read_bytes()).hexdigest()==x['sha256'],originalNow=hashlib.sha256(src.read_bytes()).hexdigest() if src.exists() else None,expected=x['sha256']))
(D/'input-after.json').write_text(json.dumps(check,indent=2))
idx=json.loads((O/'before/docs/agent-runs/20261104-three-chats/unity/phase21/source-index.json').read_text());(D/'phase21-comparison.json').write_text(json.dumps([dict(path=x['path'],pinned=x['sha256'],now=hashlib.sha256((R/x['path']).read_bytes()).hexdigest() if (R/x['path']).exists() else None) for x in idx],indent=2))
accepted=[]
for p in (O/'before/docs/agent-runs/20261104-three-chats/unity').glob('accepted-neutral-v*.json'):
 obj=json.loads(p.read_text(encoding='utf-8-sig'));accepted.append(dict(path=p.name,sha256=hashlib.sha256(p.read_bytes()).hexdigest(),rootType=type(obj).__name__,keys=list(obj) if isinstance(obj,dict) else None,entries=len(obj)))
(D/'accepted-context.json').write_text(json.dumps(accepted,indent=2))
symbols=[]
for rel in ['unity/Assets/Ashfall/Integration/MotionAuthority/MotionPublicationPreparation.cs','unity/Assets/Ashfall/Integration/MotionAuthority/HostMotionArbiter.cs','unity/Assets/Ashfall/Integration/FlightMotor/PreparedPlayerFlightStep.cs','unity/Assets/Ashfall/Integration/ClimbingMotor/ClimbingRootMotor.cs','unity/Assets/Ashfall/Modules/Climbing/PreparedClimbMotion.cs','unity/Assets/Ashfall/Modules/Player/Engine/UnityExclusivePlayerMotor.cs','unity/Assets/Ashfall/Modules/Flight/PadContacts.cs']:
 p=R/rel
 if not p.exists():continue
 symbols.append(dict(source=rel,sha256=hashlib.sha256(p.read_bytes()).hexdigest(),symbols=[dict(line=i,text=l.strip()) for i,l in enumerate(p.read_text().splitlines(),1) if any(v in l for v in ['Prepare','Apply(','Step(','Validate(','Publish','TryLaunch','Commit','controller.Move'])]))
(D/'source-symbols.json').write_text(json.dumps(symbols,indent=2));print('immutable verified',sum(x['snapshotValid'] for x in check),'/',len(check),'phase21 entries',len(idx))

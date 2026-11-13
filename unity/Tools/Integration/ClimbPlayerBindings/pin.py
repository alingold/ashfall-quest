from pathlib import Path
import hashlib,json,shutil
R=Path(r'C:\Users\aling\Documents\ashfall')
S=R/'campaign-staging/20261104-three-chats/unity/ClimbRuntimeBindingsR8'
D=R/'docs/agent-runs/20261104-three-chats/unity/ClimbRuntimeBindingsR8'
S.mkdir(parents=True,exist_ok=True);D.mkdir(parents=True,exist_ok=True)
files=[R/'unity/Assets/Ashfall/Core/Contracts.cs']
for part in ['Modules/Climbing','Modules/Player','Modules/Flight','Integration/FlightMotor','Integration/ClimbingMotor']:
 files+=list((R/'unity/Assets/Ashfall'/part).rglob('*.cs'))
files+=list((R/'unity/Tools/Integration/ClimbingMotor').rglob('*.cs'))
files+=list((R/'unity/Documentation').glob('*.md'))
U=R/'docs/agent-runs/20261104-three-chats/unity'
files+=list(U.glob('accepted-neutral-v*.json'))+[U/'phase15/source-index.json',U/'phase15/dispatch-plan.json',U/'phase15/session-identity-validation.json',U/'GAMEPLAY-CHECKPOINT-v1.md']
files+=list((U/'reviews/ClimbingTrajectoryReviewR7').glob('*.md'))
rows=[]
for p in files:
 q=S/'inputs'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True)
 if q.exists():raise RuntimeError('immutable snapshot exists '+str(q))
 shutil.copyfile(p,q)
 rows.append(dict(original=str(p.relative_to(R)),snapshot=str(q.relative_to(R)),sha256=hashlib.sha256(q.read_bytes()).hexdigest(),bytes=q.stat().st_size))
(D/'inputs.json').write_text(json.dumps(rows,indent=2))
for p in files:
 if p.suffix=='.json':
  x=json.loads(p.read_text(encoding='utf-8-sig'));print(p.name, list(x)[:12] if isinstance(x,dict) else 'records '+str(len(x)))
print('pinned',len(rows))

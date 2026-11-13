from pathlib import Path
import json,hashlib
R=Path(r'C:\Users\aling\Documents\ashfall'); O=R/'campaign-staging/20261104-three-chats/unity/MotionPreparedOwnersR3';D=R/'docs/agent-runs/20261104-three-chats/unity/MotionPreparedOwnersR3'
O.mkdir(exist_ok=True);D.mkdir(exist_ok=True)
items=[]
def pin(p,kind):
 b=p.read_bytes();q=O/'before'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);assert not q.exists() or q.read_bytes()==b;q.write_bytes(b);items.append(dict(source=str(p.relative_to(R)),snapshot=str(q),sha256=hashlib.sha256(b).hexdigest(),kind=kind))
idx=json.loads((R/'docs/agent-runs/20261104-three-chats/unity/phase16/source-index.json').read_text())
for x in idx:
 p=R/x['snapshot']; assert hashlib.sha256(p.read_bytes()).hexdigest().upper()==x['sha256'].upper()
for folder in ['unity/Assets/Ashfall/Core','unity/Assets/Ashfall/Modules/Flight','unity/Assets/Ashfall/Modules/Player','unity/Assets/Ashfall/Modules/Climbing','unity/Assets/Ashfall/Integration/FlightMotor','unity/Assets/Ashfall/Integration/ClimbingMotor','unity/Assets/Ashfall/Integration/MotionAuthority','unity/Tools/Integration/MotionAuthority','unity/Tools/Integration/FlightMotor','unity/Documentation']:
 for p in (R/folder).rglob('*'):
  if p.is_file():pin(p,'current-before')
for name in ['TWO-CHAT-20-AGENT-OVERRIDE.md','HILL-CLIMB-ORCHESTRATION.md','OVERNIGHT-AGENT-PLAN-2026-11-03.md','AGENT-DISPATCH-BRIEFS-2026-11-03.md','agent-runs/20261104-0020/preflight.json']:
 pin(R/'docs'/name,'context')
for p in (R/'docs/agent-runs/20261104-three-chats/unity').glob('accepted-neutral-v*.json'):
 pin(p,'context'); j=p.read_text(encoding='utf-8-sig');print(p.name,j[:300])
for name in ['phase16/dispatch-plan.json','phase16/source-index.json','HostMotionTransactionsR2/inputs.json','HostMotionTransactionsR2/REPORT.md','reviews/HostMotionArbitrationReviewR1/REPORT.md','GAMEPLAY-CHECKPOINT-v1.md']:
 pin(R/'docs/agent-runs/20261104-three-chats/unity'/name,'context')
(D/'inputs.json').write_text(json.dumps(items,indent=2));print('frozen',len(items),'phase16 verified',len(idx))


from pathlib import Path
import json,hashlib,shutil
R=Path(__file__).resolve().parents[4]
S=R/'campaign-staging/20261104-three-chats/unity/SourceDisposableCommitR2'
D=R/'docs/agent-runs/20261104-three-chats/unity/SourceDisposableCommitR2'
files=list((R/'unity/Documentation').glob('*.md'))
files += [R/'docs'/n for n in ['TWO-CHAT-20-AGENT-OVERRIDE.md','HILL-CLIMB-ORCHESTRATION.md','OVERNIGHT-AGENT-PLAN-2026-11-03.md','AGENT-DISPATCH-BRIEFS-2026-11-03.md','agent-runs/20261104-0020/preflight.json']]
U=R/'docs/agent-runs/20261104-three-chats/unity'
files += list(U.glob('accepted-neutral-v*.json'))+[U/'GAMEPLAY-CHECKPOINT-v1.md',U/'phase21/ROOT-REPORT.md',U/'phase21/source-index.json',U/'phase27/ROOT-REPORT.md']
for name in ['Core','RecoveryContracts','Modules/InventoryItems','Modules/WeaponHandling']:
 files += list((R/'unity/Assets/Ashfall'/name).glob('*.cs'))
files += list((R/'unity/Assets/Ashfall/Integration/SourceExplosiveAmmo').glob('*.cs'))
out=[]
for p in files:
 b=p.read_bytes();dest=S/'before'/p.relative_to(R);dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(b)
 out.append(dict(path=str(p.relative_to(R)),sha256=hashlib.sha256(b).hexdigest(),bytes=len(b)))
(D/'before-manifest.json').write_text(json.dumps(out,indent=2))
print('Pinned',len(out),'files; policy text read in full; accepted JSON parsed:',[len(json.loads(p.read_text(encoding='utf-8-sig'))) for p in U.glob('accepted-neutral-v*.json')])

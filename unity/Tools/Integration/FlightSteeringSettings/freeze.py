from pathlib import Path
import hashlib,json,shutil
r=Path.cwd(); s=r/'campaign-staging/20261104-three-chats/unity/FlightSteeringSettingsR1'; d=r/'docs/agent-runs/20261104-three-chats/unity/FlightSteeringSettingsR1'
paths=list((r/'unity/Assets/Ashfall').rglob('*.cs'))+list((r/'unity/Documentation').glob('*.md'))
paths+=list((r/'docs/agent-runs/20261104-three-chats/unity').glob('accepted-neutral-v*.json'))
paths += [r/x for x in ['docs/TWO-CHAT-20-AGENT-OVERRIDE.md','docs/HILL-CLIMB-ORCHESTRATION.md','docs/OVERNIGHT-AGENT-PLAN-2026-11-03.md','docs/AGENT-DISPATCH-BRIEFS-2026-11-03.md','docs/agent-runs/20261104-0020/preflight.json']]
for folder in ['phase21','PresentationR6','PresentationR7','PresentationLiveBindingsR9','reviews/PresentationLiveReviewR9']:
 paths+=list((r/'docs/agent-runs/20261104-three-chats/unity'/folder).glob('*.md'))+list((r/'docs/agent-runs/20261104-three-chats/unity'/folder).glob('source-index.json'))
paths+= [r/'docs/agent-runs/20261104-three-chats/unity/GAMEPLAY-CHECKPOINT-v1.md',r/'game/scripts/player.gd',r/'game/scripts/menu.gd']
paths+=list((r/'unity/Tools/Integration/PreparedPadOwner').glob('*'))
rows=[]
for p in sorted(set(paths)):
 if not p.is_file():continue
 raw=p.read_bytes();q=s/'before'/p.relative_to(r);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(raw);rows.append(dict(path=str(p.relative_to(r)),sha256=hashlib.sha256(raw).hexdigest(),bytes=len(raw)))
(d/'before.json').write_text(json.dumps(rows,indent=2));print('Frozen',len(rows),'inputs')

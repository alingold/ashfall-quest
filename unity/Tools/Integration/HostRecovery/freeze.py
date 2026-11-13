from pathlib import Path
import hashlib,json,shutil
R=Path.cwd(); D=R/'docs/agent-runs/20261104-three-chats/unity/CheckpointBundleR1'; S=R/'campaign-staging/20261104-three-chats/unity/CheckpointBundleR1/before';S.mkdir(exist_ok=False)
paths=list((R/'unity/Assets/Ashfall').rglob('*.cs'))+list((R/'unity/Documentation').glob('*.md'))+list((R/'unity/Tools/Integration/MatchRecovery').glob('*.*'))
paths+=list((R/'docs/agent-runs/20261104-three-chats/unity').glob('accepted-neutral-v*.json'))
for f in ['docs/TWO-CHAT-20-AGENT-OVERRIDE.md','docs/HILL-CLIMB-ORCHESTRATION.md','docs/OVERNIGHT-AGENT-PLAN-2026-11-03.md','docs/AGENT-DISPATCH-BRIEFS-2026-11-03.md','docs/agent-runs/20261104-0020/preflight.json']:paths.append(R/f)
base=R/'docs/agent-runs/20261104-three-chats/unity'
for sub in ['MatchStateRecoveryR10','HealthArchiveRecordsR1','InventoryCheckpointR2','ConsumableCustodyRecoveryR5','HostEpochAuthorityR1','phase21']:
 for name in ['REPORT.md','ROOT-REPORT.md','API-REQUESTS.md','source-index.json']:
  if (base/sub/name).is_file():paths.append(base/sub/name)
paths.append(base/'GAMEPLAY-CHECKPOINT-v1.md');rows=[]
for p in paths:
 b=p.read_bytes();q=S/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b);rows.append(dict(path=p.relative_to(R).as_posix(),snapshot=q.relative_to(R).as_posix(),bytes=len(b),sha256=hashlib.sha256(b).hexdigest()))
(D/'inputs.json').write_text(json.dumps(rows,indent=2));print('pinned',len(rows))
for p in base.glob('accepted-neutral-v*.json'):
 b=p.read_bytes();t=b.decode('utf-16') if b[:2] in [b'\xff\xfe',b'\xfe\xff'] else b.decode('utf-8-sig');v=json.loads(t);print(p.name,str(v)[:700])

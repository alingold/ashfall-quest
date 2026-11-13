from pathlib import Path
import hashlib,json,shutil
R=Path.cwd(); S=R/'campaign-staging/20261104-three-chats/unity/CheckpointClockEvidenceR2'; D=R/'docs/agent-runs/20261104-three-chats/unity/CheckpointClockEvidenceR2'
paths=list((R/'unity/Assets/Ashfall').rglob('*.cs'))+list((R/'unity/Documentation').glob('*.md'))
paths+=list((R/'unity/Tools/Integration/HostRecovery').glob('*'))
B=R/'docs/agent-runs/20261104-three-chats/unity'
paths+=list(B.glob('accepted-neutral-v*.json'))+[B/'GAMEPLAY-CHECKPOINT-v1.md',B/'phase21/ROOT-REPORT.md',B/'phase21/source-index.json',B/'phase27/ROOT-REPORT.md',B/'reviews/CheckpointBundleReviewR1/REPORT.md']
paths+=[R/'campaign-staging/20261104-three-chats/unity/reviews/CheckpointBundleReviewR1/Independent.cs']
rows=[]
for p in paths:
 if not p.is_file():continue
 b=p.read_bytes();q=S/'before'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True)
 with q.open('xb') as f:f.write(b)
 rows.append(dict(path=p.relative_to(R).as_posix(),sha256=hashlib.sha256(b).hexdigest(),bytes=len(b)))
(D/'inputs.json').write_text(json.dumps(rows,indent=2))
print('frozen',len(rows))

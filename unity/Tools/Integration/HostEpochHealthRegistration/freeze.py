from pathlib import Path
import hashlib,json,shutil
R=Path.cwd(); S=R/'campaign-staging/20261104-three-chats/unity/HostEpochHealthRegistrationR2'; D=R/'docs/agent-runs/20261104-three-chats/unity/HostEpochHealthRegistrationR2'
S.mkdir(exist_ok=True);D.mkdir(exist_ok=True)
files=list((R/'unity/Assets/Ashfall').rglob('*.cs'))+list((R/'unity/Tools/Integration/HostEpochAuthority').glob('*.*'))+list((R/'unity/Tools/Modules/HealthStates').glob('*.*'))
# Reviewer fixtures are immutable test inputs, not executable drivers.
files+=list((R/'campaign-staging/20261104-three-chats/unity/reviews/HostEpochAuthorityReviewR1').rglob('*.cs'))
files+=list((R/'campaign-staging/20261104-three-chats/unity/reviews/HealthEpochReviewR2').glob('*.cs'))
rows=[]
for p in files:
 if not p.is_file():continue
 q=S/'before'/p.relative_to(R) if 'campaign-staging' not in p.parts else S/'fixtures'/ (hashlib.sha256(str(p).encode()).hexdigest()[:10]+'-'+p.name);q.parent.mkdir(parents=True,exist_ok=True)
 if q.exists():assert q.read_bytes()==p.read_bytes()
 else:shutil.copyfile(p,q)
 rows.append(dict(source=str(p),snapshot=str(q),sha256=hashlib.sha256(q.read_bytes()).hexdigest()))
(D/'before.json').write_text(json.dumps(rows,indent=2))
base=R/'docs/agent-runs/20261104-three-chats/unity'
context=list((R/'unity/Documentation').glob('*.md'))+list(base.glob('accepted-neutral-v*.json'))+[base/'GAMEPLAY-CHECKPOINT-v1.md',base/'phase21/source-index.json']
for phase in ['phase18','phase21','phase25']:context.append(base/phase/'ROOT-REPORT.md')
receipts=[]
for p in context:
 b=p.read_bytes(); v=json.loads(b) if p.suffix=='.json' else b.decode('utf-8-sig',errors='replace')
 receipts.append(dict(path=str(p),sha256=hashlib.sha256(b).hexdigest(),entries=len(v),content=v if p.name=='GAMEPLAY-CHECKPOINT-v1.md' else None))
(D/'context.json').write_text(json.dumps(receipts,indent=2));print('frozen',len(rows),'context',len(receipts)); print((base/'GAMEPLAY-CHECKPOINT-v1.md').read_text())


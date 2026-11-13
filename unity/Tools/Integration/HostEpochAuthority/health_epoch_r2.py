from pathlib import Path
import json, hashlib, shutil, subprocess
R=Path(r'C:\Users\aling\Documents\ashfall')
D=R/'docs/agent-runs/20261104-three-chats/unity/HealthEpochTokensR2'
S=R/'campaign-staging/20261104-three-chats/unity/HealthEpochTokensR2'
def freeze():
 D.mkdir(exist_ok=True);S.mkdir(exist_ok=True)
 files=list((R/'unity/Assets/Ashfall').rglob('*.cs'))
 files+=list((R/'unity/Tools/Integration/HostEpochAuthority').glob('*'))+list((R/'unity/Tools/Modules/HealthStates').glob('*'))
 rows=[]
 for p in files:
  if not p.is_file():continue
  q=S/'before'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q)
  rows.append(dict(source=str(p),snapshot=str(q),sha256=hashlib.sha256(q.read_bytes()).hexdigest()))
 (D/'before.json').write_text(json.dumps(rows,indent=2))
 base=R/'docs/agent-runs/20261104-three-chats/unity'
 docs=list((R/'unity/Documentation').glob('*.md'))+list(base.glob('accepted-neutral-v*.json'))
 for phase in ['phase16','phase18','phase19','HostEpochAuthorityR1','HealthArchiveRecordsR1','reviews/HostEpochAuthorityReviewR1']:
  docs+=list((base/phase).glob('*.json'))+list((base/phase).glob('*.md'))
 receipts=[]
 for p in docs:
  b=p.read_bytes();v=json.loads(b) if p.suffix=='.json' else b.decode('utf-8-sig',errors='replace')
  receipts.append(dict(path=str(p),sha256=hashlib.sha256(b).hexdigest(),summary=list(v) if isinstance(v,dict) else len(v)))
 (D/'context-reads.json').write_text(json.dumps(receipts,indent=2))
 print('frozen',len(rows),'read',len(receipts))
if __name__=='__main__':freeze()


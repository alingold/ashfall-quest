from pathlib import Path
import json,hashlib,shutil
R=Path.cwd(); S=R/'campaign-staging/20261104-three-chats/unity/RuntimeLoopBindingsR4'; E=R/'docs/agent-runs/20261104-three-chats/unity/RuntimeLoopBindingsR4'
S.mkdir(exist_ok=False);E.mkdir(exist_ok=False)
rows=json.loads((R/'docs/agent-runs/20261104-three-chats/unity/phase15/source-index.json').read_text(encoding='utf-8-sig'))
for x in rows:
 p=R/x['snapshot']; assert hashlib.sha256(p.read_bytes()).hexdigest().upper()==x['sha256'].upper()
 d=S/'pinned'/x['original'];d.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,d)
for folder in ['unity/Assets/Ashfall/Integration/RuntimeLoop','unity/Tools/Integration/RuntimeLoop']:
 for p in (R/folder).rglob('*'):
  if p.is_file():
   d=S/'before'/p.relative_to(R);d.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,d)
reads=[]
for pat in ['docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v*.json','docs/agent-runs/20261104-three-chats/unity/phase15/*validation.json','docs/agent-runs/20261104-three-chats/unity/phase15/dispatch-plan.json']:
 for p in R.glob(pat):
  data=json.loads(p.read_text(encoding='utf-8-sig'));reads.append({'path':str(p.relative_to(R)),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'keys':list(data)[:20] if isinstance(data,dict) else 'list','scope':'bounded frozen acceptance only'})
(E/'administrative-reads.json').write_text(json.dumps(reads,indent=2));(E/'pin.json').write_text(json.dumps(rows,indent=2))
print('Pinned',len(rows),'small C# sources; prior owned bytes preserved; parsed',len(reads),'records')

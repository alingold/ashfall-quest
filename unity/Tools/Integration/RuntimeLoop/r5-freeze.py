from pathlib import Path
import json,hashlib,shutil
R=Path.cwd(); S=R/'campaign-staging/20261104-three-chats/unity/MotionRuntimeLoopCompositionR5'; E=R/'docs/agent-runs/20261104-three-chats/unity/MotionRuntimeLoopCompositionR5'
S.mkdir(exist_ok=False); E.mkdir(exist_ok=False)
rows=[]
for folder in ['unity/Assets/Ashfall','unity/Tools/Integration/RuntimeLoop']:
 for p in (R/folder).rglob('*'):
  if p.is_file() and (p.suffix in ['.cs','.py','.md']) and 'Desktop' not in p.parts:
   rel=p.relative_to(R); d=S/'pinned'/rel;d.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,d)
   rows.append(dict(path=rel.as_posix(),snapshot=d.relative_to(R).as_posix(),sha256=hashlib.sha256(d.read_bytes()).hexdigest()))
reads=[]
for p in sorted((R/'docs/agent-runs/20261104-three-chats/unity').glob('accepted-neutral-v*.json')):
 data=json.loads(p.read_text(encoding='utf-8-sig'));reads.append(dict(path=str(p.relative_to(R)),sha256=hashlib.sha256(p.read_bytes()).hexdigest(),keys=list(data),status=data.get('status'),scope=data.get('scope')))
(E/'inputs.json').write_text(json.dumps(rows,indent=2));(E/'accepted-reads.json').write_text(json.dumps(reads,indent=2)); print(len(rows),'inputs frozen',len(reads),'accepted records parsed')

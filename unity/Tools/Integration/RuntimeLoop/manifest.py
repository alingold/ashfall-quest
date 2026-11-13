from pathlib import Path
import hashlib,json
root=Path(r'C:\Users\aling\Documents\ashfall');base=root/'campaign-staging/20261104-three-chats/unity/RuntimeLoopR1/r5';ev=root/'docs/agent-runs/20261104-three-chats/unity/RuntimeLoopR1/r5'
paths=[]
for rel in ['unity/Assets/Ashfall/Integration/RuntimeLoop','unity/Tools/Integration/RuntimeLoop','campaign-staging/20261104-three-chats/unity/RuntimeLoopR1','docs/agent-runs/20261104-three-chats/unity/RuntimeLoopR1']:
 paths.extend(p for p in (root/rel).rglob('*') if p.is_file() and p.name not in ['frozen-manifest.json','frozen-manifest.sha256'])
records=[{'path':p.relative_to(root).as_posix(),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'bytes':p.stat().st_size} for p in sorted(paths)]
p=ev/'frozen-manifest.json';p.write_text(json.dumps(records,indent=2));digest=hashlib.sha256(p.read_bytes()).hexdigest();(ev/'frozen-manifest.sha256').write_text(digest+'\n');print('FROZEN',digest,len(records),'files')

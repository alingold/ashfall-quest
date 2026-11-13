from pathlib import Path
import json,hashlib
R=Path(r'C:\Users\aling\Documents\ashfall');D=R/'docs/agent-runs/20261104-three-chats/unity/SessionReplicationR5'
roots=['unity/Assets/Ashfall/Integration/SessionReplication','unity/Tools/Integration/SessionReplication/r5','docs/agent-runs/20261104-three-chats/unity/SessionReplicationR5','campaign-staging/20261104-three-chats/unity/SessionReplicationR5']
rows=[]
for root in roots:
 for p in sorted((R/root).rglob('*')):
  if p.is_file() and p.name not in ['candidate-manifest.json','manifest.sha256.txt']:
   rows.append(dict(path=str(p.relative_to(R)),sha256=hashlib.sha256(p.read_bytes()).hexdigest().upper(),bytes=p.stat().st_size))
p=D/'candidate-manifest.json';p.write_text(json.dumps(dict(status='UNREVIEWED',final_attempt='attempt-04',files=rows),indent=2));(D/'manifest.sha256.txt').write_text(hashlib.sha256(p.read_bytes()).hexdigest().upper()+'  candidate-manifest.json\n');print('Manifest',len(rows),'files')

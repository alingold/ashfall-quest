from pathlib import Path
import json,hashlib
R=Path.cwd();D=R/'docs/agent-runs/20261104-three-chats/unity/CheckpointBundleR1';S=R/'campaign-staging/20261104-three-chats/unity/CheckpointBundleR1';rows=json.loads((D/'inputs.json').read_text());assert all(hashlib.sha256((R/r['snapshot']).read_bytes()).hexdigest()==r['sha256'] for r in rows)
entries=[]
for base in [D,S,R/'unity/Assets/Ashfall/Integration/HostRecovery',R/'unity/Tools/Integration/HostRecovery']:
 for p in base.rglob('*'):
  if p.is_file() and p.name!='SHA256-manifest.json':entries.append(dict(path=p.relative_to(R).as_posix(),bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
for p in [Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'),Path(r'C:\Users\aling\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe')]:entries.append(dict(path=str(p),bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
(D/'SHA256-manifest.json').write_text(json.dumps(entries,indent=2));print('sealed',len(entries),'files; pinned inputs intact',len(rows));print('drift',len(json.loads((D/'final07-drift.json').read_text())))

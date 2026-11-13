import json,hashlib
from pathlib import Path
R=Path.cwd();D=R/'docs/agent-runs/20261104-three-chats/unity/ConstructionRuntimeOwnerR5';dirs=[D,R/'campaign-staging/20261104-three-chats/unity/ConstructionRuntimeOwnerR5',R/'unity/Tools/Integration/ConstructionRuntimeOwner',R/'unity/Assets/Ashfall/Integration/ConstructionRuntime',R/'unity/Assets/Ashfall/Integration/ConstructionTransactions'];rows=[]
for d in dirs:
 for p in sorted(d.rglob('*')):
  if p.is_file() and p.name!='MANIFEST.json':rows.append(dict(path=p.relative_to(R).as_posix(),bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
(D/'MANIFEST.json').write_text(json.dumps(rows,indent=2));print('manifest files',len(rows))

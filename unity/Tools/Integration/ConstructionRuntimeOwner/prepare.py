import json,subprocess,hashlib
from pathlib import Path
R=Path.cwd();B=R/'campaign-staging/20261104-three-chats/unity/ConstructionRuntimeOwnerR5'; O=B/'run02';O.mkdir(exist_ok=False)
rows=json.loads((B/'before.json').read_text()); lookup={}
for r in rows:
 p=R/r['path'];b=p.read_bytes() if '/Integration/ConstructionTransactions/' in r['path'] else Path(r['snapshot']).read_bytes();q=O/'sources'/r['path'];q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b);lookup[r['path']]=q
p=R/'unity/Assets/Ashfall/Integration/ConstructionTransactions/PreparedRuntime.cs';q=O/'sources'/p.relative_to(R);q.write_bytes(p.read_bytes());lookup[p.relative_to(R).as_posix()]=q
(O/'inputs.json').write_text(json.dumps([dict(original=k,snapshot=str(v),sha256=hashlib.sha256(v.read_bytes()).hexdigest()) for k,v in lookup.items()],indent=2))

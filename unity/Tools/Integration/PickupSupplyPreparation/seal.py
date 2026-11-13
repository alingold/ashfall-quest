import json,hashlib
from pathlib import Path
R=Path.cwd();D=R/'docs/agent-runs/20261104-three-chats/unity/PickupSupplyPreparationR1';B=R/'campaign-staging/20261104-three-chats/unity/PickupSupplyPreparationR1';roots=[D,B,R/'unity/Tools/Integration/PickupSupplyPreparation',R/'unity/Assets/Ashfall/Modules/Pickups'];paths=[]
for root in roots:paths+=list(root.rglob('*'))
paths += [R/'unity/Assets/Ashfall/Integration/ConstructionTransactions'/n for n in ['PreparedRuntime.cs','PreparedSupplyGrant.cs']]
rows=[dict(path=p.relative_to(R).as_posix(),bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in sorted(set(paths)) if p.is_file() and p.name!='MANIFEST.json'];(D/'MANIFEST.json').write_text(json.dumps(rows,indent=2));print('manifest',len(rows))

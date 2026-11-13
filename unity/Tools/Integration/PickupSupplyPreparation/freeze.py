import json,hashlib
from pathlib import Path
R=Path.cwd();O=R/'campaign-staging/20261104-three-chats/unity/PickupSupplyPreparationR1';O.mkdir(exist_ok=False,parents=True)
folders=['Core','RecoveryContracts']
paths=[]
for f in folders: paths+=list((R/'unity/Assets/Ashfall'/f).rglob('*.cs'))
for f in ['Player','Sessions','HealthStates','Combat','World','Construction','ConstructionCandidate','InventoryItems','WeaponHandling','Pickups']:paths+=list((R/'unity/Assets/Ashfall/Modules'/f).rglob('*.cs'))
for f in ['ConstructionRuntime','ConstructionTransactions','ConstructionGeometry','LifecycleTransactions','SessionReplication']:paths+=list((R/'unity/Assets/Ashfall/Integration'/f).rglob('*.cs'))
paths+=list((R/'unity/Documentation').glob('*.md'))
for f in ['ConstructionRuntimeOwner','ConstructionTransactions']:paths+=list((R/'unity/Tools/Integration'/f).rglob('*.*'))
paths+=list((R/'unity/Tools').rglob('*Pickup*.cs'))
paths+=list((R/'docs/agent-runs/20261104-three-chats/unity').glob('accepted-neutral-v*.json'))
paths+=[R/'docs/agent-runs/20261104-three-chats/unity/phase21/source-index.json']
rows=[]
for p in sorted(set(paths)):
 b=p.read_bytes();q=O/'before'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b);rows.append(dict(path=p.relative_to(R).as_posix(),sha256=hashlib.sha256(b).hexdigest()))
(O/'before.json').write_text(json.dumps(rows,indent=2));print(len(rows))

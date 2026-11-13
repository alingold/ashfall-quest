import json,hashlib
from pathlib import Path
R=Path.cwd();O=R/'campaign-staging/20261104-three-chats/unity/ConstructionRuntimeOwnerR5'; rows=[]
folders=['unity/Assets/Ashfall/Core','unity/Assets/Ashfall/RecoveryContracts']+['unity/Assets/Ashfall/Modules/'+x for x in ['Player','Sessions','HealthStates','World','Construction','ConstructionCandidate','InventoryItems','WeaponHandling','Pickups','Combat']]+['unity/Assets/Ashfall/Integration/'+x for x in ['ConstructionRuntime','ConstructionTransactions','ConstructionGeometry','LifecycleTransactions','SessionReplication']]
for f in folders:
 for p in (R/f).rglob('*.cs'):
  b=p.read_bytes();q=O/'before'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b);rows.append(dict(path=p.relative_to(R).as_posix(),snapshot=str(q),sha256=hashlib.sha256(b).hexdigest()))
for p in list((R/'unity/Documentation').glob('*.md'))+list((R/'docs/agent-runs/20261104-three-chats/unity').glob('accepted-neutral-v*.json'))+list((R/'docs/agent-runs/20261104-three-chats/unity/phase21').glob('*.json')):
 b=p.read_bytes();q=O/'before'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b)
(O/'before.json').write_text(json.dumps(rows,indent=2)); print('Pinned',len(rows),'C# sources')

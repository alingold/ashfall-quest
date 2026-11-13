from pathlib import Path
import json, hashlib, shutil
r=Path.cwd(); d=r/'docs/agent-runs/20261104-three-chats/unity/HostEpochAuthorityR1'; s=r/'campaign-staging/20261104-three-chats/unity/HostEpochAuthorityR1'
assert not (d/'inputs.json').exists()
files=[r/'unity/Assets/Ashfall/Core/Contracts.cs']
for area in ['Modules/HealthStates','Modules/Combat','Modules/InventoryItems','Modules/WeaponHandling','Modules/Sessions','Modules/Persistence','Integration/InventoryRecovery','Integration/CombatPublication']:
 files+=list((r/'unity/Assets/Ashfall'/area).glob('*.cs'))
files+=[r/'unity/Assets/Ashfall/Integration/CombatHealthBridge.cs']
records=[]
for i,p in enumerate(files):
 b=p.read_bytes(); q=s/'inputs'/('%03d-'%i+p.name);q.parent.mkdir(exist_ok=True);q.write_bytes(b)
 records.append(dict(source=str(p),snapshot=str(q),sha256=hashlib.sha256(b).hexdigest()))
(d/'inputs.json').write_text(json.dumps(records,indent=2))
reads=[]
base=r/'docs/agent-runs/20261104-three-chats/unity'
for p in list(base.glob('accepted-neutral-v*.json'))+list((base/'phase15').glob('*.json')):
 b=p.read_bytes();obj=json.loads(b);reads.append(dict(path=str(p),sha256=hashlib.sha256(b).hexdigest(),keys=list(obj) if isinstance(obj,dict) else len(obj),status=obj.get('status') if isinstance(obj,dict) else None))
(d/'administrative-reads.json').write_text(json.dumps(reads,indent=2))
print('Pinned',len(records),'small sources; read',len(reads),'root receipts')

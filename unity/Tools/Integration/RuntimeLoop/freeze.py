from pathlib import Path
import hashlib,json
root=Path(r'C:\Users\aling\Documents\ashfall')
base=root/'campaign-staging/20261104-three-chats/unity/RuntimeLoopR1/r5'
mods=['Player','World','Flight','Combat','HealthStates','InventoryItems','Pickups','WeaponHandling','Modes','Sessions','Networking','Audio','Presentation']
paths=[root/'unity/Assets/Ashfall/Core/Contracts.cs']
for m in mods:paths+=sorted((root/('unity/Assets/Ashfall/Modules/'+m)).glob('*.cs'))
for m in ['FlightMotor','InventoryTransfers','MatchDirector']:paths+=sorted((root/('unity/Assets/Ashfall/Integration/'+m)).glob('*.cs'))
paths+=[root/'unity/Assets/Ashfall/Integration/CombatHealthBridge.cs']
raise SystemExit('Development capture disabled: candidate sealed; new revision required')
records=[]
for p in paths:
 d=p.read_bytes();rel=p.relative_to(root);out=base/'inputs'/rel;out.parent.mkdir(parents=True,exist_ok=True);out.write_bytes(d)
 records.append({'path':rel.as_posix(),'sha256':hashlib.sha256(d).hexdigest(),'bytes':len(d)})
(base/'dependencies.json').write_text(json.dumps(records,indent=2))
print('Pinned',len(records),'complete source files',sum(x['bytes'] for x in records),'bytes')

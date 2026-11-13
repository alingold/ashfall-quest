from pathlib import Path
import hashlib,json
r=Path.cwd(); out=r/'campaign-staging/20261104-three-chats/unity/ParityExpansion/r3/baseline-inputs'
out.mkdir(parents=True,exist_ok=False)
sources=[r/'unity/Assets/Ashfall/Core/Contracts.cs',r/'unity/Assets/Ashfall/Integration/CombatHealthBridge.cs',r/'unity/Tools/ToolchainProbe/ConversionProbe.cs']
for m in ['ParityHarness','Flight','Sessions','Networking','Pickups','Construction','WeaponHandling','Combat','HealthStates']:
 sources+=sorted((r/('unity/Assets/Ashfall/Modules/'+m)).glob('*.cs'))
sources+=sorted((r/'unity/Tools/Modules/ParityHarness').glob('*.cs'))+sorted((r/'unity/Tools/Modules/ParityHarness/Integration').glob('*.cs'))
records=[]
for p in sources:
 d=p.read_bytes(); rel=p.relative_to(r); dest=out/rel;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(d)
 records.append({'path':rel.as_posix(),'sha256':hashlib.sha256(d).hexdigest()})
(out.parent/'baseline-manifest.json').write_text(json.dumps(records,indent=2))
print('Frozen',len(records),'actual dependency inputs')

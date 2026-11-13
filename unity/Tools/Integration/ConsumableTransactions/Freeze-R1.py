from pathlib import Path
import json,hashlib,shutil
R=Path(r'C:\Users\aling\Documents\ashfall');O=R/'campaign-staging/20261104-three-chats/unity/ConsumableTransactionsR1';E=R/'docs/agent-runs/20261104-three-chats/unity/ConsumableTransactionsR1';F=O/'r5/final';D=E/'r5';D.mkdir(parents=True,exist_ok=True)
h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
files=[]
for group in ['unity/Assets/Ashfall/Modules/HealthStates','unity/Assets/Ashfall/Modules/InventoryItems','unity/Assets/Ashfall/Modules/Pickups','unity/Assets/Ashfall/Integration/ConsumableTransactions','unity/Tools/Integration/ConsumableTransactions']:
 for p in (R/group).glob('*'):
  if p.is_file():
   q=F/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,q);files.append({'path':str(p.relative_to(R)),'snapshot':str(q.relative_to(R)),'sha256':h(q),'bytes':q.stat().st_size})
drift=[]
for p in (O/'inputs/unity/Assets/Ashfall/Modules/Player').glob('*.cs'):
 live=R/'unity/Assets/Ashfall/Modules/Player'/p.name;drift.append({'path':str(live.relative_to(R)),'pinned':h(p),'current':h(live),'match':h(p)==h(live)})
for group in ['Core','Modules/HealthStates','Modules/InventoryItems','Modules/Pickups','Integration/ConsumableTransactions']:
 for p in (O/'inputs/unity/Assets/Ashfall'/group).glob('*.cs'):
  live=R/'unity/Assets/Ashfall'/group/p.name;drift.append({'path':str(live.relative_to(R)),'pinned':h(p),'current':h(live),'match':h(p)==h(live)})
(D/'dependency-drift.json').write_text(json.dumps(drift,indent=2));(D/'source-manifest.json').write_text(json.dumps(files,indent=2))
source_refs=[]
for rel in ['quest-r22/source/scripts/field_consumables.gd','game/scripts/field_consumables.gd','unity/Assets/Ashfall/Modules/Construction/ConstructionService.cs','unity/Assets/Ashfall/Modules/WeaponHandling/Ports.cs','unity/Assets/Ashfall/Integration/InventoryTransfers/Ports.cs']:
 p=R/rel;source_refs.append({'path':rel,'sha256':h(p)})
(D/'source-references.json').write_text(json.dumps(source_refs,indent=2))
print('files',len(files),'drift',sum(not x['match'] for x in drift))

from pathlib import Path
import hashlib,json,shutil
R=Path.cwd(); B=R/'campaign-staging/20261104-three-chats/unity/InventoryCheckpointR2'; D=R/'docs/agent-runs/20261104-three-chats/unity/InventoryCheckpointR2'
B.mkdir();D.mkdir()
def sha(b):return hashlib.sha256(b).hexdigest()
inputs=[]
paths=list((R/'unity/Assets/Ashfall/Core').glob('*.cs'))
for folder in ['Modules/InventoryItems','Modules/WeaponHandling','Integration/InventoryRecovery','RecoveryContracts','Integration/WeaponFireTransactions']:
 paths+=list((R/'unity/Assets/Ashfall'/folder).glob('*.cs'))
for folder in ['InventoryRecovery','WeaponFireTransactions']:
 paths+=list((R/'unity/Tools/Integration'/folder).glob('*'))
paths+=list((R/'unity/Documentation').glob('*.md'))
U=R/'docs/agent-runs/20261104-three-chats/unity'
paths += [U/('accepted-neutral-v%d.json'%i) for i in range(1,7)]+[U/'GAMEPLAY-CHECKPOINT-v1.md',U/'phase16/source-index.json',U/'phase16/dispatch-plan.json']
for n in ['InventoryRecoveryReviewR1','WeaponPreparedFireReviewR1']:
 paths+=list((U/'reviews'/n).glob('*.json'))+[U/'reviews'/n/'REPORT.md']
for p in paths:
 if not p.is_file():continue
 data=p.read_bytes();q=B/'before'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(data)
 inputs.append(dict(path=str(p.relative_to(R)),snapshot=str(q),sha256=sha(data)))
 # Read all administrative context, not only displayed excerpts.
 if p.suffix=='.json':json.loads(data.decode('utf-8-sig'))
(D/'before.json').write_text(json.dumps(inputs,indent=2))
(D/'registry-pin.json').write_text(json.dumps({'path':'unity/Assets/Ashfall/Modules/WeaponHandling/LifetimeRegistryCheckpoint.cs','present':False,'scope':'Absent at single initial observation; external owner not chased.'},indent=2))
def read(p):return (R/p).read_text()
def write(p,s):q=R/p;q.parent.mkdir(parents=True,exist_ok=True);q.write_text(s)
base='unity/Assets/Ashfall/'
wire=read(base+'Integration/InventoryRecovery/Wire.cs')
utility=wire[:wire.index('    // Provision through')]+ '}\n'
utility=utility.replace('namespace Ashfall.Integration.InventoryRecovery','namespace Ashfall.RecoveryContracts').replace('internal static class Wire','public static class RecoveryWire').replace('internal static','public static').replace('internal const','public const').replace('internal void','public void').replace('Wire.','RecoveryWire.')
write(base+'RecoveryContracts/RecoveryWire.cs',utility)
receipt=wire[wire.index('    // Provision through'):]
write(base+'Integration/InventoryRecovery/Wire.cs','using System;\nusing Ashfall.RecoveryContracts;\nusing Wire=Ashfall.RecoveryContracts.RecoveryWire;\nnamespace Ashfall.Integration.InventoryRecovery\n{\n'+receipt)
for folder in ['Modules/InventoryItems','Modules/WeaponHandling','Integration/InventoryRecovery']:
 for p in (R/base/folder).glob('*.cs'):
  if p.name not in ['CheckpointState.cs','FiniteAmmoReserve.cs','ColdInventoryCheckpoint.cs']:continue
  s=p.read_text().replace('using Ashfall.Integration.InventoryRecovery;','using Ashfall.RecoveryContracts;\nusing Wire=Ashfall.RecoveryContracts.RecoveryWire;').replace('Ashfall.Integration.InventoryRecovery.Wire.','Ashfall.RecoveryContracts.RecoveryWire.')
  if folder=='Integration/InventoryRecovery':
   s='using Ashfall.RecoveryContracts;\nusing Wire=Ashfall.RecoveryContracts.RecoveryWire;\n'+s
   s=s.replace('inventory.ExportCheckpoint(binding)','InventoryCheckpointCodec.Capture(inventory,binding)').replace('member.Handler.ExportCheckpoint(binding,member.ReserveProvenance)','WeaponCheckpointCodec.Capture(member.Handler,binding,member.ReserveProvenance)').replace('Inventory.RestoreCheckpoint(', 'InventoryCheckpointCodec.Restore(').replace('Handler.RestoreCheckpoint(', 'WeaponCheckpointCodec.Restore(')
   s=s.replace('// WeaponLifetimes, or the legacy untracked suspension/transfer tokens.','// WeaponLifetimes or external producers; this is local owner accounting only.')
  p.write_text(s)
for module,code in [('InventoryItems','''public static class InventoryCheckpointCodec
 {
  public static byte[] Capture(Inventory owner,CheckpointBinding binding){if(owner==null||binding==null)throw new ArgumentNullException();return owner.ExportCheckpoint(binding);}
  public static Inventory Restore(byte[] bytes,ItemCatalog catalog,IInventoryBoundary boundary,CheckpointBinding binding){if(binding==null||catalog==null||boundary==null)throw new ArgumentNullException();return Inventory.RestoreCheckpoint(bytes,catalog,boundary,binding);}
 }'''),('WeaponHandling','''public static class WeaponCheckpointCodec
 {
  public static byte[] Capture(WeaponHandling owner,CheckpointBinding binding,string provenance){if(owner==null||binding==null)throw new ArgumentNullException();return owner.ExportCheckpoint(binding,provenance);}
  public static WeaponHandling Restore(byte[] bytes,CheckpointBinding binding,string provenance,IAuthorityGate authority,out FiniteAmmoReserve reserve){if(binding==null||authority==null)throw new ArgumentNullException();return WeaponHandling.RestoreCheckpoint(bytes,binding,provenance,authority,out reserve);}
 }''')]:
 p=base+'Modules/'+module+'/CheckpointState.cs';s=read(p);pos=s.rfind('}');write(p,s[:pos]+code+'\n'+s[pos:])

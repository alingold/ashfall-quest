from pathlib import Path
import hashlib,json,shutil
root=Path(__file__).resolve().parents[5]
stage=root/'campaign-staging/20261104-three-chats/unity/SourceExplosiveMechanicsR3'
report=root/'docs/agent-runs/20261104-three-chats/unity/SourceExplosiveMechanicsR3'
coord=root/'docs/agent-runs/20261104-three-chats/unity'
paths=list((root/'unity/Documentation').glob('*.md'))+list(coord.glob('accepted-neutral-v*.json'))+[coord/'phase21/ROOT-REPORT.md',coord/'phase21/source-index.json',coord/'phase24/ROOT-REPORT.md',coord/'GAMEPLAY-CHECKPOINT-v1.md',coord/'SourceExplosiveAmmoR2/API-REQUESTS.md']
records=[]
for p in paths:
 data=p.read_bytes()
 if p.suffix=='.json':json.loads(data.decode('utf-8-sig'))
 else:
  try:data.decode('utf-8-sig')
  except UnicodeDecodeError:data.decode('cp1252')
 records.append(dict(path=str(p.relative_to(root)),bytes=len(data),sha256=hashlib.sha256(data).hexdigest()))
(report/'coordination-read-manifest.json').write_text(json.dumps(records,indent=2))
# Pin external owner inputs ONCE; subsequent owner drift is recorded separately.
external=stage/'external-owner-inputs'
if not external.exists():
 external.mkdir()
 for rel in ['unity/Assets/Ashfall/Modules/InventoryItems/PreparedRemoval.cs','unity/Assets/Ashfall/Modules/InventoryItems/PreparedWeaponHandoff.cs','unity/Assets/Ashfall/Modules/InventoryItems/CheckpointState.cs','unity/Assets/Ashfall/Integration/InventoryRecovery/ColdInventoryCheckpoint.cs']:
  p=root/rel;shutil.copy2(p,external/p.name)
current=root/'unity/Assets/Ashfall/Modules/WeaponHandling'
rows=[]
for p in sorted(current.glob('*.cs')):
 before=stage/'before'/p.name
 rows.append(dict(path=str(p.relative_to(root)),sha256=hashlib.sha256(p.read_bytes()).hexdigest(),beforeSha256=hashlib.sha256(before.read_bytes()).hexdigest() if before.exists() else None,changed=not before.exists() or before.read_bytes()!=p.read_bytes()))
(report/'owner-output-manifest.json').write_text(json.dumps(rows,indent=2))
print('Recorded',len(records),'coordination inputs and',len(rows),'owner source records')

from pathlib import Path
import json,hashlib
R=Path.cwd();B=R/'campaign-staging/20261104-three-chats/unity/InventoryCheckpointR2';D=R/'docs/agent-runs/20261104-three-chats/unity/InventoryCheckpointR2'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
idx=json.loads((B/'before/docs/agent-runs/20261104-three-chats/unity/phase16/source-index.json').read_text());checks=[]
for row in idx:
 p=R/row['snapshot'];checks.append({'path':str(p),'expected':row['sha256'].lower(),'actual':sha(p)})
(D/'phase16-verification.json').write_text(json.dumps(checks,indent=2));assert all(x['expected']==x['actual'] for x in checks)
schema=[]
for name in ['ServiceRifle','PrecisionRifle','MagnumRevolver']:
 old=R/'campaign-staging/20261104-three-chats/unity/InventoryRecoveryR1/final03'/(name+'.checkpoint');new=B/'final04'/(name+'.checkpoint')
 schema.append({'old':str(old),'new':str(new),'old_sha256':sha(old),'new_sha256':sha(new),'identical':old.read_bytes()==new.read_bytes()})
(D/'schema1-byte-identity.json').write_text(json.dumps(schema,indent=2));assert all(x['identical'] for x in schema)
before=json.loads((D/'before.json').read_text());changed=[]
for row in before:
 if not row['path'].startswith('unity'):continue
 p=R/row['path'];actual=sha(p)
 if actual!=row['sha256']:changed.append({'path':row['path'],'before':row['sha256'],'current':actual})
(D/'current-observation.json').write_text(json.dumps(changed,indent=2))
symbols=[]
for p in [R/'unity/Assets/Ashfall/RecoveryContracts/RecoveryWire.cs',R/'unity/Assets/Ashfall/Modules/InventoryItems/CheckpointState.cs',R/'unity/Assets/Ashfall/Modules/WeaponHandling/CheckpointState.cs',R/'unity/Assets/Ashfall/Modules/WeaponHandling/PreparedWeapons.cs',R/'unity/Assets/Ashfall/Modules/WeaponHandling/PreparedLifetimes.cs',R/'unity/Assets/Ashfall/Modules/WeaponHandling/PreparedFire.cs']:
 for n,line in enumerate(p.read_text().splitlines(),1):
  if any(s in line for s in ['public static','ReserveOperation(','ReleaseOperation(','public RuleResult','public void Commit','public void Abort']):symbols.append({'file':str(p),'line':n,'symbol':line.strip(),'sha256':sha(p)})
(D/'source-symbols.json').write_text(json.dumps(symbols,indent=2))
manifest=[]
for folder in [B,D,R/'unity/Tools/Integration/InventoryRecovery',R/'unity/Tools/Integration/WeaponFireTransactions',R/'unity/Assets/Ashfall/RecoveryContracts',R/'unity/Assets/Ashfall/Integration/InventoryRecovery']:
 for p in folder.rglob('*'):
  if p.is_file() and p.name!='SHA256-manifest.json':manifest.append({'path':str(p.relative_to(R)),'sha256':sha(p),'bytes':p.stat().st_size})
for row in changed:
 p=R/row['path'];manifest.append({'path':row['path'],'sha256':sha(p),'bytes':p.stat().st_size})
(D/'SHA256-manifest.json').write_text(json.dumps(manifest,indent=2));print('phase16',len(checks),'schema byte identity',len(schema),'sealed',len(manifest))

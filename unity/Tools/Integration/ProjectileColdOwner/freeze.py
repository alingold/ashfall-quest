import pathlib,hashlib,json,shutil
R=pathlib.Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/ProjectileColdOwnerR5';D=R/'docs/agent-runs/20261104-three-chats/unity/ProjectileColdOwnerR5';S.mkdir(parents=True,exist_ok=True);D.mkdir(parents=True,exist_ok=True)
files=[]
for folder in ['Core','Modules/Projectiles','Modules/Combat','Modules/HealthStates','Modules/Persistence','Modules/WeaponHandling','Integration/CombatPublication','Integration/ProjectileWeapons','RecoveryContracts']:
 files+=list((R/'unity/Assets/Ashfall'/folder).glob('*.cs'))
files += [R/'unity/Assets/Ashfall/Integration/CombatHealthBridge.cs',R/'unity/Assets/Ashfall/Integration/InventoryRecovery/Wire.cs']
files+=list((R/'unity/Tools/Modules/Projectiles').glob('*.cs'))+list((R/'unity/Tools/Integration/ProjectileWeapons').glob('*.cs'))
rows=[]
for p in files:
 q=S/'before'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q);rows.append({'path':p.relative_to(R).as_posix(),'sha256':hashlib.sha256(q.read_bytes()).hexdigest()})
(D/'before.json').write_text(json.dumps(rows,indent=2))
reads=[]
for p in sorted((R/'docs/agent-runs/20261104-three-chats/unity').glob('accepted-neutral-v*.json'))+[R/'docs/agent-runs/20261104-three-chats/unity/phase21/source-index.json']:
 data=json.loads(p.read_text(encoding='utf-8-sig'));reads.append({'path':str(p.relative_to(R)),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'entries':len(data)})
(D/'coordination-read.json').write_text(json.dumps(reads,indent=2));print(len(rows),'frozen source files',reads)

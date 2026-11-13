from pathlib import Path
import hashlib,json,shutil
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/CombatProjectileArchiveR5';D=R/'docs/agent-runs/20261104-three-chats/unity/CombatProjectileArchiveR5'
S.mkdir(parents=True,exist_ok=True);D.mkdir(parents=True,exist_ok=True)
assert not (D/'before.json').exists()
files=[]
for f in ['Core','RecoveryContracts','Modules/Combat','Modules/HealthStates','Modules/Persistence','Integration/CombatPublication']:
 files+=list((R/'unity/Assets/Ashfall'/f).glob('*.cs'))
files+=[R/'unity/Assets/Ashfall/Integration/CombatHealthBridge.cs']
for f in ['CombatPublication','ProjectileWeapons']:files+=list((R/'unity/Tools/Integration'/f).glob('*.cs'))
files+=list((R/'unity/Documentation').glob('*.md'))
files+=list((R/'docs/agent-runs/20261104-three-chats/unity').glob('accepted-neutral-v*.json'))
files+=[R/'docs/agent-runs/20261104-three-chats/unity'/f for f in ['phase21/source-index.json','phase21/ROOT-REPORT.md','phase25/ROOT-REPORT.md','GAMEPLAY-CHECKPOINT-v1.md']]
rows=[]
for p in files:
 data=p.read_bytes();q=S/'before'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(data)
 if p.suffix=='.json':json.loads(data.decode('utf-8-sig'))
 rows.append(dict(path=p.relative_to(R).as_posix(),sha256=hashlib.sha256(data).hexdigest(),bytes=len(data)))
(D/'before.json').write_text(json.dumps(rows,indent=2));print('frozen',len(rows))

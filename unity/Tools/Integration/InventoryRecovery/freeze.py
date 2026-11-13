from pathlib import Path
import json,hashlib,shutil
root=Path.cwd(); out=root/'campaign-staging/20261104-three-chats/unity/InventoryRecoveryR1'; docs=root/'docs/agent-runs/20261104-three-chats/unity/InventoryRecoveryR1'
paths=list((root/'unity/Assets/Ashfall/Core').glob('*.cs'))+list((root/'unity/Assets/Ashfall/Modules/InventoryItems').glob('*.cs'))+list((root/'unity/Assets/Ashfall/Modules/WeaponHandling').glob('*.cs'))
assert not (docs/'before.json').exists(), 'Existing before evidence is immutable'
records=[]
for p in paths:
 t=out/'before'/p.relative_to(root);t.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,t);records.append({'path':str(p.relative_to(root)),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'snapshot':str(t)})
(docs/'before.json').write_text(json.dumps(records,indent=2))
refs=[root/'docs/agent-runs/20261104-three-chats/unity'/('accepted-neutral-v%d.json'%i) for i in range(1,7)]
refs += [root/'docs/agent-runs/20261104-three-chats/unity/phase13'/f for f in ['dispatch-plan.json','source-index.json']]
for p in refs:
 b=p.read_bytes();json.loads(b);t=out/'context'/p.name;t.parent.mkdir(exist_ok=True);t.write_bytes(b)
print('Pinned',len(paths),'production inputs; fully parsed',len(refs),'context records')

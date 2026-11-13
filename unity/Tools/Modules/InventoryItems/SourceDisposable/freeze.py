from pathlib import Path
import hashlib,json,shutil
r=Path.cwd(); o=r/'campaign-staging/20261104-three-chats/unity/SourceDisposableInventoryR1'; d=r/'docs/agent-runs/20261104-three-chats/unity/SourceDisposableInventoryR1'
paths=[r/'unity/Assets/Ashfall/Core/Contracts.cs']
for root in ['unity/Assets/Ashfall/RecoveryContracts','unity/Assets/Ashfall/Modules/InventoryItems','unity/Assets/Ashfall/Modules/WeaponHandling','unity/Tools/Modules/InventoryItems']:
 paths+=list((r/root).rglob('*.cs'))
paths+=list((r/'unity/Documentation').glob('*.md'))
paths+=list((r/'docs/agent-runs/20261104-three-chats/unity').glob('accepted-neutral-v*.json'))
paths += [r/'docs/agent-runs/20261104-three-chats/unity/phase21/source-index.json']
a=[]
for p in sorted(set(paths)):
 b=p.read_bytes(); rel=p.relative_to(r); dest=o/'before'/rel;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(b);a.append(dict(path=rel.as_posix(),bytes=len(b),sha256=hashlib.sha256(b).hexdigest()))
(d/'inputs.json').write_text(json.dumps(a,indent=2))
print('Frozen',len(a),'files')
for n in range(1,7):
 p=r/('docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v%d.json'%n); x=json.loads(p.read_text(encoding='utf-8-sig'));print(p.name,[(k,len(v) if isinstance(v,(dict,list)) else str(v)[:220]) for k,v in x.items() if k not in ['checks','sourceChecks']])

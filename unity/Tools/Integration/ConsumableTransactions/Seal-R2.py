from pathlib import Path
import hashlib,json,shutil
R=Path(r'C:\Users\aling\Documents\ashfall');O=R/'campaign-staging/20261104-three-chats/unity/ConsumableTransactionsR2';E=R/'docs/agent-runs/20261104-three-chats/unity/ConsumableTransactionsR2'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
before=json.loads((E/'before-manifest.json').read_text());old={x['path']:x for x in before}
paths=[R/'unity/Assets/Ashfall/Modules/HealthStates/PreparedGains.cs',R/'unity/Assets/Ashfall/Modules/HealthStates/PreparedPublication.cs']
for base in ['unity/Assets/Ashfall/Modules/InventoryItems','unity/Assets/Ashfall/Modules/Pickups','unity/Assets/Ashfall/Integration/ConsumableTransactions','unity/Tools/Integration/ConsumableTransactions']:
 paths+=list((R/base).glob('*'))
changed=[]
for p in paths:
 if not p.is_file() or p.name=='PreparedWeaponHandoff.cs':continue
 previous=old.get(str(p));digest=sha(p)
 if previous and previous['sha256']==digest:continue
 q=O/'final'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(p.read_bytes())
 changed.append(dict(path=str(p),sha256=digest,bytes=p.stat().st_size,beforeSha256=previous['sha256'] if previous else None,snapshot=str(q)))
(E/'changed-file-SHA256.json').write_text(json.dumps(changed,indent=2))
inputs=json.loads((E/'run03/inputs.json').read_text());drift=[]
for x in inputs:
 p=Path(x['path']);actual=sha(p);drift.append(dict(path=str(p),pinned=x['sha256'],current=actual,drift=actual!=x['sha256']))
(E/'dependency-drift.json').write_text(json.dumps(drift,indent=2))
print('changed',len(changed),'compiled input drift',sum(x['drift'] for x in drift))
reads=[]
for p in list((R/'docs/agent-runs/20261104-three-chats/unity').glob('accepted-neutral-v*.json'))+[R/'docs/agent-runs/20261104-three-chats/unity/phase09/native-summary.json',R/'docs/agent-runs/20261104-three-chats/unity/editor-probes/Avatar-r5-native-05/result.json',R/'docs/agent-runs/20261104-three-chats/unity/editor-probes/Avatar-r5-native-05/execution.json',R/'docs/agent-runs/20261104-three-chats/unity/phase07/status.json']:
 b=p.read_bytes();data=json.loads(b.decode('utf-8-sig'));q=O/'read-receipts'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b);reads.append(dict(path=str(p),sha256=sha(p),bytes=len(b)))
 if 'accepted-neutral' in p.name:print(p.name,[(x.get('name'),x.get('scope')) for x in data.get('accepted',[])],data.get('scope'))
(E/'additional-read-receipts.json').write_text(json.dumps(reads,indent=2))
print('preserved native evidence; no engine executed')

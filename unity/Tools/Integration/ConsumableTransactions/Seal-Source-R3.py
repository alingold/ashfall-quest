from pathlib import Path
import json,hashlib
R=Path(r'C:\Users\aling\Documents\ashfall');E=R/'docs/agent-runs/20261104-three-chats/unity/ConsumableCompositionR3';O=R/'campaign-staging/20261104-three-chats/unity/ConsumableCompositionR3'
prod=['unity/Assets/Ashfall/Integration/ConsumableTransactions/ConsumableBridge.cs','unity/Assets/Ashfall/Modules/InventoryItems/PreparedConsumables.cs','unity/Assets/Ashfall/Modules/InventoryItems/PreparedRemoval.cs']
files=[R/p for p in prod]+[p for p in (R/'unity/Tools/Integration/ConsumableTransactions').glob('*R3*') if p.is_file()]
rows=[]
for p in files:
 b=p.read_bytes();q=O/'candidate'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b);rows.append(dict(path=str(p.relative_to(R)),snapshot=str(q.relative_to(R)),sha256=hashlib.sha256(b).hexdigest().upper(),bytes=len(b)))
(E/'candidate-source.json').write_text(json.dumps(rows,indent=2));print('Candidate source manifest SHA256',hashlib.sha256((E/'candidate-source.json').read_bytes()).hexdigest().upper())
for row in rows[:3]:print(row['path'],row['sha256'])
# Check every consumed frozen byte and unowned phase12 source. No mutation/repair.
verify=[]
for x in json.loads((E/'run04/inputs.json').read_text()):
 p=Path(x['snapshot']);verify.append(dict(path=str(p),match=hashlib.sha256(p.read_bytes()).hexdigest().upper()==x['sha256']))
unchanged=[]
for x in json.loads((E/'phase12-baseline.json').read_text()):
 p=R/x['original'];actual=hashlib.sha256(p.read_bytes()).hexdigest().upper();unchanged.append(dict(path=x['original'],before=x['currentSha256'],after=actual,changed=actual!=x['currentSha256'],owned=x['original'].replace('\\','/') in prod))
(E/'closeout-verification.json').write_text(json.dumps(dict(frozen=verify,current=unchanged),indent=2));print('frozen verified',len(verify),all(x['match'] for x in verify));print('external drift during work',[x['path'] for x in unchanged if x['changed'] and not x['owned']])

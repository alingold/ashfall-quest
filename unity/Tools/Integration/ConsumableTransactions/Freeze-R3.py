from pathlib import Path
import hashlib,json
R=Path(r'C:\Users\aling\Documents\ashfall'); E=R/'docs/agent-runs/20261104-three-chats/unity/ConsumableCompositionR3'; O=R/'campaign-staging/20261104-three-chats/unity/ConsumableCompositionR3'
E.mkdir(parents=True,exist_ok=False);O.mkdir(parents=True,exist_ok=False)
idx=json.loads((R/'docs/agent-runs/20261104-three-chats/unity/phase12/source-index.json').read_text()); records=[]
for x in idx:
 p=R/x['snapshot']; b=p.read_bytes(); assert hashlib.sha256(b).hexdigest().upper()==x['sha256'].upper()
 q=O/'baseline'/x['original'];q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b)
 current=(R/x['original']).read_bytes(); records.append(dict(original=x['original'],snapshot=str(q.relative_to(R)),sha256=x['sha256'],currentSha256=hashlib.sha256(current).hexdigest().upper(),drift=current!=b))
(E/'phase12-baseline.json').write_text(json.dumps(records,indent=2))
paths=list((R/'unity/Documentation').glob('*.md'))+list((R/'unity/Tools/Integration/ConsumableTransactions').glob('*'))+list((R/'docs/agent-runs/20261104-three-chats/unity').glob('accepted-neutral-v*.json'))+[R/'quest-r22/source/scripts/field_consumables.gd',R/'game/scripts/field_consumables.gd']
paths += [R/x['original'] for x in idx if any(s in x['original'] for s in ['ConsumableTransactions','PreparedSelection','PreparedConsumables','PreparedRemoval','TransferBridge'])]
rows=[]
for p in paths:
 if not p.is_file():continue
 b=p.read_bytes();q=O/'before'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b);rows.append(dict(original=str(p.relative_to(R)),snapshot=str(q.relative_to(R)),sha256=hashlib.sha256(b).hexdigest().upper()))
(E/'before.json').write_text(json.dumps(rows,indent=2));print('Verified and froze',len(records),'phase12 sources;',sum(x['drift'] for x in records),'current drift;',len(rows),'context/before files')
for v in range(1,7):
 d=json.loads((R/f'docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v{v}.json').read_text());print('accepted',v,'keys',list(d))

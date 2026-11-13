from pathlib import Path
import json,hashlib,re
R=Path.cwd();E=R/'docs/agent-runs/20261104-three-chats/unity/ConsumableLiveBindingsR4';O=R/'campaign-staging/20261104-three-chats/unity/ConsumableLiveBindingsR4'
h=lambda b:hashlib.sha256(b).hexdigest().upper()
inputs=json.loads((E/'run05/inputs.json').read_text());drift=[];symbols=[]
for row in inputs:
 p=R/row['original'];b=p.read_bytes();drift.append(dict(original=row['original'],consumed=row['sha256'],current=h(b),drift=h(b)!=row['sha256']))
 for n,line in enumerate((R/row['snapshot']).read_text(encoding='utf-8-sig').splitlines(),1):
  if re.search(r'\b(public|internal)\b',line):symbols.append(dict(path=row['original'],line=n,symbol=line.strip()))
(E/'dependency-drift.json').write_text(json.dumps(drift,indent=2));(E/'source-symbols.json').write_text(json.dumps(symbols,indent=2))
for name in ['TestsR3.cs','TestsR4.cs','Tests.cs','ColdR4.cs']:
 p=R/'unity/Tools/Integration/ConsumableTransactions'/name;q=O/'run05/tests'/name;q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(p.read_bytes())
# Root receipts are context, never this leaf's executed evidence.
summary=[]
for p in (R/'docs/agent-runs/20261104-three-chats/unity/phase15').glob('*validation.json'):
 v=json.loads(p.read_text(encoding='utf-8-sig'));summary.append(dict(path=str(p.relative_to(R)),sha256=h(p.read_bytes()),keys=list(v) if isinstance(v,dict) else 'array'))
(E/'root-validation-context.json').write_text(json.dumps(summary,indent=2))
changed=[x for x in inputs if x['original'].endswith(('ConsumableBridge.cs','PreparedConsumables.cs','PreparedRemoval.cs','TransferBridge.cs','ConsumableInventoryCheckpoint.cs'))]
(E/'candidate-source.json').write_text(json.dumps(changed,indent=2))
print('candidate',h((E/'candidate-source.json').read_bytes()),'inputs',len(inputs),'drift',sum(x['drift'] for x in drift))
print('changed',[(x['original'],x['sha256']) for x in changed])

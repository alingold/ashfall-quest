from pathlib import Path
import json,hashlib
R=Path.cwd();P=R/'campaign-staging/20261104-three-chats/unity/AudioRuntimeR2';E=R/'docs/agent-runs/20261104-three-chats/unity/AudioRuntimeR2'
paths=['docs/TWO-CHAT-20-AGENT-OVERRIDE.md','docs/HILL-CLIMB-ORCHESTRATION.md','docs/OVERNIGHT-AGENT-PLAN-2026-11-03.md','docs/AGENT-DISPATCH-BRIEFS-2026-11-03.md','docs/agent-runs/20261104-0020/preflight.json','game/scripts/audio_bank.gd','quest-r22/source/scripts/audio_bank.gd','game/scripts/reference_audio_099.gd','quest-r22/source/scripts/reference_audio_099.gd']
paths += ['docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v'+str(i)+'.json' for i in range(1,6)]
paths += ['unity/Documentation/'+n+'.md' for n in ['CONTRACTS-v1','CONTRACTS-v2','CONTRACTS-v3','COLLISION-LAYERS-v1','NETWORK-LIFECYCLE-v4','DAMAGE-POLICY-v4','HEALTH-TRANSACTIONS-v4','MATCH-TRANSACTIONS-v4','AUDIO-DEPLOYMENT-v2']]
rows=[]
for rel in paths:
 p=R/rel;data=p.read_bytes();dst=P/'context'/rel;dst.parent.mkdir(parents=True,exist_ok=True);dst.write_bytes(data);row=dict(path=rel,sha256=hashlib.sha256(data).hexdigest())
 if p.suffix=='.json':
  obj=json.loads(data.decode('utf-8-sig'));row['top_keys']=list(obj) if isinstance(obj,dict) else 'list'
 rows.append(row)
(E/'context-identities.json').write_text(json.dumps(rows,indent=2))
# Verify scoped accepted ledger source records without dumping entire ledgers into chat.
checks=[]
def visit(v):
 if isinstance(v,dict):
  path=v.get('path');expected=v.get('expected')
  if path and isinstance(path,str) and expected and isinstance(expected,str):
   p=Path(path);p=p if p.is_absolute() else R/p
   if p.is_file():checks.append(dict(path=str(p),expected=expected,actual=hashlib.sha256(p.read_bytes()).hexdigest(),match=expected.lower()==hashlib.sha256(p.read_bytes()).hexdigest()))
  for x in v.values():visit(x)
 elif isinstance(v,list):
  for x in v:visit(x)
for i in range(1,6):visit(json.loads((R/('docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v'+str(i)+'.json')).read_text(encoding='utf-8-sig')))
(E/'accepted-ledger-read.json').write_text(json.dumps(checks,indent=2))
# Full plan chunks retained to verify reads outside tool truncation; source unchanged.
print('Context pinned',len(rows),'accepted source records examined',len(checks),'drift records',sum(not x['match'] for x in checks))

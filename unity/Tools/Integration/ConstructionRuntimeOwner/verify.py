import json,hashlib
from pathlib import Path
R=Path.cwd();O=R/'docs/agent-runs/20261104-three-chats/unity/ConstructionRuntimeOwnerR5';B=R/'campaign-staging/20261104-three-chats/unity/ConstructionRuntimeOwnerR5';cache={};summaries=[]
def digest(p):
 if p not in cache:cache[p]=hashlib.sha256(p.read_bytes()).hexdigest() if p.is_file() else None
 return cache[p]
for v in range(1,7):
 p=B/'before/docs/agent-runs/20261104-three-chats/unity'/('accepted-neutral-v%d.json'%v);d=json.loads(p.read_text(encoding='utf-8-sig'));checks=[]
 def walk(x):
  if isinstance(x,dict):
   if 'path' in x and 'expected' in x and isinstance(x['path'],str):
    a=digest(R/x['path']);checks.append(dict(path=x['path'],expected=x['expected'],actual=a,match=a is not None and a.upper()==str(x['expected']).upper()))
   for y in x.values():walk(y)
  elif isinstance(x,list):
   for y in x:walk(y)
 walk(d);summaries.append(dict(version=v,receiptSha256=digest(p),records=len(checks),mismatches=[x for x in checks if not x['match']]))
(O/'accepted-verification.json').write_text(json.dumps(summaries,indent=2));print([(x['version'],x['records'],len(x['mismatches'])) for x in summaries])
rows=json.loads((B/'before.json').read_text());drift=[]
for r in rows:
 actual=digest(R/r['path'])
 if actual!=r['sha256']:drift.append(dict(r,currentSha256=actual,owned='/Integration/ConstructionTransactions/' in r['path'] or '/Integration/ConstructionRuntime/' in r['path']))
(O/'dependency-drift.json').write_text(json.dumps(drift,indent=2));print('drift',[(r['path'],r['owned']) for r in drift])
idx=json.loads((B/'before/docs/agent-runs/20261104-three-chats/unity/phase21/source-index.json').read_text(encoding='utf-8-sig'));print('phase21 entries',len(idx))

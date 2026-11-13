import pathlib, json, hashlib
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall')
S=R/'campaign-staging/20261104-three-chats/unity/NetworkRuntimeR2'
D=R/'docs/agent-runs/20261104-three-chats/unity/NetworkRuntimeR2'
S.mkdir(exist_ok=True); D.mkdir(exist_ok=True)
B=S/'baseline'; B.mkdir()
sha=lambda b:hashlib.sha256(b).hexdigest().upper()
rows=[]
for x in json.loads((R/'docs/agent-runs/20261104-three-chats/unity/phase12/source-index.json').read_text()):
 p=R/x['snapshot']; b=p.read_bytes(); assert sha(b)==x['sha256']
 q=B/(str(len(rows))+'-'+p.name);q.write_bytes(b)
 live=R/x['original']
 rows.append(dict(original=x['original'],snapshot=str(q.relative_to(R)),sha256=sha(b),current=sha(live.read_bytes()),kind='phase12'))
for folder in ['unity/Assets/Ashfall/Integration/NetworkRuntime','unity/Tools/Integration/NetworkRuntime']:
 for p in sorted((R/folder).rglob('*')):
  if not p.is_file():continue
  b=p.read_bytes();q=B/(str(len(rows))+'-'+p.name);q.write_bytes(b)
  rows.append(dict(original=str(p.relative_to(R)),snapshot=str(q.relative_to(R)),sha256=sha(b),current=sha(b),kind='owned-before'))
(D/'baseline.json').write_text(json.dumps(rows,indent=2))
accepted=[]
def walk(x):
 if isinstance(x,dict):
  if 'path' in x and 'expected' in x:
   p=pathlib.Path(x['path']);p=p if p.is_absolute() else R/p
   if p.is_file(): accepted.append(dict(path=str(p),expected=x['expected'],actual=sha(p.read_bytes())))
  for v in x.values():walk(v)
 elif isinstance(x,list):
  for v in x:walk(v)
for i in range(1,7):walk(json.loads((R/('docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v%d.json'%i)).read_text(encoding='utf-8-sig')))
(D/'accepted-verification.json').write_text(json.dumps(accepted,indent=2))
print('frozen',len(rows),'phase12 drift',sum(x['current']!=x['sha256'] for x in rows),'accepted',len(accepted),'mismatch',sum(x['expected'].upper()!=x['actual'] for x in accepted))

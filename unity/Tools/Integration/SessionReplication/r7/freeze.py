import pathlib,json,hashlib
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall'); S=R/'campaign-staging/20261104-three-chats/unity/SessionReplicationR7'; D=R/'docs/agent-runs/20261104-three-chats/unity/SessionReplicationR7'
paths=[]
for folder in ['Core','Modules/Networking','Modules/Sessions','Modules/HealthStates','Integration/Sessions','Integration/SessionReplication']:
 paths+=list((R/'unity/Assets/Ashfall'/folder).rglob('*.cs'))
rows=[]
for p in paths:
 b=p.read_bytes();q=S/'before'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);assert not q.exists();q.write_bytes(b);rows.append(dict(path=str(p.relative_to(R)),snapshot=str(q.relative_to(R)),sha256=hashlib.sha256(b).hexdigest().upper(),bytes=len(b)))
(D/'before-manifest.json').write_text(json.dumps(rows,indent=2))
# Read complete accepted receipts and verify exact path/hash records; no acceptance changes.
accepted=[]
for n in range(1,6):
 p=R/'docs/agent-runs/20261104-three-chats/unity'/('accepted-neutral-v%d.json'%n);o=json.loads(p.read_text(encoding='utf-8-sig'))
 def walk(x):
  if isinstance(x,dict):
   if 'path' in x and ('sha256' in x or 'expected' in x):
    q=pathlib.Path(x['path']);q=q if q.is_absolute() else R/q;h=x.get('sha256',x.get('expected'));a=hashlib.sha256(q.read_bytes()).hexdigest().upper() if q.is_file() else None;accepted.append(dict(path=str(q),expected=h,actual=a,match=a==str(h).upper()))
   for v in x.values():walk(v)
  elif isinstance(x,list):
   for v in x:walk(v)
 walk(o)
(D/'accepted-verification.json').write_text(json.dumps(accepted,indent=2));print('preserved',len(rows),'accepted',len(accepted),'mismatches',sum(not x['match'] for x in accepted))

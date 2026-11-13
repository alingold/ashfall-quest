import pathlib, hashlib, json
R=pathlib.Path(__file__).resolve().parents[4]
S=R/'campaign-staging/20261104-three-chats/unity/LifecycleBindingsR5'
D=R/'docs/agent-runs/20261104-three-chats/unity/LifecycleBindingsR5'
S.mkdir(exist_ok=False);D.mkdir(exist_ok=False)
def sha(b):return hashlib.sha256(b).hexdigest()
rows=[]
for p in (R/'unity/Assets/Ashfall').rglob('*.cs'):
 rel=p.relative_to(R).as_posix()
 if '/Integration/Sessions/' in rel or '/Integration/SessionReplication/' in rel:continue
 b=p.read_bytes();q=S/'inputs'/rel;q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b)
 rows.append(dict(path=rel,sha256=sha(b),bytes=len(b)))
(D/'inputs.json').write_text(json.dumps(rows,indent=2))
context=[]
for p in list((R/'unity/Documentation').glob('*.md'))+list((R/'docs/agent-runs/20261104-three-chats/unity').glob('accepted-neutral-v*.json'))+list((R/'docs/agent-runs/20261104-three-chats/unity/phase15').glob('*.json')):
 b=p.read_bytes();data=None
 if p.suffix=='.json':data=json.loads(b.decode('utf-8-sig'))
 rel=p.relative_to(R);q=S/'context'/rel;q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b)
 context.append(dict(path=rel.as_posix(),sha256=sha(b),kind=type(data).__name__,entries=len(data) if isinstance(data,(dict,list)) else None))
(D/'context.json').write_text(json.dumps(context,indent=2))
print('Pinned',len(rows),'sources and',len(context),'contracts/accepted/phase15 records; active writer excluded')

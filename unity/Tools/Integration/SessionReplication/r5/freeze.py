import pathlib,hashlib,json
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall'); O=R/'campaign-staging/20261104-three-chats/unity/SessionReplicationR5'; D=R/'docs/agent-runs/20261104-three-chats/unity/SessionReplicationR5'
rows=[]
for folder in ['Core','Modules/Networking','Modules/Sessions','Integration/Sessions','Integration/SessionReplication']:
 for p in sorted((R/'unity/Assets/Ashfall'/folder).glob('*.cs')):
  b=p.read_bytes(); q=O/'before'/folder/p.name;q.parent.mkdir(parents=True,exist_ok=True);assert not q.exists();q.write_bytes(b);assert p.read_bytes()==b
  rows.append(dict(path=str(p),snapshot=str(q),sha256=hashlib.sha256(b).hexdigest()))
(D/'before.json').write_text(json.dumps(rows,indent=2))
# Read complete accepted ledgers without flooding output; preserve identities, not copies.
reads=[]
for p in sorted((R/'docs/agent-runs/20261104-three-chats/unity').glob('accepted-neutral-v*.json')):
 b=p.read_bytes();obj=json.loads(b.decode('utf-8-sig'));reads.append(dict(path=str(p),bytes=len(b),sha256=hashlib.sha256(b).hexdigest(),keys=list(obj)))
(D/'accepted-reads.json').write_text(json.dumps(reads,indent=2))
print('Pinned',len(rows),'small source files; read',len(reads),'accepted ledgers')

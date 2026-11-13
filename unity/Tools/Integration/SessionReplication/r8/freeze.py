import pathlib,json,hashlib
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall')
S=R/'campaign-staging/20261104-three-chats/unity/SessionStateReplicationR8'
D=R/'docs/agent-runs/20261104-three-chats/unity/SessionStateReplicationR8'
S.mkdir(parents=True,exist_ok=True);D.mkdir(parents=True,exist_ok=True)
assert not (D/'inputs.json').exists()
pins=[]
def pin(p,category):
 b=p.read_bytes();q=S/'before'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b)
 pins.append(dict(path=str(p.relative_to(R)),snapshot=str(q.relative_to(R)),sha256=hashlib.sha256(b).hexdigest(),category=category))
for folder in ['Core','Modules/Networking','Modules/Sessions','Modules/HealthStates','Modules/Player','Integration/Sessions','Integration/SessionReplication']:
 for p in (R/'unity/Assets/Ashfall'/folder).glob('*.cs'):pin(p,'source')
U=R/'docs/agent-runs/20261104-three-chats/unity'
refs=list((R/'unity/Documentation').glob('*.md'))+list(U.glob('accepted-neutral-v*.json'))+list((U/'phase13').glob('*.json'))+[U/'reviews/SessionReplicationReviewR7/REPORT.md',U/'NetworkRuntimeR2/ROOT-REVIEW-ONLY.patch']
refs+=list((U/'editor-probes/avatar-pose-r1-native-03').glob('*.json'))
for p in refs:pin(p,'reference')
(D/'inputs.json').write_text(json.dumps(pins,indent=2))
for p in U.glob('accepted-neutral-v*.json'):
 j=json.loads(p.read_text(encoding='utf-8-sig'));print(p.name,str(j)[:280])
j=json.loads((U/'phase13/source-index.json').read_text(encoding='utf-8-sig'));print('source index',str(j)[:650])
j=json.loads((U/'phase13/root-validation.json').read_text(encoding='utf-8-sig'));print('root results',[(x.get('name'),x.get('compile'),x.get('exit')) for x in j.get('results',[])])
print('frozen',len(pins))

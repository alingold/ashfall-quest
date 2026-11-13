import pathlib,json,hashlib
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall')
S=R/'campaign-staging/20261104-three-chats/unity/MatchDeliveryR8'
D=R/'docs/agent-runs/20261104-three-chats/unity/MatchDeliveryR8'
rows=[]
for x in json.loads((R/'docs/agent-runs/20261104-three-chats/unity/phase12/source-index.json').read_text()):
    p=R/x['original']; frozen=R/x['snapshot']; b=p.read_bytes()
    assert hashlib.sha256(frozen.read_bytes()).hexdigest().upper()==x['sha256'].upper()
    q=S/'before'/x['original'];q.parent.mkdir(parents=True,exist_ok=True)
    if q.exists(): b=q.read_bytes()
    else: q.write_bytes(b)
    rows.append(dict(original=x['original'],snapshot=str(q.relative_to(R)),sha256=hashlib.sha256(b).hexdigest().upper(),phase12=x['sha256'],drift=b!=frozen.read_bytes()))
(D/'baseline.json').write_text(json.dumps(rows,indent=2))
accepted={}
for i in range(1,7):
    p=R/('docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v%d.json'%i)
    data=json.loads(p.read_text(encoding='utf-8-sig'));accepted[p.name]=dict(sha256=hashlib.sha256(p.read_bytes()).hexdigest(),content=data)
(D/'accepted-inputs.json').write_text(json.dumps(accepted,indent=2))
print('Frozen',len(rows),'sources; drift',sum(x['drift'] for x in rows),'parsed six acceptance ledgers')

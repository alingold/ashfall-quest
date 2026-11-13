import pathlib,json,hashlib
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall');D=R/'docs/agent-runs/20261104-three-chats/unity/MatchPingRecoveryR12';S=R/'campaign-staging/20261104-three-chats/unity/MatchPingRecoveryR12'
rows=json.loads((D/'inputs.json').read_text());drift=[]
for row in rows:
 p=R/row['source'];h=hashlib.sha256(p.read_bytes()).hexdigest() if p.exists() else None
 if h!=row['sha256']:drift.append(dict(source=row['source'],frozen=row['sha256'],current=h,owned='/MatchDirector/' in row['source']))
(D/'later-drift.json').write_text(json.dumps(drift,indent=2))
summary=[]
for n in range(1,7):
 p=S/'before/docs/agent-runs/20261104-three-chats/unity'/('accepted-neutral-v%d.json'%n);data=json.loads(p.read_text(encoding='utf-8-sig'));summary.append(dict(version=n,sha256=hashlib.sha256(p.read_bytes()).hexdigest(),keys=list(data),sourceCheckCount=len(data.get('sourceChecks',[]))))
(D/'accepted-read.json').write_text(json.dumps(summary,indent=2))
print('drift',len(drift),json.dumps(drift,indent=2));print(json.dumps(summary,indent=2))

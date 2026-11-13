import pathlib,json,hashlib,difflib
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/MatchDeliveryR8';D=R/'docs/agent-runs/20261104-three-chats/unity/MatchDeliveryR8'
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
base=json.loads((D/'baseline.json').read_text());changes=[]
for folder in ['MatchDirector','MatchRounds']:
    for p in (R/'unity/Assets/Ashfall/Integration'/folder).glob('*.cs'):
        q=S/'final07/candidate-source'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(p.read_bytes())
        old=S/'before'/p.relative_to(R);changed=not old.exists() or old.read_bytes()!=p.read_bytes()
        changes.append(dict(path=str(p.relative_to(R)),sha256=digest(p),changed=changed,before=digest(old) if old.exists() else None,snapshot=str(q.relative_to(R))))
        if changed and old.exists():(D/(p.name+'.diff')).write_text(''.join(difflib.unified_diff(old.read_text().splitlines(True),p.read_text().splitlines(True))))
(D/'candidate-sources.json').write_text(json.dumps(changes,indent=2))
drift=[]
for row in base:
    p=R/row['original'];h=digest(p)
    if h!=row['sha256']:drift.append(dict(path=row['original'],before=row['sha256'],now=h,owned='\\Integration\\MatchDirector\\' in row['original'] or '\\Integration\\MatchRounds\\' in row['original']))
(D/'final-drift.json').write_text(json.dumps(drift,indent=2))
assert all(digest(R/x['original'])==x['sha256'] for x in base if '\\Modules\\Modes\\' in x['original'] or '\\Modules\\Persistence\\' in x['original'])
print(json.dumps(changes,indent=2))
print('Latest upstream drift:',[x['original'] for x in json.loads((D/'latest-upstream-final07.json').read_text()) if x['drift_from_baseline']])

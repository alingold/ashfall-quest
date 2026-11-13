import pathlib,shutil,json,hashlib
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall')
S=R/'campaign-staging/20261104-three-chats/unity/MatchDirectorR4/r5'
D=R/'docs/agent-runs/20261104-three-chats/unity/MatchDirectorR4/r5'
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest().upper()
if (D/'before.json').exists(): raise RuntimeError('Initial snapshot already exists; preserve it')
paths=[R/'unity/Assets/Ashfall/Core/Contracts.cs']
for mod in ['Modes','Sessions','HealthStates','Persistence']:
 paths+=list((R/('unity/Assets/Ashfall/Modules/'+mod)).glob('*.cs'))
paths+=list((R/'unity/Assets/Ashfall/Integration/MatchDirector').glob('*.cs'))
records=[]
for p in paths:
 rel=p.relative_to(R); q=S/'before'/rel;q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q)
 records.append(dict(path=str(rel),sha256=sha(q)))
(D/'before.json').write_text(json.dumps(records,indent=2))
V=R/'campaign-staging/20261104-three-chats/unity/reviews/MatchDirector-r3'
for name in ['BuilderTests.cs','IndependentTests.cs']:
 shutil.copyfile(V/name,S/name)
accepted=json.loads((R/'docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v1.json').read_text(encoding='utf-8-sig'))
for x in accepted['sourceChecks']:
 if x['name'] in ['Modes','Persistence']: assert sha(pathlib.Path(x['path']))==x['expected']
print('Pinned',len(paths),'source files; accepted Modes/Persistence exact hashes verified')

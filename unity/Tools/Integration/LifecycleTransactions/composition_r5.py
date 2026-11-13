import pathlib,json,subprocess,hashlib
R=pathlib.Path(__file__).resolve().parents[4];S=R/'campaign-staging/20261104-three-chats/unity/LifecycleBindingsR5';D=R/'docs/agent-runs/20261104-three-chats/unity/LifecycleBindingsR5';O=S/'composition02';O.mkdir(exist_ok=False);I=S/'inputs'
old=R/'campaign-staging/20261104-three-chats/unity/reviews/RuntimeLoopReviewR3/frozen'
parents={p.relative_to(old).parent.as_posix() for p in (old/'unity/Assets/Ashfall').rglob('*.cs')}
parents.update('unity/Assets/Ashfall/'+x for x in ['Integration/CombatPublication','Modules/Persistence','Integration/InventoryRecovery','Modules/World'])
files=[]
for p in (I/'unity/Assets/Ashfall').rglob('*.cs'):
 if p.relative_to(I).parent.as_posix() not in parents or 'Engine' in p.parts or '/LifecycleTransactions/' in p.as_posix() or p.name in ['PreparedLifecycle.cs','PreparedLifeProjection.cs']:continue
 files.append(p)
files+=list((R/'unity/Assets/Ashfall/Integration/LifecycleTransactions').glob('*.cs'))+[R/'unity/Assets/Ashfall/Modules/HealthStates/PreparedLifecycle.cs',R/'unity/Assets/Ashfall/Modules/Sessions/PreparedLifeProjection.cs',D/'RootComposition.cs']
frozen=[]
for i,p in enumerate(files):
 q=O/('%03d-'%i+p.name);q.write_bytes(p.read_bytes());frozen.append(q)
rsp=O/'compile.rsp';rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:library','/r:System.Runtime.Serialization.dll','/r:System.Web.Extensions.dll','/out:"'+str(O/'Composition.dll')+'"']+['"'+str(p)+'"' for p in frozen]));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];p=subprocess.run(cmd,capture_output=True,text=True);(O/'compile.log').write_text(p.stdout+p.stderr);(D/'composition-results.json').write_text(json.dumps(dict(command=cmd,exit=p.returncode,inputs=[dict(path=str(q.relative_to(R)),sha256=hashlib.sha256(q.read_bytes()).hexdigest()) for q in frozen]),indent=2));print(p.stdout);raise SystemExit(p.returncode)

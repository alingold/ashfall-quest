import pathlib, json, hashlib, subprocess,sys
R=pathlib.Path(__file__).resolve().parents[4];S=R/'campaign-staging/20261104-three-chats/unity/LifecycleBindingsR5';D=R/'docs/agent-runs/20261104-three-chats/unity/LifecycleBindingsR5'
O=S/(sys.argv[1] if len(sys.argv)>1 else 'validation');O.mkdir(exist_ok=False)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
inputs=S/'inputs';sources=[]
for area in ['Core','Modules/HealthStates','Modules/Sessions','Modules/World']:
 for p in (inputs/'unity/Assets/Ashfall'/area).rglob('*.cs'):
  if 'Engine' in p.parts:continue
  rel=p.relative_to(inputs);live=R/rel
  sources.append(live if p.name in ['PreparedLifecycle.cs','PreparedLifeProjection.cs'] else p)
sources+=list((R/'unity/Assets/Ashfall/Integration/LifecycleTransactions').glob('*.cs'))
sources+=[inputs/'unity/Assets/Ashfall/Integration/MatchDirector/Ports.cs']
# Pin owned candidate too. All compile and repeat runs read the exact same bytes.
frozen=[]
for i,p in enumerate(sources):
 q=O/('%03d-'%i+p.name);q.write_bytes(p.read_bytes());frozen.append(q)
tools=R/'unity/Tools/Integration/LifecycleTransactions'
tests=[]
for p in [tools/'Scenario.cs',tools/'BindingsR5Tests.cs']:
 q=O/p.name;q.write_bytes(p.read_bytes());tests.append(q)
results=[]
for name in ['Fixture','BindingsR5Tests']:
 exe=O/(name+'.exe');rsp=O/(name+'.rsp')
 args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/r:System.Runtime.Serialization.dll','/target:exe','/main:'+name,'/out:"'+str(exe)+'"']+['"'+str(p)+'"' for p in frozen+tests]
 rsp.write_text('\n'.join(args));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)]
 c=subprocess.run(cmd,capture_output=True,text=True);(O/(name+'-compile.log')).write_text(c.stdout+c.stderr)
 row=dict(name=name,command=cmd,compile=c.returncode)
 if c.returncode==0:
  p=subprocess.run([str(exe)],capture_output=True,text=True);(O/(name+'-run.log')).write_text(p.stdout+p.stderr);row.update(exit=p.returncode,output=p.stdout+p.stderr)
 results.append(row)
(D/(O.name+'-commands-results.json')).write_text(json.dumps(results,indent=2))
(D/'compile-inputs.json').write_text(json.dumps([dict(path=str(p.relative_to(R)),sha256=sha(p)) for p in frozen+tests],indent=2))
print(json.dumps(results,indent=2))
raise SystemExit(0 if all(x['compile']==0 and x.get('exit')==0 for x in results) else 1)

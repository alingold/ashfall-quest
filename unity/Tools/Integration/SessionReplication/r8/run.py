import pathlib,json,hashlib,subprocess
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/SessionStateReplicationR8';D=R/'docs/agent-runs/20261104-three-chats/unity/SessionStateReplicationR8'
n=1
while (S/('run%02d'%n)).exists():n+=1
O=S/('run%02d'%n);O.mkdir();L=D/O.name;L.mkdir();pins=[];commands=[]
def cp(p,q):
 b=p.read_bytes();q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b);pins.append(dict(path=str(p.relative_to(R)),snapshot=str(q.relative_to(R)),sha256=hashlib.sha256(b).hexdigest()));return q
files=[]
for folder in ['Core','Modules/Networking','Modules/Sessions','Modules/HealthStates','Modules/Player','Integration/Sessions','Integration/SessionReplication']:
 for p in (R/'unity/Assets/Ashfall'/folder).glob('*.cs'):
  # Other namespaces remain exact immutable pre-edit snapshots throughout tests.
  src=p if folder.startswith('Integration/') else S/'before'/p.relative_to(R)
  files.append(cp(src,O/'source'/folder/p.name))
T=R/'unity/Tools/Integration/SessionReplication';V=R/'campaign-staging/20261104-three-chats/unity/reviews/SessionReplication-r6'
fixture=cp(T/'r5/Fixture.cs',O/'tests/Fixture.cs')
def suite(name,tests,args=[],mutate=None,expected=0):
 fs=files[:]
 if mutate:
  suffix,old,new=mutate;p=next(p for p in fs if str(p).endswith(suffix));b=p.read_text();assert old in b
  q=O/(name+'-mutant.cs');q.write_text(b.replace(old,new));fs[fs.index(p)]=q
 testfiles=[cp(p,O/'tests'/name/p.name) for p in tests]
 exe=O/(name+'.exe');cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/langversion:5','/warnaserror+','/out:'+str(exe)]+[str(p) for p in fs+testfiles]
 for phase,argv in [('compile',cmd),('run',[str(exe)]+args)]:
  r=subprocess.run(argv,capture_output=True,text=True,timeout=180);log=r.stdout+r.stderr;(L/(name+'-'+phase+'.txt')).write_text(log);commands.append(dict(suite=name,phase=phase,argv=argv,exit=r.returncode));(L/'commands.json').write_text(json.dumps(commands,indent=2));print(name,phase,r.returncode,log[-1800:],flush=True)
  if phase=='compile' and r.returncode:return False
 return commands[-1]['exit']==expected
ok=suite('R8',[fixture,T/'r8/Tests.cs'])
if ok:
 ok &= suite('R7',[fixture,T/'r7/Tests.cs'])
 ok &= suite('R5',[fixture,T/'r5/Tests.cs'])
 ok &= suite('Sequence1',[fixture,T/'r5/Tests.cs'],['restart'])
 ok &= suite('Holdouts',[fixture,V/'Holdouts.cs'])
 ok &= suite('NoGeneration',[fixture,T/'r8/Tests.cs'],mutate=('SourceStateClient.cs','g!=generation','g<=0'),expected=1)
 ok &= suite('NoDigest',[fixture,T/'r8/Tests.cs'],mutate=('SourceStateClient.cs','if(Convert.ToBase64String(actual)!=hash)','if(actual.Length==0)'),expected=1)
 ok &= suite('NoRegistry',[fixture,T/'r8/Tests.cs'],mutate=('SourceStateHost.cs','binding.Current(connection,false)','binding.Record!=null'),expected=1)
(L/'inputs.json').write_text(json.dumps(pins,indent=2));(L/'result.json').write_text(json.dumps(dict(passExpected=bool(ok)),indent=2));print('RESULT',ok,O)
raise SystemExit(0 if ok else 1)

import pathlib,json,hashlib,subprocess
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/SessionReplicationR7';D=R/'docs/agent-runs/20261104-three-chats/unity/SessionReplicationR7';O=S/'run03';O.mkdir();L=D/'run03';L.mkdir();pins=[];commands=[]
def cp(p,q):
 b=p.read_bytes();q.parent.mkdir(parents=True,exist_ok=True);assert not q.exists();q.write_bytes(b);pins.append(dict(path=str(p.relative_to(R)),snapshot=str(q.relative_to(R)),sha256=hashlib.sha256(b).hexdigest().upper(),bytes=len(b)));return q
files=[]
for folder in ['Core','Modules/Networking','Modules/Sessions','Modules/HealthStates','Integration/Sessions','Integration/SessionReplication']:
 for p in (R/'unity/Assets/Ashfall'/folder).glob('*.cs'):files.append(cp(p,O/'source'/folder/p.name))
T=R/'unity/Tools/Integration/SessionReplication'; V=R/'campaign-staging/20261104-three-chats/unity/reviews/SessionReplication-r6'
fixture=cp(T/'r5/Fixture.cs',O/'tests/Fixture.cs');tests=cp(T/'r5/Tests.cs',O/'tests/R5Tests.cs');holdouts=cp(V/'Holdouts.cs',O/'tests/Holdouts.cs');fanout=cp(V/'Fanout.cs',O/'tests/Fanout.cs');old=cp(R/'campaign-staging/20261104-three-chats/unity/reviews/SessionReplication-r4/Independent.cs',O/'tests/Independent.cs');codec=cp(V/'frozen/codec/Acceptance.cs',O/'tests/Acceptance.cs');engine=cp(R/'unity/Assets/Ashfall/Modules/Networking/Engine/UnityTransportAdapter.cs',O/'guard/UnityTransportAdapter.cs')
def suite(name,fs,args=[],library=False):
 exe=O/(name+('.dll' if library else '.exe'));cmd='"C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\csc.exe" /nologo /langversion:5 /warnaserror+ /target:'+('library' if library else 'exe')+' /out:"'+str(exe)+'" '+' '.join('"'+str(p)+'"' for p in fs)
 for phase,c in [('compile',cmd),('run','"'+str(exe)+'" '+' '.join(args))]:
  if library and phase=='run':break
  r=subprocess.run(c,capture_output=True,text=True,timeout=180);(L/(name+'-'+phase+'.txt')).write_text(r.stdout+r.stderr);commands.append(dict(suite=name,phase=phase,command=c,exit=r.returncode));(L/'commands.json').write_text(json.dumps(commands,indent=2));print(name,phase,r.returncode,(r.stdout+r.stderr)[-1500:],flush=True)
  if r.returncode:break
r7=cp(T/'r7/Tests.cs',O/'tests/R7Tests.cs');suite('R7',files+[fixture,r7])
suite('R5',files+[fixture,tests]);suite('Sequence1',files+[fixture,tests],['restart']);suite('Holdouts',files+[fixture,holdouts]);suite('Fanout',files+[fixture,fanout]);suite('IndependentR4',files+[old]);suite('Codec',files+[codec]);suite('DefineOFF',files+[engine],library=True)
(L/'inputs.json').write_text(json.dumps(pins,indent=2))

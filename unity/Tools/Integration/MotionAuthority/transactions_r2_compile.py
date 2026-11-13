from pathlib import Path
import json, subprocess, hashlib
R=Path(r'C:\Users\aling\Documents\ashfall')
O=R/'campaign-staging/20261104-three-chats/unity/HostMotionTransactionsR2'
D=R/'docs/agent-runs/20261104-three-chats/unity/HostMotionTransactionsR2'
run=O/'compile01';run.mkdir(exist_ok=False)
receipt=json.loads((D/'run03-results.json').read_text())
sources=receipt['sources'][:next(i for i,x in enumerate(receipt['sources']) if x['source'].endswith('TransactionsR2Tests.cs'))]
files=[Path(x['snapshot']) for x in sources]
C=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
results=[]
def compile(name,fs,refs,native=False):
 out=run/(name+'.dll');rsp=run/(name+'.rsp')
 flags=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:library','/out:"'+str(out)+'"']
 if native:flags+=['/nostdlib+','/define:UNITY_6000_0_OR_NEWER']
 rsp.write_text('\n'.join(flags+['/reference:"'+str(x)+'"' for x in refs]+['"'+str(x)+'"' for x in fs]))
 argv=[str(C),'/noconfig','@'+str(rsp)];p=subprocess.run(argv,capture_output=True,text=True)
 results.append(dict(name=name,argv=argv,exit=p.returncode,output=p.stdout+p.stderr));print(name,p.returncode,p.stdout+p.stderr)
 return out
framework=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319')
refs=[framework/'System.dll',framework/'System.Core.dll']
parts={part:[Path(x['snapshot']) for x in sources if part in x['source'].replace('\\','/')] for part in ['Core/','Modules/Player/','Modules/Flight/','Modules/Climbing/','Integration/FlightMotor/','Integration/ClimbingMotor/','Integration/MotionAuthority/']}
a=compile('Core',parts['Core/'],refs);b=compile('Player',parts['Modules/Player/'],refs+[a]);c=compile('Flight',parts['Modules/Flight/'],refs+[a]);e=compile('Climbing',parts['Modules/Climbing/'],refs+[a]);f=compile('FlightMotor',parts['Integration/FlightMotor/'],refs+[a,b,c]);g=compile('ClimbingMotor',parts['Integration/ClimbingMotor/'],refs+[a,b,c,e,f]);compile('MotionAuthority',parts['Integration/MotionAuthority/'],refs+[a,b,c,e,f,g])
native=[]
for p in [O/'before/unity/Assets/Ashfall/Modules/Player/Engine/UnityExclusivePlayerMotor.cs',R/'unity/Tools/Integration/MotionAuthority/TransactionsR2NativeFixture.cs']:
 q=run/p.name;q.write_bytes(p.read_bytes());native.append(q)
editor=Path(r'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data')
nrefs=[editor/'NetStandard/ref/2.1.0/netstandard.dll',editor/'Managed/UnityEngine/UnityEngine.CoreModule.dll',editor/'Managed/UnityEngine/UnityEngine.PhysicsModule.dll']
compile('NativeRefusalFixture',files+native,nrefs,True)
# Restoring the reviewed unsafe arbiter must fail the preservation/refusal oracle.
old=O/'before/unity/Assets/Ashfall/Integration/MotionAuthority/HostMotionArbiter.cs'
q=run/'ReviewedUnsafeArbiter.cs';q.write_bytes(old.read_bytes())
mutant=[Path(x['snapshot']) for x in sources if not x['source'].endswith('HostMotionArbiter.cs')]+[q,Path(receipt['sources'][len(sources)]['snapshot'])]
rsp=run/'unsafe-mutant.rsp';exe=run/'unsafe-mutant.exe'
rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:"'+str(exe)+'"']+['/reference:"'+str(x)+'"' for x in refs]+['"'+str(x)+'"' for x in mutant]))
for argv in [[str(C),'/noconfig','@'+str(rsp)],[str(exe)]]:
 p=subprocess.run(argv,capture_output=True,text=True);results.append(dict(name='unsafe-mutant',argv=argv,exit=p.returncode,output=p.stdout+p.stderr));print(p.returncode,p.stdout+p.stderr)
(D/'compile01-results.json').write_text(json.dumps(dict(results=results,nativeReferences=[dict(path=str(x),sha256=hashlib.sha256(x.read_bytes()).hexdigest()) for x in nrefs],nativeSources=[dict(path=str(x),sha256=hashlib.sha256(x.read_bytes()).hexdigest()) for x in native]),indent=2))
assert all(x['exit']==0 for x in results[:-1]) and results[-1]['exit']==1

from pathlib import Path
import json,hashlib,subprocess,sys
R=Path.cwd();O=R/'campaign-staging/20261104-three-chats/unity/MotionPreparedOwnersR3';D=R/'docs/agent-runs/20261104-three-chats/unity/MotionPreparedOwnersR3';run=O/sys.argv[1];run.mkdir(exist_ok=False)
old=json.loads((R/'docs/agent-runs/20261104-three-chats/unity/HostMotionTransactionsR2/run03-results.json').read_text())['sources'];old=old[:next(i for i,x in enumerate(old) if x['source'].endswith('TransactionsR2Tests.cs'))]
owned=['unity/Assets/Ashfall/Modules/Flight/Capsules.cs','unity/Assets/Ashfall/Modules/Climbing/ClimbController.cs','unity/Assets/Ashfall/Modules/Climbing/PreparedClimbMotion.cs']
files=[];manifest=[];results=[]
for x in old:
 rel=x['source'].replace('\\','/');rel=rel[rel.rindex('unity/Assets/'):];p=R/rel if rel in owned else O/'before'/rel
 if not p.exists():raise Exception(str(p))
 files.append(p)
files.append(R/owned[-1])
sealed=[]
for i,p in enumerate(files):
 q=run/(str(i)+'-'+p.name);q.write_bytes(p.read_bytes());sealed.append(q);manifest.append(dict(source=str(p),snapshot=str(q),sha256=hashlib.sha256(q.read_bytes()).hexdigest()))
def call(argv):
 p=subprocess.run(list(map(str,argv)),capture_output=True,text=True);results.append(dict(argv=list(map(str,argv)),exit=p.returncode,output=p.stdout+p.stderr));print(p.returncode,(p.stdout+p.stderr)[-2200:]);return p.returncode==0
C=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe');F=C.parent
for test in ['unity/Tools/Integration/MotionAuthority/r3/PreparedOwnersTests.cs','unity/Tools/Integration/MotionAuthority/TransactionsR2Tests.cs','unity/Tools/Integration/FlightMotor/r7/R7Tests.cs','unity/Tools/Integration/FlightMotor/r6/R6Tests.cs','unity/Tools/Modules/Flight/R2Tests.cs','unity/Tools/Modules/Flight/FlightTests.cs']:
 p=R/test;q=run/p.name;q.write_bytes(p.read_bytes());manifest.append(dict(source=str(p),snapshot=str(q),sha256=hashlib.sha256(q.read_bytes()).hexdigest()));rsp=run/(p.stem+'.rsp');exe=run/(p.stem+'.exe')
 rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:"'+str(exe)+'"']+['/reference:"'+str(F/x)+'"' for x in ['System.dll','System.Core.dll']]+['"'+str(x)+'"' for x in sealed+[q]]))
 if call([C,'/noconfig','@'+str(rsp)]):call([exe])
(D/(sys.argv[1]+'-results.json')).write_text(json.dumps(dict(results=results,sources=manifest),indent=2));sys.exit(int(any(x['exit'] for x in results)))


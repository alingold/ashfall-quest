from pathlib import Path
import json,subprocess,hashlib
R=Path.cwd();D=R/'docs/agent-runs/20261104-three-chats/unity/MotionPreparedOwnersR3';O=R/'campaign-staging/20261104-three-chats/unity/MotionPreparedOwnersR3';run=O/'compile04';run.mkdir(exist_ok=False)
source=json.loads((D/'run05-results.json').read_text())['sources'];source=source[:41];fs=[Path(x['snapshot']) for x in source];C=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe');results=[];manifest=[]
def execute(args):
 p=subprocess.run(list(map(str,args)),capture_output=True,text=True);results.append(dict(argv=list(map(str,args)),exit=p.returncode,output=p.stdout+p.stderr));print(p.returncode,(p.stdout+p.stderr)[-900:]);return p.returncode
refs=[C.parent/'System.dll',C.parent/'System.Core.dll']
def compile(name,files,references,exe=False,native=False):
 out=run/(name+('.exe' if exe else '.dll'));rsp=run/(name+'.rsp');flags=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:'+('exe' if exe else 'library'),'/out:"'+str(out)+'"']
 if native:flags+=['/nostdlib+','/define:UNITY_6000_0_OR_NEWER']
 rsp.write_text('\n'.join(flags+['/reference:"'+str(x)+'"' for x in references]+['"'+str(x)+'"' for x in files]));code=execute([C,'/noconfig','@'+str(rsp)]);return out,code
parts={x:[Path(y['snapshot']) for y in source if x in y['source'].replace('\\','/')] for x in ['Core/','Modules/Player/','Modules/Flight/','Modules/Climbing/','Integration/FlightMotor/','Integration/ClimbingMotor/','Integration/MotionAuthority/']}
a,_=compile('Core',parts['Core/'],refs);b,_=compile('Player',parts['Modules/Player/'],refs+[a]);c,_=compile('Flight',parts['Modules/Flight/'],refs+[a]);d,_=compile('Climbing',parts['Modules/Climbing/'],refs+[a]);e,_=compile('FlightMotor',parts['Integration/FlightMotor/'],refs+[a,b,c]);f,_=compile('ClimbingMotor',parts['Integration/ClimbingMotor/'],refs+[a,b,c,d,e]);compile('MotionAuthority',parts['Integration/MotionAuthority/'],refs+[a,b,c,d,e,f]);test=Path(json.loads((D/'run05-results.json').read_text())['sources'][41]['snapshot']);out,code=compile('ExternalOwnerTests',[test],refs+[a,c,d],True)
if code==0:execute([out])
# Native current motor + source revision + retained refusal test. No engine execution.
extra=[]
for rel in ['unity/Assets/Ashfall/Modules/Player/Engine/UnityExclusivePlayerMotor.cs','unity/Assets/Ashfall/Modules/Player/Engine/UnityPlayerGeometry.cs','unity/Tools/Integration/MotionAuthority/TransactionsR2NativeFixture.cs']:
 p=O/'before'/rel;q=run/p.name;q.write_bytes(p.read_bytes());extra.append(q);manifest.append(dict(source=str(p),snapshot=str(q),sha256=hashlib.sha256(q.read_bytes()).hexdigest()))
q=run/'NativeGates.cs';q.write_bytes((R/'unity/Tools/Integration/MotionAuthority/r3/NativeGates.cs').read_bytes());extra.append(q);manifest.append(dict(source=str(R/'unity/Tools/Integration/MotionAuthority/r3/NativeGates.cs'),snapshot=str(q),sha256=hashlib.sha256(q.read_bytes()).hexdigest()))
editor=Path(r'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data');nrefs=[editor/'NetStandard/ref/2.1.0/netstandard.dll',editor/'Managed/UnityEngine/UnityEngine.CoreModule.dll',editor/'Managed/UnityEngine/UnityEngine.PhysicsModule.dll',editor/'Managed/UnityEngine/UnityEngine.MathematicsModule.dll'];compile('NativeOwnerRefusal',fs+extra,nrefs,native=True)
# A shallow route clone must compile but fail actual mantle isolation oracle.
original=next(x for x in fs if x.name.endswith('-PreparedClimbMotion.cs'));q=run/'ShallowClimb.cs';q.write_text(original.read_text().replace('(Float3[])route.Clone()','route'));out,code=compile('ShallowClimbMutant',[q if x==original else x for x in fs]+[test],refs,True)
if code==0:execute([out])
# Field addition must fail closed, independently of source-name marker checks.
q=run/'FutureClimb.cs';p=next(x for x in fs if x.name.endswith('-ClimbController.cs'));q.write_text(p.read_text().replace('public const string ApiVersion','public int UnaccountedFutureState = 1;\n        public const string ApiVersion'));out,code=compile('FutureFieldMutant',[q if x==p else x for x in fs]+[test],refs,True)
if code==0:execute([out])
(D/'compile04-results.json').write_text(json.dumps(dict(results=results,extra=manifest,references=[dict(path=str(x),sha256=hashlib.sha256(x.read_bytes()).hexdigest()) for x in nrefs]),indent=2))


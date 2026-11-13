from pathlib import Path
import json,hashlib,subprocess,sys
R=Path.cwd();O=R/'campaign-staging/20261104-three-chats/unity/CertifiedMotionPublicationR4';D=R/'docs/agent-runs/20261104-three-chats/unity/CertifiedMotionPublicationR4';B=O/sys.argv[1];B.mkdir(exist_ok=False)
meta=json.loads((O/'before/docs/agent-runs/20261104-three-chats/unity/MotionPreparedOwnersR3/run05-results.json').read_text())['sources'][:41];sources=[];pins=[];results=[]
for x in meta:
 rel=x['source'].replace('\\','/');rel=rel[rel.rindex('unity/Assets/'):];p=O/'before'/rel;q=B/'sources'/rel;q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(p.read_bytes());sources.append(q);pins.append(dict(source=str(p),snapshot=str(q),sha256=hashlib.sha256(q.read_bytes()).hexdigest()))
for p in [R/'unity/Assets/Ashfall/Integration/MotionAuthority/MotionPublicationPreparation.cs',R/'unity/Tools/Integration/MotionAuthority/r4/PublicationTests.cs']:
 q=B/p.name;q.write_bytes(p.read_bytes());pins.append(dict(source=str(p),snapshot=str(q),sha256=hashlib.sha256(q.read_bytes()).hexdigest()))
p=O/'before/unity/Assets/Ashfall/Modules/Player/PlayerSettingsSnapshot.cs';q=B/'sources/unity/Assets/Ashfall/Modules/Player/PlayerSettingsSnapshot.cs';q.write_bytes(p.read_bytes());sources.append(q);pins.append(dict(source=str(p),snapshot=str(q),sha256=hashlib.sha256(q.read_bytes()).hexdigest()))
sources.append(B/'MotionPublicationPreparation.cs');test=B/'PublicationTests.cs';C=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe');refs=[C.parent/'System.dll',C.parent/'System.Core.dll']
def run(args):
 p=subprocess.run(list(map(str,args)),capture_output=True,text=True);results.append(dict(argv=list(map(str,args)),exit=p.returncode,output=p.stdout+p.stderr));(D/(sys.argv[1]+'-commands.json')).write_text(json.dumps(results,indent=2));print(p.returncode,(p.stdout+p.stderr)[-1600:]);return p.returncode

def compile(name,files,references,exe=False,native=False):
 out=B/(name+('.exe' if exe else '.dll'));rsp=B/(name+'.rsp');flags=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:'+('exe' if exe else 'library'),'/out:"'+str(out)+'"']
 if native:flags+=['/nostdlib+','/define:UNITY_6000_0_OR_NEWER']
 rsp.write_text('\n'.join(flags+['/reference:"'+str(p)+'"' for p in references]+['"'+str(p)+'"' for p in files]));code=run([C,'/noconfig','@'+str(rsp)]);return out,code
parts={n:[p for p in sources if key in str(p).replace('\\','/')] for n,key in [('Core','Core/'),('Player','Modules/Player/'),('Flight','Modules/Flight/'),('Climbing','Modules/Climbing/'),('FlightMotor','Integration/FlightMotor/'),('ClimbingMotor','Integration/ClimbingMotor/'),('MotionAuthority','Integration/MotionAuthority/')]};parts['MotionAuthority'].append(B/'MotionPublicationPreparation.cs')
a,_=compile('Core',parts['Core'],refs);b,_=compile('Player',parts['Player'],refs+[a]);c,_=compile('Flight',parts['Flight'],refs+[a]);d,_=compile('Climbing',parts['Climbing'],refs+[a]);e,_=compile('FlightMotor',parts['FlightMotor'],refs+[a,b,c]);f,_=compile('ClimbingMotor',parts['ClimbingMotor'],refs+[a,b,c,d,e]);g,_=compile('MotionAuthority',parts['MotionAuthority'],refs+[a,b,c,d,e,f]);allrefs=refs+[a,b,c,d,e,f,g]
exe,code=compile('PublicConsumer',[test],allrefs,True)
if code==0:run([exe])
# Compile real production mutated participant in a replacement DLL; tests stay unchanged.
for name,old,new in [('RootPreflight','r=RootMotionSafety.Approve(proposal,true,preflight);','r=RuleResult.Accept();'),('Reservation','options.LeftReserved|=frame.Reservations.Left;','options.LeftReserved|=false;'),('Gravity','observation.InputEnabled=frame.Ready;','observation.InputEnabled=frame.Ready;observation.DeltaSeconds*=2;')]:
 q=B/(name+'.cs');q.write_text((B/'MotionPublicationPreparation.cs').read_text().replace(old,new).replace('return RootMotionSafety.Approve(p.Proposal,true,preflight);','return RuleResult.Accept();') if name=='RootPreflight' else (B/'MotionPublicationPreparation.cs').read_text().replace(old,new));assert q.read_text()!=(B/'MotionPublicationPreparation.cs').read_text()
 mutant,code=compile('Mutant'+name,[x for x in parts['MotionAuthority'] if x.name!='MotionPublicationPreparation.cs']+[q],refs+[a,b,c,d,e,f])
 # Isolated runtime directories preserve assembly identities without replacing outputs.
 mb=B/('mutant-'+name);mb.mkdir()
 import shutil
 for p in [a,b,c,d,e,f]:shutil.copyfile(p,mb/p.name)
 # Compiler assembly name must match its filename, so external consumer references that exact mutant.
 shutil.copyfile(mutant,mb/mutant.name)
 ext,code=compile('Consumer'+name,[test],refs+[a,b,c,d,e,f,mutant],True)
 if code==0:run([ext])
# Retained feature suites execute actual algorithms, with no test changes.
for rel in ['unity/Tools/Integration/MotionAuthority/r3/PreparedOwnersTests.cs','unity/Tools/Integration/MotionAuthority/TransactionsR2Tests.cs','unity/Tools/Integration/FlightMotor/r7/R7Tests.cs','unity/Tools/Integration/FlightMotor/r6/R6Tests.cs','unity/Tools/Modules/Flight/R2Tests.cs','unity/Tools/Modules/Flight/FlightTests.cs']:
 p=O/'before'/rel
 if not p.exists():
  raise Exception('Missing frozen retained suite '+str(p))
 q=B/('Retained'+p.name);q.write_bytes(p.read_bytes());out,code=compile('Retained'+p.stem,sources+[q],refs,True)
 if code==0:run([out])
# Exact installed engine API compile only. Never execute.
extra=[]
for rel in ['unity/Assets/Ashfall/Modules/Player/Engine/UnityExclusivePlayerMotor.cs','unity/Assets/Ashfall/Modules/Player/Engine/UnityPlayerGeometry.cs','unity/Tools/Integration/MotionAuthority/TransactionsR2NativeFixture.cs','unity/Tools/Integration/MotionAuthority/r3/NativeGates.cs']:
 p=O/'before'/rel;q=B/p.name;q.write_bytes(p.read_bytes());extra.append(q)
p=O/'before/unity/Tools/Integration/MotionAuthority/r4/NativeFixture.cs';q=B/p.name;q.write_bytes(p.read_bytes());extra.append(q)
editor=Path(r'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data');nr=[editor/'NetStandard/ref/2.1.0/netstandard.dll']+[editor/'Managed/UnityEngine'/n for n in ['UnityEngine.CoreModule.dll','UnityEngine.PhysicsModule.dll','UnityEngine.MathematicsModule.dll']]
compile('NativeStaged',sources+extra,nr,native=True)
(D/(sys.argv[1]+'.json')).write_text(json.dumps(dict(results=results,sources=pins,toolchain=[dict(path=str(p),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in [C,Path(sys.executable)]+refs+nr]),indent=2))

from pathlib import Path
import json,subprocess,shutil,hashlib
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/MotionRuntimeLoopCompositionR5';E=R/'docs/agent-runs/20261104-three-chats/unity/MotionRuntimeLoopCompositionR5';O=S/'regression02';L=E/'regression02';O.mkdir();L.mkdir();files=[];commands=[];results={}
old=json.loads((R/'docs/agent-runs/20261104-three-chats/unity/RuntimeLoopBindingsR4/final06/sources.json').read_text())
for row in old:
 p=Path(row['path']);assert hashlib.sha256(p.read_bytes()).hexdigest()==row['sha256']
 if any(x in p.as_posix() for x in ['/RuntimeLoop/','/Modules/Player/','/Modules/Flight/','/Integration/FlightMotor/','/Integration/ClimbingMotor/','/Modules/Climbing/']):continue
 d=O/'old'/p.as_posix().split('/Assets/Ashfall/')[1];d.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,d);files.append(d)
P=S/'pinned/unity/Assets/Ashfall'
for folder in ['Modules/Player','Modules/Flight','Modules/Climbing','Integration/FlightMotor','Integration/ClimbingMotor']:
 files+=list((P/folder).glob('*.cs'))
for p in (R/'unity/Assets/Ashfall/Integration/RuntimeLoop').rglob('*.cs'):
 d=O/'candidate'/p.relative_to(R);d.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,d);files.append(d)
for name in ['Scenario.cs','R2Gates.cs','R3Gates.cs','R4Gates.cs','R5Gates.cs','NativeSchedulerFixture.cs']:shutil.copyfile(R/'unity/Tools/Integration/RuntimeLoop'/name,O/name)
for name in ['reference-map.json','r2-tone.wav','r2-audio.tsv']:shutil.copyfile(R/'campaign-staging/20261104-three-chats/unity/RuntimeLoopBindingsR4/final06'/name,O/name)
# Retain actual independent pressure fixture and its command authorization extension.
review=R/'campaign-staging/20261104-three-chats/unity/reviews/RuntimeLoopBindingsReviewR4/pressure'
for name in ['Scenario.cs','CapacityIndependent.cs']:shutil.copyfile(review/name,O/('Pressure'+name))
def comp(name,extras,main=None,refs=(),flags=()):
 out=O/(name+('.exe' if main else '.dll'));args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:'+('exe' if main else 'library'),'/reference:System.Runtime.Serialization.dll','/reference:System.Web.Extensions.dll','/out:'+str(out)]+['/reference:'+str(x) for x in refs]+(['/main:'+main] if main else [])+list(flags)+list(map(str,extras));rsp=L/(name+'.rsp');rsp.write_text('\n'.join(subprocess.list2cmdline([x]) for x in args));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];p=subprocess.run(cmd,capture_output=True);commands.append(cmd);results[name]=p.returncode;(L/(name+'-compile.log')).write_bytes(p.stdout+p.stderr);print(name,p.returncode,p.stdout.decode()[-1500:]);return out,p.returncode
lib,rc=comp('Closure',files);assert rc==0
for name,extra,main in [('Scenario',['Scenario.cs'],'Scenario'),('R2',['Scenario.cs','R2Gates.cs'],'R2Tests'),('R3',['Scenario.cs','R2Gates.cs','R3Gates.cs'],'R3Tests'),('R4',['Scenario.cs','R2Gates.cs','R4Gates.cs'],'R4Tests'),('R5',['Scenario.cs','R2Gates.cs','R5Gates.cs'],'LoopR5Tests'),('Pressure',['PressureScenario.cs','PressureCapacityIndependent.cs'],'CapacityIndependent')]:
 exe,rc=comp(name,files+[O/x for x in extra],main);assert rc==0
 p=subprocess.run([str(exe)],cwd=O,capture_output=True);(L/(name+'.log')).write_bytes(p.stdout+p.stderr);results[name+'Run']=p.returncode;print(p.returncode,p.stdout.decode()[-500:])
# Compile only native adapter and fixture against installed real assemblies.
u=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data/Managed/UnityEngine')
comp('Native',[p for p in files if '/RuntimeLoop/Engine/' in p.as_posix()]+[O/'NativeSchedulerFixture.cs'],refs=[lib,u/'UnityEngine.CoreModule.dll',u/'UnityEngine.PhysicsModule.dll',u.parent.parent/'UnityReferenceAssemblies/unity-4.8-api/Facades/netstandard.dll'],flags=['/define:UNITY_6000_0_OR_NEWER'])
(L/'commands.json').write_text(json.dumps(commands,indent=2));(L/'results.json').write_text(json.dumps(results,indent=2));(L/'sources.json').write_text(json.dumps([dict(path=str(p),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in files],indent=2))


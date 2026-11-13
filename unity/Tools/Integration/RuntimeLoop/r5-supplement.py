from pathlib import Path
import subprocess,json,hashlib
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/MotionRuntimeLoopCompositionR5';E=R/'docs/agent-runs/20261104-three-chats/unity/MotionRuntimeLoopCompositionR5';O=S/'regression02';L=E/'supplement02';L.mkdir()
commands=[];results={}
def invoke(name,args):
 rsp=L/(name+'.rsp');rsp.write_text('\n'.join(subprocess.list2cmdline([x]) for x in args));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];p=subprocess.run(cmd,capture_output=True);commands.append(cmd);results[name]=p.returncode;(L/(name+'.log')).write_bytes(p.stdout+p.stderr);print(name,p.returncode,p.stdout.decode()[-800:]);assert p.returncode==0
args=(E/'regression02/Pressure.rsp').read_text().replace('/main:CapacityIndependent','/main:IndependentTests').splitlines()
# Already quoted exact original response lines, preserve those Windows paths.
args.append(str(O/'R2Gates.cs'));rsp=L/'Pressure.rsp';rsp.write_text('\n'.join(args));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];p=subprocess.run(cmd,capture_output=True);commands.append(cmd);(L/'Pressure-compile.log').write_bytes(p.stdout+p.stderr);assert p.returncode==0
p=subprocess.run([str(O/'Pressure.exe')],cwd=O,capture_output=True);(L/'Pressure.log').write_bytes(p.stdout+p.stderr);results['Pressure']=p.returncode;print(p.stdout.decode()[-400:]);assert p.returncode==0
files=[Path(x.strip('"')) for x in (E/'regression02/Closure.rsp').read_text().splitlines() if x.strip('"').endswith('.cs')]
u=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data/Managed/UnityEngine');refs=[O/'Closure.dll',u/'UnityEngine.CoreModule.dll',u/'UnityEngine.PhysicsModule.dll',u.parent.parent/'UnityReferenceAssemblies/unity-4.8-api/Facades/netstandard.dll']
invoke('Native',['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:library','/define:UNITY_6000_0_OR_NEWER','/out:'+str(O/'Native.dll')]+['/reference:'+str(x) for x in refs]+[str(p) for p in files if '/RuntimeLoop/Engine/' in p.as_posix()]+[str(O/'NativeSchedulerFixture.cs')])
for x in refs:results[str(x)]=hashlib.sha256(x.read_bytes()).hexdigest()
# Strict historical UNKNOWN exit remains required.
p=subprocess.run([str(O/'Scenario.exe'),'--strict'],cwd=O,capture_output=True);(L/'strict.log').write_bytes(p.stdout+p.stderr);results['Strict']=p.returncode;assert p.returncode==2
(L/'commands.json').write_text(json.dumps(commands,indent=2));(L/'results.json').write_text(json.dumps(results,indent=2))


from pathlib import Path
import subprocess,json,hashlib,shutil
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/MotionRuntimeLoopCompositionR5';E=R/'docs/agent-runs/20261104-three-chats/unity/MotionRuntimeLoopCompositionR5';O=S/'run03';L=E/'run03';O.mkdir();L.mkdir()
P=S/'pinned/unity/Assets/Ashfall';commands=[];results={}
def comp(name,files,refs=(),main=None):
 out=O/(name+('.exe' if main else '.dll'));args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:'+('exe' if main else 'library'),'/out:'+str(out)]+['/reference:'+str(x) for x in refs]+(['/main:'+main] if main else [])+list(map(str,files));rsp=L/(name+'.rsp');rsp.write_text('\n'.join(subprocess.list2cmdline([x]) for x in args));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];p=subprocess.run(cmd,capture_output=True);commands.append(cmd);(L/(name+'-compile.log')).write_bytes(p.stdout+p.stderr);print(name,p.returncode,p.stdout.decode()[-1600:]);results[name]=p.returncode;assert p.returncode==0;return out
refs=[]
for name,path in [('Core','Core'),('Player','Modules/Player'),('Flight','Modules/Flight'),('Climbing','Modules/Climbing'),('FlightMotor','Integration/FlightMotor'),('ClimbingMotor','Integration/ClimbingMotor')]:
 refs.append(comp(name,list((P/path).glob('*.cs')),refs))
for rel in ['unity/Assets/Ashfall/Integration/RuntimeLoop/RuntimeMotionPreparation.cs','unity/Tools/Integration/RuntimeLoop/MotionR5.cs']:
 p=R/rel;d=O/p.name;shutil.copyfile(p,d)
lib=comp('RuntimeMotion',[O/'RuntimeMotionPreparation.cs'],refs)
exe=comp('MotionR5',[O/'MotionR5.cs'],refs+[lib],'MotionR5');p=subprocess.run([str(exe)],capture_output=True);(L/'MotionR5.log').write_bytes(p.stdout+p.stderr);results['run']=p.returncode;print(p.stdout.decode());assert p.returncode==0
# Behavioral compiled mutants remain isolated from the production candidate.
source=(O/'RuntimeMotionPreparation.cs').read_text()
for name,old,new in [('foreign-owner','frame.Environment.ExternalMotionOwner','false'),('generation','frame.LifecycleGeneration!=generation','frame.LifecycleGeneration<generation'),('invalidation','void Invalidate(){retired=true;Abort(pending);}','void Invalidate(){retired=false;}')]:
 d=O/(name+'.cs');assert old in source;d.write_text(source.replace(old,new));mut=comp('mut-'+name,[d],refs);dest=O/('case-'+name);dest.mkdir();
 for ref in refs:shutil.copyfile(ref,dest/ref.name)
 shutil.copyfile(mut,dest/lib.name)
 # Compile consumer against mutant identity rather than shadowing assembly identity.
 x=comp('test-'+name,[O/'MotionR5.cs'],refs+[mut],'MotionR5');p=subprocess.run([str(x)],capture_output=True);(L/(name+'.log')).write_bytes(p.stdout+p.stderr);results['mutant-'+name]=p.returncode;print(name,p.returncode,p.stdout.decode()[-220:]);assert p.returncode!=0
(L/'commands.json').write_text(json.dumps(commands,indent=2));(L/'results.json').write_text(json.dumps(results,indent=2))


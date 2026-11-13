from pathlib import Path
import hashlib,json,subprocess,sys
R=Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/ClimbRuntimeBindingsR8';D=R/'docs/agent-runs/20261104-three-chats/unity/ClimbRuntimeBindingsR8'
attempt=sys.argv[1];assert attempt.isalnum();O=S/attempt;O.mkdir();L=D/attempt;L.mkdir()
I=S/'inputs';A=I/'unity/Assets/Ashfall'
base=[A/'Core/Contracts.cs']
for part in ['Modules/Climbing','Modules/Player','Modules/Flight','Integration/FlightMotor','Integration/ClimbingMotor']:
 base+=list((A/part).rglob('*.cs'))
base+=list((R/'unity/Assets/Ashfall/Integration/ClimbPlayerBindings').rglob('*.cs'))
tests=I/'unity/Tools/Integration/ClimbingMotor'
commands=[];ok=True
def compile_run(name,sources,main,native=False):
 global ok
 out=O/(name+('.dll' if native else '.exe'));rsp=O/(name+'.rsp')
 args=['/nologo','/langversion:5','/warnaserror+','/target:'+('library' if native else 'exe'),'/out:"'+str(out)+'"']
 if main:args+=['/main:'+main]
 if native:
  old=(R/'campaign-staging/20261104-three-chats/unity/reviews/ClimbingTrajectoryReviewR7/NativeAPI.rsp').read_text()
  args+=['/define:UNITY_6000_0_OR_NEWER','/nostdlib+']+[line for line in old.splitlines() if line.startswith('/reference:') or line.startswith('/r:')]
  pkg=R/'campaign-staging/20261104-three-chats/unity/editor-probes/install-6000.6.8f1/project/Library/ScriptAssemblies'
  args+=['/reference:"'+str(pkg/name)+'"' for name in ['Unity.InputSystem.dll','Unity.XR.OpenXR.dll']]
 sealed=[]
 for idx,p in enumerate(sources):
  q=O/'sources'/name/(str(idx)+'-'+p.name);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(p.read_bytes());sealed.append(q)
 args+=['"'+str(p)+'"' for p in sealed];rsp.write_text('\n'.join(args))
 cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe']+(['/noconfig'] if native else [])+['@'+str(rsp)]
 run=subprocess.run(cmd,capture_output=True,text=True);(L/(name+'-compile.log')).write_text(run.stdout+run.stderr)
 row=dict(name=name,command=subprocess.list2cmdline(cmd),arguments=args,compile=run.returncode);commands.append(row)
 if run.returncode:ok=False;print(name,run.stdout,run.stderr);return
 if not native:
  run=subprocess.run([str(out)],capture_output=True,text=True);row.update(run=run.returncode,output=run.stdout+run.stderr);(L/(name+'-run.log')).write_text(run.stdout+run.stderr);ok=ok and run.returncode==0;print(name,run.returncode,run.stdout,run.stderr)
 else: print(name,'compiler only PASS; UNEXECUTED')
compile_run('Bindings',base+[R/'unity/Tools/Integration/ClimbPlayerBindings/BindingTests.cs'],'BindingTests')
for name,main in [('Composition','CompositionTests'),('R6','R6Tests'),('R7','R7Tests')]:
 compile_run(name,base+[tests/(main+'.cs')],main)
# Actual installed references, no stubs. Driver needs InputSystem and package references
# from the sealed R7 recipe; this compile also includes the actual Driver source.
compile_run('NativeAPI',base+list((R/'unity/Tools/Integration/ClimbPlayerBindings/Engine').glob('*.cs')),None,True)
(L/'commands.json').write_text(json.dumps(commands,indent=2))
manifest=[]
for p in O.rglob('*'):
 if p.is_file():manifest.append(dict(path=str(p.relative_to(R)),sha256=hashlib.sha256(p.read_bytes()).hexdigest(),bytes=p.stat().st_size))
(L/'manifest.json').write_text(json.dumps(manifest,indent=2))
sys.exit(0 if ok else 1)

from pathlib import Path
import subprocess,hashlib,json,shutil,sys
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/PreparedPadOwnerR5';O=S/('run'+(sys.argv[1] if len(sys.argv)>1 else '01'));O.mkdir();D=R/'docs/agent-runs/20261104-three-chats/unity/PreparedPadOwnerR5'
C=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'); results=[];inputs=[];reference_records={}
def freeze(p):
 p=Path(p);q=O/'inputs'/(hashlib.sha256(str(p).encode()).hexdigest()[:12]+'-'+p.name);q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q);inputs.append({'path':str(p.relative_to(R)),'sha256':hashlib.sha256(q.read_bytes()).hexdigest()});return q
# Pin external sources exactly once. Only authorized owner candidates replace before snapshots.
base=S/'before/unity/Assets/Ashfall'
core=[freeze(p) for p in (base/'Core').rglob('*.cs')]
player=[freeze(p) for p in (base/'Modules/Player').rglob('*.cs')]
flight=[freeze(p) for p in (base/'Modules/Flight').rglob('*.cs') if p.name!='FlightMachine.cs']+[freeze(R/'unity/Assets/Ashfall/Modules/Flight'/n) for n in ['FlightMachine.cs','PreparedPadLaunch.cs']]
motor=[freeze(p) for p in (base/'Integration/FlightMotor').rglob('*.cs')]+[freeze(R/'unity/Assets/Ashfall/Integration/FlightMotor/PreparedPadFlight.cs')]
fixture=freeze(R/'unity/Tools/Integration/PreparedPadOwner/PreparedPadTests.cs')
def run(name,args,expected=0):
 p=subprocess.run(list(map(str,args)),capture_output=True,text=True);out=p.stdout+p.stderr;(O/(name+'.log')).write_text(subprocess.list2cmdline(list(map(str,args)))+'\n'+out);results.append(dict(name=name,argv=list(map(str,args)),exit=p.returncode,expected=expected));print(name,p.returncode,out[-180:]);return p.returncode

def pinref(p):
 p=Path(p)
 if str(p) not in reference_records:
  raw=p.read_bytes();h=hashlib.sha256(raw).hexdigest();q=O/'references'/(h[:12]+'-'+p.name);q.parent.mkdir(exist_ok=True);q.write_bytes(raw);reference_records[str(p)]={'snapshot':str(q),'sha256':h}
 return Path(reference_records[str(p)]['snapshot'])
compiler_sha=hashlib.sha256(C.read_bytes()).hexdigest()
def compile(name,files,refs=[],lib=True):
 out=O/(name+('.dll' if lib else '.exe'));rsp=O/(name+'.rsp');rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:'+('library' if lib else 'exe'),'/out:"'+str(out)+'"']+['/reference:"'+str(pinref(x))+'"' for x in [Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Core.dll'),Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.dll')]+refs]+['"'+str(x)+'"' for x in files]));assert run(name+'-compile',[C,'/noconfig','@'+str(rsp)])==0;return out
try:
 a=compile('Core',core);b=compile('Player',player,[a]);c=compile('Flight',flight,[a]);d=compile('FlightMotor',motor,[a,b,c]);refs=[a,b,c,d]
 run('PreparedPadTests',[compile('PreparedPadTests',[fixture],refs,False)])
 for name,path,expected in [('R7','unity/Tools/Integration/FlightMotor/r7/R7Tests.cs',0),('R6','unity/Tools/Integration/FlightMotor/r6/R6Tests.cs',0),('R5','unity/Tools/Integration/FlightMotor/r5/R5Tests.cs',0),('OriginalFlight','unity/Tools/Modules/Flight/FlightTests.cs',0),('OriginalFresh','campaign-staging/20261104-three-chats/unity/reviews/Flight-r3/Fresh.cs',1)]:
  f=freeze(R/path);run(name,[compile(name,[f],refs,False)],expected)
 native=freeze(R/'unity/Tools/Integration/PreparedPadOwner/PreparedPadNativeFixture.cs')
 managed=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data/Managed/UnityEngine')
 nativeRefs=[managed.parent.parent/'UnityReferenceAssemblies/unity-4.8-api/Facades/netstandard.dll',managed/'UnityEngine.CoreModule.dll',managed/'UnityEngine.PhysicsModule.dll']
 compile('NativeFixture-UNRUN',[native],refs+nativeRefs,True)
 # Compiled mutants use the same actual production closure and unchanged public fixture.
 for name,old,new in [('CooldownMutation','now+1.2','now+.01'),('LaunchMutation','float fall=0,clear=.3f','float fall=0,clear=0'),('WorldMutation','if(!p.World.ClearPadContact(p.Pad.Id,p.Face,p.Contact))','if(p.World==null)'),('AuthorityMutation','var r=commands.Validate(p.Context);','var r=RuleResult.Accept();'),('RevisionMutation','p.FlightRevision!=stateRevision','p.FlightRevision<0')]:
  source=next(p for p in flight if p.name.endswith('-PreparedPadLaunch.cs'));mut=O/(name+'.cs');assert old in source.read_text();mut.write_text(source.read_text().replace(old,new));run(name,[compile(name,core+player+[p for p in flight if p!=source]+[mut]+motor+[fixture],[],False)],1)
finally:
 (O/'references.json').write_text(json.dumps(reference_records,indent=2));(O/'compiler.json').write_text(json.dumps({'path':str(C),'sha256':compiler_sha,'unchanged':hashlib.sha256(C.read_bytes()).hexdigest()==compiler_sha},indent=2))
 (O/'results.json').write_text(json.dumps(results,indent=2));(O/'inputs.json').write_text(json.dumps(inputs,indent=2));(D/(O.name+'-results.json')).write_text(json.dumps(results,indent=2))
raise SystemExit(any(r['exit']!=r['expected'] for r in results))





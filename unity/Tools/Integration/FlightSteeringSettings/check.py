from pathlib import Path
import subprocess,hashlib,json,datetime,shutil
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/FlightSteeringSettingsR1';D=R/'docs/agent-runs/20261104-three-chats/unity/FlightSteeringSettingsR1';O=S/('run-'+datetime.datetime.now().strftime('%H%M%S%f'));O.mkdir();B=S/'before';C=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe');rows=[];inputs=[]
def pin(p):
 p=Path(p);q=O/'inputs'/(hashlib.sha256(str(p).encode()).hexdigest()[:12]+'-'+p.name);q.parent.mkdir(exist_ok=True);q.write_bytes(p.read_bytes());inputs.append(dict(path=str(p),snapshot=str(q),sha256=hashlib.sha256(q.read_bytes()).hexdigest()));return q
owned=['Modules/Flight/FlightMachine.cs','Modules/Flight/FlightSettingsSnapshot.cs','Integration/FlightMotor/FlightSettingsBinding.cs','Integration/PersonalSettings/FlightSettingsPort.cs','Integration/PersonalSettings/PlayerSettingsBinding.cs']
def sources(folder):
 base='unity/Assets/Ashfall/';result=[p for p in (B/base/folder).rglob('*.cs') if str(p.relative_to(B/base)).replace('\\','/') not in owned];result += [R/base/x for x in owned if x.startswith(folder+'/')];return [pin(p) for p in result]
def call(name,args,expected=0,execute=True):
 p=subprocess.run(list(map(str,args)),capture_output=True,text=True);out=p.stdout+p.stderr;rows.append(dict(name=name,argv=list(map(str,args)),exit=p.returncode,expected=expected,output=out));(O/(name+'.log')).write_text(out);print(name,p.returncode,out[-900:]);return p.returncode
sysrefs=[pin(Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319')/x) for x in ['System.dll','System.Core.dll']]
def compile(name,src,refs=[],lib=True,extra=[]):
 out=O/(name+('.dll' if lib else '.exe'));rsp=O/(name+'.rsp');rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:'+('library' if lib else 'exe'),'/out:"'+str(out)+'"']+['/r:"'+str(x)+'"' for x in sysrefs+refs]+extra+['"'+str(x)+'"' for x in src]));assert call(name+'-compile',[C,'/noconfig','@'+str(rsp)])==0;return out
try:
 core=sources('Core');player=sources('Modules/Player');flight=sources('Modules/Flight');motor=sources('Integration/FlightMotor');persist=sources('Modules/Persistence');presentation=sources('Modules/Presentation')
 a=compile('Core',core);b=compile('Player',player,[a]);c=compile('Flight',flight,[a]);e=compile('FlightMotor',motor,[a,b,c]);p=compile('Persistence',persist,[a]);v=compile('Presentation',presentation,[a])
 personal=[pin((R if n in ['PlayerSettingsBinding.cs','FlightSettingsPort.cs'] else B)/'unity/Assets/Ashfall/Integration/PersonalSettings'/n) for n in ['Ports.cs','RawSettingsJson.cs','PersonalSettingsAdapter.cs','TypedPlayerSettingsPort.cs','PlayerSettingsBinding.cs','FlightSettingsPort.cs','PersonalSettingsRuntimeHost.cs']]
 h=compile('PersonalSettings',personal,[a,b,c,e,p,v]);refs=[a,b,c,e,p,v,h];test=pin(R/'unity/Tools/Integration/FlightSteeringSettings/SteeringTests.cs');exe=compile('SteeringTests',[test],refs,False);call('SteeringTests',[exe,O/'data'])
 for name,path,expected in [('PreparedPadTests','unity/Tools/Integration/PreparedPadOwner/PreparedPadTests.cs',0),('R7','unity/Tools/Integration/FlightMotor/r7/R7Tests.cs',0),('R6','unity/Tools/Integration/FlightMotor/r6/R6Tests.cs',0),('R5','unity/Tools/Integration/FlightMotor/r5/R5Tests.cs',0),('OriginalFlight','unity/Tools/Modules/Flight/FlightTests.cs',0),('OriginalFresh','campaign-staging/20261104-three-chats/unity/reviews/Flight-r3/Fresh.cs',1)]:
  f=pin(R/path);call(name,[compile(name,[f],refs,False)],expected)
 # Separate DLL mutation lanes; unchanged external test executable.
 fm=next(x for x in flight if x.name.endswith('-FlightMachine.cs'))
 for name,old,new in [('HardcodedGain','bank*.715f*settings.GlideSteeringGain','bank*.715f*.875f'),('NoInvalidation','settingsRevision=revision;stateRevision++','settingsRevision=revision'),('NoAuthority','var result=commands.Validate(c);','var result=RuleResult.Accept();')]:
  lane=O/name;lane.mkdir();mut=lane/'FlightMachine.cs';text=fm.read_text();assert old in text;mut.write_text(text.replace(old,new));dll=compile(name+'Flight',[mut if x==fm else x for x in flight],[a]);
  for dep in refs+[exe]:shutil.copyfile(dep,lane/dep.name)
  # Mutant must retain production Flight assembly name for CLR resolution.
  rsp=lane/'Flight.rsp';rsp.write_text('\n'.join(['/nologo','/langversion:5','/warnaserror+','/target:library','/out:"'+str(lane/'Flight.dll')+'"','/r:"'+str(a)+'"']+['/r:"'+str(x)+'"' for x in sysrefs]+['"'+str(mut if x==fm else x)+'"' for x in flight]));assert call(name+'IdentityCompile',[C,'/noconfig','@'+str(rsp)])==0
  call(name,[lane/exe.name,lane/'data'],1)
 # Exact historical default algorithm versus candidate, 101 banks /303 float bits.
 probe=pin(R/'unity/Tools/Integration/FlightSteeringSettings/DefaultBits.cs')
 current=compile('DefaultBits',[probe],refs,False);call('DefaultBits',[current]);currentOutput=rows[-1]['output']
 lane=O/'historical-default';lane.mkdir()
 for dep in refs:shutil.copyfile(dep,lane/dep.name)
 oldflight=[pin(x) for x in (B/'unity/Assets/Ashfall/Modules/Flight').rglob('*.cs')]
 rsp=lane/'Flight.rsp';rsp.write_text('\n'.join(['/nologo','/langversion:5','/warnaserror+','/target:library','/out:"'+str(lane/'Flight.dll')+'"']+['/r:"'+str(x)+'"' for x in [a]+sysrefs]+['"'+str(x)+'"' for x in oldflight]));assert call('HistoricalDefaultCompile',[C,'/noconfig','@'+str(rsp)])==0
 shutil.copyfile(current,lane/current.name);call('HistoricalDefaultBits',[lane/current.name]);assert rows[-1]['output']==currentOutput
 print('Default exact303 float bits match historical before source')
 native=pin(R/'unity/Tools/Integration/PreparedPadOwner/PreparedPadNativeFixture.cs');u=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data');nr=[pin(u/'UnityReferenceAssemblies/unity-4.8-api/Facades/netstandard.dll'),pin(u/'Managed/UnityEngine/UnityEngine.CoreModule.dll'),pin(u/'Managed/UnityEngine/UnityEngine.PhysicsModule.dll')]
 compile('NativeFixture-UNRUN',[native,pin(R/'unity/Tools/Integration/FlightSteeringSettings/FlightSteeringNativeFixture.cs')],refs+nr)
finally:
 (O/'results.json').write_text(json.dumps(rows,indent=2));(O/'inputs.json').write_text(json.dumps(inputs,indent=2));(D/(O.name+'-results.json')).write_text(json.dumps(rows,indent=2));(D/'latest-run.txt').write_text(str(O));(D/'compiler.json').write_text(json.dumps(dict(path=str(C),sha256=hashlib.sha256(C.read_bytes()).hexdigest()),indent=2))
raise SystemExit(any(x['exit']!=x['expected'] for x in rows))



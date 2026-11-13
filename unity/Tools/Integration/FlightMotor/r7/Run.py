from pathlib import Path
import subprocess,json,hashlib,shutil,sys
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/FlightMotorR7';D=R/'docs/agent-runs/20261104-three-chats/unity/FlightMotorR7';O=S/('run'+(sys.argv[1] if len(sys.argv)>1 else '01'));O.mkdir(exist_ok=False)
C=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe');flags=['/nologo','/langversion:5','/warn:4','/warnaserror+'];results=[];inputs=[]
def capture(p):
 p=Path(p);h=hashlib.sha256(p.read_bytes()).hexdigest();q=O/'inputs'/(h[:16]+'-'+p.name);q.parent.mkdir(exist_ok=True)
 if not q.exists():shutil.copyfile(p,q)
 inputs.append(dict(path=str(p),snapshot=str(q),sha256=h));return q
def run(name,args,expected=0):
 args=list(map(str,args));p=subprocess.run(args,capture_output=True,text=True);(O/(name+'.log')).write_text(subprocess.list2cmdline(args)+'\nEXIT '+str(p.returncode)+'\n'+p.stdout+p.stderr);results.append(dict(name=name,argv=args,exit=p.returncode,expected=expected,output=p.stdout+p.stderr));print(name,p.returncode,(p.stdout+p.stderr)[-220:]);return p.returncode
def compile(name,files,refs=[],library=False):
 out=O/(name+('.dll' if library else '.exe'));rsp=O/(name+'.rsp');rsp.write_text('\n'.join(flags+['/target:'+('library' if library else 'exe'),'/out:"'+str(out)+'"']+['/reference:"'+str(p)+'"' for p in refs]+['"'+str(capture(p))+'"' for p in files]));assert run(name+'-compile',[C,'@'+str(rsp)])==0;return out
core=[R/'unity/Assets/Ashfall/Core/Contracts.cs'];player=list((R/'unity/Assets/Ashfall/Modules/Player').rglob('*.cs'));flight=list((R/'unity/Assets/Ashfall/Modules/Flight').rglob('*.cs'));motor=list((R/'unity/Assets/Ashfall/Integration/FlightMotor').rglob('*.cs'))
compile('GuardOFF',core+player+flight+motor+[R/'unity/Tools/Integration/FlightMotor/r7/FlightMotorR7NativeGates.cs'],library=True)
a=compile('Core',core,library=True);b=compile('Player',player,[a],True);c=compile('Flight',flight,[a],True);e=compile('FlightMotor',motor,[a,b,c],True)
refs=[a,b,c,e]
def test(name,path,expected=0,args=[]):run(name,[compile(name,[path],refs)]+args,expected)
for name,path,expect in [('R7','unity/Tools/Integration/FlightMotor/r7/R7Tests.cs',0),('R6','unity/Tools/Integration/FlightMotor/r6/R6Tests.cs',0),('R5','unity/Tools/Integration/FlightMotor/r5/R5Tests.cs',0),('R3','unity/Tools/Integration/FlightMotor/R3Tests.cs',0),('OriginalFresh','campaign-staging/20261104-three-chats/unity/reviews/Flight-r3/Fresh.cs',1),('CorrectedFresh','campaign-staging/20261104-three-chats/unity/FlightR4/r5/CorrectedFresh.cs',0),('Original','unity/Tools/Modules/Flight/FlightTests.cs',0),('R1','campaign-staging/20261104-three-chats/unity/reviews/Flight-r1/Adversarial.cs',0),('R2','unity/Tools/Modules/Flight/R2Tests.cs',0),('Independent','campaign-staging/20261104-three-chats/unity/reviews/FlightMotor-r6/Independent.cs',0)]:test(name,R/path,expect)
run('RejectedRetry',[O/'Independent.exe','overflow'])
# Rebuild every retained PlayerR6 response input; old snapshots remain immutable.
pd=R/'docs/agent-runs/20261104-three-chats/unity/PlayerR6';lookup={r['snapshot']:r['path'] for r in json.loads((pd/'compile-inputs.json').read_text())}
for command in json.loads((pd/'commands.json').read_text()):
 if len(command)!=2 or not command[1].startswith('@'):continue
 rsp=Path(command[1][1:]);files=[]
 for line in rsp.read_text().splitlines():
  if not line.startswith('"'):continue
  p=Path(lookup.get(line.strip('"'),line.strip('"')))
  if '\\Assets\\Ashfall\\Modules\\Player\\' in str(p):continue
  files.append(p)
 name='Player-'+rsp.stem
 run(name,[compile(name,files+player)],3762504530 if rsp.stem=='IntegrationR5' else 0)
 if rsp.stem=='IntegrationR5':
  oldtest=next(p for p in files if p.name.endswith('IntegrationR5Tests.cs'));correct=O/'CorrectedIntegrationR5Tests.cs';correct.write_text(oldtest.read_text().replace('Near(partial.Velocity.X,.5f)&&Near(partial.Velocity.Z,1)','Near(partial.Velocity.X,0)&&Near(partial.Velocity.Z,0)&&!partial.HasPhaseFeedback'));run('CorrectedIntegrationR5',[compile('CorrectedIntegrationR5',[p for p in files if p!=oldtest]+[correct]+player)])
# Actual Player gravity mutant; new R7 must fail behaviorally at its fixed oracle.
loc=R/'unity/Assets/Ashfall/Modules/Player/Locomotion.cs';mut=O/'MutantLocomotion.cs';mut.write_text(loc.read_text().replace('16.2f','32.4f'))
mb=compile('MutantPlayer',[p for p in player if p!=loc]+[mut],[a],True)
# Separate directory ensures CLR resolves the mutant under the original Player assembly identity.
# Compile an entire composed executable from real modules for this mutation lane.
run('PlayerGravityMutation',[compile('PlayerGravityMutation',core+[p for p in player if p!=loc]+[mut]+flight+motor+[R/'unity/Tools/Integration/FlightMotor/r7/R7Tests.cs'])],1)
run('IndependentPlayerGravityMutation',[compile('IndependentPlayerGravityMutation',core+[p for p in player if p!=loc]+[mut]+flight+motor+[R/'campaign-staging/20261104-three-chats/unity/reviews/FlightMotor-r6/Independent.cs'])],1)
oldMotor=S/'before/unity/Assets/Ashfall/Integration/FlightMotor/PlayerFlightMotor.cs'
run('RejectedPublicationMutation',[compile('RejectedPublicationMutation',core+player+flight+[p for p in motor if p.name!='PlayerFlightMotor.cs']+[oldMotor,R/'unity/Tools/Integration/FlightMotor/r7/R7Tests.cs'])],1)
(D/(O.name+'-results.json')).write_text(json.dumps(results,indent=2));(D/(O.name+'-compile-inputs.json')).write_text(json.dumps(inputs,indent=2))
raise SystemExit(any(r['exit']!=r['expected'] for r in results))

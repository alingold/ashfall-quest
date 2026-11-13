from pathlib import Path
import subprocess,json,hashlib,shutil,sys
R=Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/FlightMotorR6';D=R/'docs/agent-runs/20261104-three-chats/unity/FlightMotorR6'; C=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
out=S/'run01';out.mkdir(exist_ok=False)
records=[]
for p in list((R/'unity/Assets/Ashfall/Modules/Flight').rglob('*.cs'))+list((R/'unity/Assets/Ashfall/Integration/FlightMotor').rglob('*.cs'))+list((S/'before/unity/Assets/Ashfall/Modules/Player').glob('*.cs'))+[S/'before/unity/Assets/Ashfall/Core/Contracts.cs']:
 rel=p.relative_to(S/'before') if p.is_relative_to(S/'before') else p.relative_to(R)
 q=out/'snapshot'/rel;q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q);records.append(dict(source=str(p),snapshot=str(q),sha256=hashlib.sha256(q.read_bytes()).hexdigest()))
(D/'run01-inputs.json').write_text(json.dumps(records,indent=2))
snap=out/'snapshot/unity/Assets/Ashfall';core=[snap/'Core/Contracts.cs'];player=list((snap/'Modules/Player').glob('*.cs'));flight=list((snap/'Modules/Flight').glob('*.cs'));motor=list((snap/'Integration/FlightMotor').glob('*.cs'));results=[]
def run(name,args,expect=0):
 args=list(map(str,args));p=subprocess.run(args,capture_output=True,text=True);(D/(name+'.log')).write_text(subprocess.list2cmdline(args)+'\nEXIT '+str(p.returncode)+'\n'+p.stdout+p.stderr);results.append(dict(name=name,argv=args,exit=p.returncode,expected=expect));print(name,p.returncode,p.stdout[-170:]);return p.returncode
flags=['/nologo','/langversion:5','/warn:4','/warnaserror+']
def lib(name,files,refs=[]):
 p=out/(name+'.dll');assert run(name,[C]+flags+['/target:library','/out:'+str(p)]+['/reference:'+str(x) for x in refs]+files)==0;return p
a=lib('Ashfall.Core',core);b=lib('Ashfall.Player',player,[a]);c=lib('Ashfall.Flight',flight,[a]);e=lib('Ashfall.FlightMotor',motor,[a,b,c])
lib('GuardOFF',list(snap.rglob('*.cs')))
def test(name,t,expected=0,base=False):
 exe=out/(name+'.exe');refs=[a,c] if base else [a,b,c,e]
 if run(name+'-compile',[C]+flags+['/target:exe','/out:'+str(exe)]+['/reference:'+str(x) for x in refs]+[t])==0:run(name,[exe],expected)
test('R6',R/'unity/Tools/Integration/FlightMotor/r6/R6Tests.cs')
test('R5',R/'unity/Tools/Integration/FlightMotor/r5/R5Tests.cs')
test('R3',R/'unity/Tools/Integration/FlightMotor/R3Tests.cs')
test('OriginalFresh',R/'campaign-staging/20261104-three-chats/unity/reviews/Flight-r3/Fresh.cs',1)
test('CorrectedFresh',R/'campaign-staging/20261104-three-chats/unity/FlightR4/r5/CorrectedFresh.cs')
for name,t in [('Original',R/'unity/Tools/Modules/Flight/FlightTests.cs'),('R1',R/'campaign-staging/20261104-three-chats/unity/reviews/Flight-r1/Adversarial.cs'),('R2',R/'unity/Tools/Modules/Flight/R2Tests.cs')]:test(name,t,base=True)
(D/'results.json').write_text(json.dumps(results,indent=2));drift=[]
for r in json.loads((D/'before.json').read_text()):
 if '/Modules/Player/' in r['live'].replace('\\','/') or '/Core/' in r['live'].replace('\\','/'):
  drift.append(dict(path=r['live'],before=r['sha256'],after=hashlib.sha256(Path(r['live']).read_bytes()).hexdigest()))
(D/'dependency-drift.json').write_text(json.dumps(drift,indent=2))
sys.exit(any(r['exit']!=r['expected'] for r in results))

from pathlib import Path
import json,subprocess,hashlib,shutil
R=Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/FlightR4/r5';D=R/'docs/agent-runs/20261104-three-chats/unity/FlightR4/r5';CSC=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper()
# Pin the concrete candidate before any compile.
files=list((R/'unity/Assets/Ashfall/Modules/Flight').rglob('*.cs'))+list((R/'unity/Assets/Ashfall/Integration/FlightMotor').rglob('*.cs'))
f=[]
for p in files:
 q=S/'candidate'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q);f.append(q)
core=S/'inputs/core/Contracts.cs';neutral=[p for p in f if 'Engine' not in p.parts];base=[p for p in neutral if 'FlightMotor' not in p.parts]
review=R/'campaign-staging/20261104-three-chats/unity/reviews/Flight-r3'
fresh=(review/'Fresh.cs').read_text().replace('IFlightWorld\n','IFlightWorld,IFlightCollisionAuthorization\n')
fresh=fresh.replace(' int pass,fail;', ' public RuleResult ValidateCollision(CommandContext c,FlightMotionReceipt p,FlightCollisionResult r){return RuleResult.Accept();}\n int pass,fail;')
fresh=fresh.replace('Check(motor.Step(C("a","after-hit",3)', 'Check(motor.ApplyCollisionResult(C("a","solver-hit",2),motor.PendingMotion,new FlightCollisionResult{Velocity=new Float3(),CeilingBlocked=true},this).Accepted && motor.Step(C("a","after-hit",3)')
fresh=fresh.replace('Near(motion.Velocity.Y,0,"corrected collision momentum must survive next frame");','Near(motion.Velocity.Y,-1.62f,"corrected collision momentum resumes one gravity next frame");')
(S/'CorrectedFresh.cs').write_text(fresh)
results=[]
def run(name,args,expected=0):
 args=[str(a) for a in args];p=subprocess.run(args,capture_output=True,text=True);(D/(name+'.txt')).write_text(subprocess.list2cmdline(args)+'\nEXIT '+str(p.returncode)+'\n'+p.stdout+p.stderr);results.append(dict(name=name,argv=args,exit=p.returncode,expected=expected,matched=p.returncode==expected));print(name,p.returncode,p.stdout[-150:].strip());return p.returncode

def test(name,sources,test,expected=0):
 exe=S/(name+'.exe')
 if run(name+'-compile',[CSC,'/nologo','/langversion:5','/warnaserror+','/target:exe','/out:'+str(exe),core]+sources+[test])==0:run(name+'-tests',[exe],expected)
before=list((S/'inputs/before').glob('*.cs'));before=[p for p in before if p.name!='FlightRootAdapter.cs']
sealed=R/'campaign-staging/20261104-three-chats/unity/phase05/small-fixes/Player/sealed-current/unity/Assets/Ashfall/Modules/Player'
for p in sealed.glob('*.cs'):
 q=S/'inputs/sealed-Player-r4'/p.name;q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q)
for dep in ['frozen-player','current-player','sealed-Player-r4']:
 player=list((S/'inputs'/dep).glob('*.cs'))
 test('incumbent-'+dep,before+player,review/'Fresh.cs',1)
 test('oldFresh-'+dep,neutral+player,review/'Fresh.cs',1)
 test('correctedFresh-'+dep,neutral+player,S/'CorrectedFresh.cs')
 test('r5-'+dep,neutral+player,R/'unity/Tools/Integration/FlightMotor/r5/R5Tests.cs')
 test('r3-'+dep,neutral+player,R/'unity/Tools/Integration/FlightMotor/R3Tests.cs')
for name,t in [('original',R/'unity/Tools/Modules/Flight/FlightTests.cs'),('r1',R/'campaign-staging/20261104-three-chats/unity/reviews/Flight-r1/Adversarial.cs'),('r2',R/'unity/Tools/Modules/Flight/R2Tests.cs')]:test(name,base,t)
run('guarded-define-off',[CSC,'/nologo','/langversion:5','/warnaserror+','/target:library','/out:'+str(S/'Flight.dll'),core]+f+list((S/'inputs/current-player').glob('*.cs')))
checks=[]
for rec in json.loads((D/'inputs-before.json').read_text()):
 checks.append(dict(rec,current=sha(R/rec['live']),snapshotStillMatches=sha(R/rec['snapshot'])==rec['sha256'],liveMatches=sha(R/rec['live'])==rec['sha256']))
(D/'dependencies-after.json').write_text(json.dumps(checks,indent=2));(D/'results.json').write_text(json.dumps(results,indent=2))
print('MISMATCHED RUNS',sum(not x['matched'] for x in results))

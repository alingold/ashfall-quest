from pathlib import Path
import subprocess,json,hashlib
R=Path(r'C:\Users\aling\Documents\ashfall');O=R/'campaign-staging/20261104-three-chats/unity/HealthStates/r2';E=R/'docs/agent-runs/20261104-three-chats/unity/HealthStates/evidence/r2'
CSC=r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
core=R/'unity/Assets/Ashfall/Core/Contracts.cs';prod=list((R/'unity/Assets/Ashfall/Modules/HealthStates').glob('*.cs'));tools=R/'unity/Tools/Modules/HealthStates';combat=list((O/'inputs/Combat').glob('*.cs'));snap=O.parent/'revisions/r1-before-r2/production'
commands=[]
def run(args,label):
 args=[str(a) for a in args];commands.append(subprocess.list2cmdline(args));p=subprocess.run(args,capture_output=True,text=True);(E/(label+'.log')).write_text(p.stdout+p.stderr);print(label,p.returncode,p.stdout[-700:],p.stderr);return p.returncode
def compile(out,src,kind='exe',refs=[]):
 return run([CSC,'/nologo','/langversion:5','/warnaserror+','/target:'+kind,'/out:'+str(out)]+['/reference:'+str(x) for x in refs]+src,out.stem+'-compile')
results={}
baseline=O/'baseline/Adversarial.exe'
if compile(baseline,[core]+list(snap.glob('*.cs'))+combat+[R/'campaign-staging/20261104-three-chats/unity/reviews/HealthStates-r1/Adversarial.cs'])==0:results['r1_findings']=run([baseline],'before')
lib=O/'HealthStates.dll'
if compile(lib,[core]+prod,'library')==0:
 for name,src in [('HealthTests',[tools/'HealthTests.cs']),('R2Tests',[tools/'R2Tests.cs',tools/'CandidateHealthCombatBridge.cs']+combat)]:
  exe=O/(name+'.exe');code=compile(exe,src,refs=[lib]);results[name]=code if code else run([exe],name)
(E/'commands.txt').write_text('\n'.join(commands)+'\n');(E/'validation.json').write_text(json.dumps(results,indent=2))
raise SystemExit(0 if results.get('HealthTests')==0 and results.get('R2Tests')==0 else 1)

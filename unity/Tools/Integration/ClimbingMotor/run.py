from pathlib import Path
import subprocess,json,sys,hashlib,shutil
R=Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/ClimbingBackendR5';D=R/'docs/agent-runs/20261104-three-chats/unity/ClimbingBackendR5'
attempt=sys.argv[1];assert attempt.isalnum();O=S/attempt;L=D/attempt;O.mkdir();L.mkdir()
before=S/'before';frozen=R/'campaign-staging/20261104-three-chats/unity/Climbing/r4/sealed-inputs'
rows=[]
def run(name,sources,main):
 exe=O/(name+'.exe');rsp=O/(name+'.rsp');args=['/nologo','/langversion:5','/warnaserror+','/target:exe','/main:'+main,'/out:"'+str(exe)+'"']+['"'+str(p)+'"' for p in sources]
 rsp.write_text('\n'.join(args));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)]
 p=subprocess.run(cmd,capture_output=True,text=True);(L/(name+'-compile.log')).write_text(p.stdout+p.stderr)
 row=dict(name=name,command=subprocess.list2cmdline(cmd),arguments=args,compileExit=p.returncode);rows.append(row)
 if p.returncode:print(p.stdout+p.stderr);return False
 p=subprocess.run([str(exe)],capture_output=True,text=True);(L/(name+'-tests.log')).write_text(p.stdout+p.stderr);row.update(testCommand='"'+str(exe)+'"',testExit=p.returncode)
 print(name,p.returncode,p.stdout.splitlines()[-1] if p.stdout else '');return p.returncode==0
mod=Path('unity/Assets/Ashfall/Modules/Climbing');T=Path('unity/Tools/Modules/Climbing');reviews=Path('campaign-staging/20261104-three-chats/unity/reviews')
suites=[('Builder',[T/'ClimbingTests.cs'],'ClimbingTests'),('R2',[T/'ClimbingTests.cs',T/'R2Tests.cs'],'R2Tests'),('R1Reviewer',[reviews/'Climbing-r1/Adversarial.cs'],'Adversarial'),('R2Reviewer',[reviews/'Climbing-r2/Fresh.cs'],'Fresh'),('R3',[T/'ClimbingTests.cs',T/'r3/R3Tests.cs'],'R3Tests'),('Independent',[T/'ClimbingTests.cs',T/'r3/R3Tests.cs',reviews/'Climbing-r3/Independent.cs'],'Independent'),('Geometry',[T/'r4/GeometryTests.cs'],'GeometryTests')]
ok=True
for name,tests,main in suites:
 ok=run('frozen-r4-'+name,[frozen/'unity/Assets/Ashfall/Core/Contracts.cs']+list((frozen/mod).glob('*.cs'))+[frozen/t for t in tests],main) and ok
ok=run('frozen-r4-Adversarial',[frozen/'unity/Assets/Ashfall/Core/Contracts.cs']+list((frozen/mod).glob('*.cs'))+[R/reviews/'Climbing-r4/Adversarial.cs'],'AdversarialR4') and ok
sources=[before/'unity/Assets/Ashfall/Core/Contracts.cs']
for part in ['Modules/Player','Modules/Flight','Integration/FlightMotor']:
 sources+=list((before/'unity/Assets/Ashfall'/part).rglob('*.cs'))
sources+=list((R/'unity/Assets/Ashfall/Modules/Climbing').rglob('*.cs'))+list((R/'unity/Assets/Ashfall/Integration/ClimbingMotor').rglob('*.cs'))+[R/'unity/Tools/Integration/ClimbingMotor/CompositionTests.cs']
sources+=list((R/'unity/Tools/Integration/ClimbingMotor/Engine').glob('*.cs'))
# Seal exact code used by this attempt, never overwrite previous evidence.
sealed=[];manifest=[]
for p in sources:
 rel=p.relative_to(before) if p.is_relative_to(before) else p.relative_to(R);q=O/'sources'/rel;q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q);sealed.append(q)
 manifest.append(dict(original=str(p),sealed=str(q),sha256=hashlib.sha256(q.read_bytes()).hexdigest().upper()))
ok=run('Composition',sealed,'CompositionTests') and ok
(L/'commands.json').write_text(json.dumps(rows,indent=2));(L/'sources.json').write_text(json.dumps(manifest,indent=2))
sys.exit(0 if ok else 1)

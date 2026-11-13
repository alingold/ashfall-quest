from pathlib import Path
import subprocess,json,hashlib,sys
root=Path(__file__).resolve().parents[4]
base=root/'campaign-staging/20261104-three-chats/unity/ParityExpansion/r3'
evbase=root/'docs/agent-runs/20261104-three-chats/unity/ParityExpansion/r3'
n=1
while (base/('run-%02d'%n)).exists():n+=1
build=base/('run-%02d'%n);build.mkdir();ev=evbase/('run-%02d'%n);ev.mkdir(parents=True)
records=json.loads((base/'baseline-manifest.json').read_text())
sources=[base/'baseline-inputs'/x['path'] for x in records]
if '--current' in sys.argv:
 sources=[]
 known={x['path'] for x in records}
 for module in ['ParityHarness','Flight','Sessions','Networking','Pickups','Construction','WeaponHandling','Combat','HealthStates']:
  for path in sorted((root/('unity/Assets/Ashfall/Modules/'+module)).glob('*.cs')):
   rel=path.relative_to(root).as_posix()
   if rel not in known:records.append({'path':rel});known.add(rel)
 for x in records:
  d=(root/x['path']).read_bytes();dest=build/'inputs'/x['path'];dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(d);sources.append(dest);x['sha256']=hashlib.sha256(d).hexdigest()
for p in sorted((root/'unity/Assets/Ashfall/Integration/ParitySubjects').glob('*.cs'))+[root/'unity/Tools/Integration/ParityExpansion/Main.cs']:
 rel=p.relative_to(root);d=p.read_bytes();dest=build/'inputs'/rel;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(d);sources.append(dest);records.append({'path':rel.as_posix(),'sha256':hashlib.sha256(d).hexdigest()})
(ev/'consumed-sources.json').write_text(json.dumps(records,indent=2))
# Validate every immutable baseline byte before compiling; never silently refresh dependencies.
for rec,src in zip(records,sources):
 assert hashlib.sha256(src.read_bytes()).hexdigest()==rec['sha256'],str(src)
csc=r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
commands=[]
for name,main in [('Expansion','Ashfall.Tools.ParityExpansion.MainProgram'),('AcceptedLinked','Ashfall.Modules.ParityHarness.LinkedMain')]:
 exe=build/(name+'.exe');cmd=[csc,'/nologo','/target:exe','/langversion:5','/warn:4','/warnaserror+','/reference:System.Web.Extensions.dll','/main:'+main,'/out:'+str(exe)]+[str(p) for p in sources]
 if '--current' in sys.argv:cmd.insert(1,'/define:PARITY_CURRENT')
 (ev/(name+'-compile-command.txt')).write_text(subprocess.list2cmdline(cmd));p=subprocess.run(cmd,capture_output=True,text=True);(ev/(name+'-compile.log')).write_text(p.stdout+p.stderr)
 if p.returncode:print(p.stdout+p.stderr);raise SystemExit(p.returncode)
for label,exe,args,expected in [('expanded','Expansion',[],0),('repeat','Expansion',[],0),('strict','Expansion',['--strict'],2),('required-id','Expansion',['--omit-expansion'],2),('self-check','Expansion',['--self-check'],0),('accepted-linked','AcceptedLinked',[],0)]:
 cmd=[str(build/(exe+'.exe')),str(ev/(label+'.json'))]+args;p=subprocess.run(cmd,capture_output=True,text=True);(ev/(label+'.log')).write_text(p.stdout+p.stderr);commands.append({'label':label,'command':subprocess.list2cmdline(cmd),'exit':p.returncode,'expected':expected});print(label,p.returncode,p.stdout[-400:])
def semantics(path):
 obj=json.loads(path.read_text());obj.pop('StopwatchFrequency',None)
 for row in obj['Results']:
  for key in ['MinTicks','MedianTicks','MaxTicks']:row.pop(key)
 return obj
same=semantics(ev/'expanded.json')==semantics(ev/'repeat.json')
drift=[x['path'] for x in records if hashlib.sha256((root/x['path']).read_bytes()).hexdigest()!=x['sha256']]
result={'commands':commands,'repeat_semantics_equal':same,'live_dependency_drift':drift,'note':'Drift does not change tested frozen candidate; rebind and independent review required','build':str(build),'evidence':str(ev)}
(ev/'validation.json').write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2))
raise SystemExit(0 if same and all(x['exit']==x['expected'] for x in commands) else 1)




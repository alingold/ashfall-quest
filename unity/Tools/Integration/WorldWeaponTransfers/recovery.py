from pathlib import Path
import json,subprocess,sys,hashlib,shutil
r=Path.cwd();label=sys.argv[1];assert label.isalnum();s=r/'campaign-staging/20261104-three-chats/unity/WeaponTransferRecoveryR3'/label;s.mkdir(exist_ok=False);d=r/'docs/agent-runs/20261104-three-chats/unity/WeaponTransferRecoveryR3';base=r/'campaign-staging/20261104-three-chats/unity/WeaponTransferRecoveryR3/before'
prod=[];manifest=[]
for p in base.rglob('*.cs'):
 rel=p.relative_to(base);live=r/rel
 # Owned world route consumes new candidate; all other inputs use the frozen phase12 snapshot.
 if 'WorldWeaponTransfers' in rel.parts: continue
 q=s/'inputs'/rel;q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q);prod.append(q);manifest.append(dict(original=str(rel),snapshot=str(q.relative_to(r)),sha256=hashlib.sha256(q.read_bytes()).hexdigest()))
for p in (r/'unity/Assets/Ashfall/Integration/WorldWeaponTransfers').glob('*.cs'):
 q=s/'inputs'/p.name;shutil.copyfile(p,q);prod.append(q);manifest.append(dict(original=str(p.relative_to(r)),snapshot=str(q.relative_to(r)),sha256=hashlib.sha256(q.read_bytes()).hexdigest()))
logs=[]
def build(name,files,refs=[],main=None):
 out=s/name;out.mkdir();binary=out/('candidate.dll' if name=='library' else 'test.exe');args=['/nologo','/langversion:5','/warnaserror+','/target:'+('library' if name=='library' else 'exe'),'/out:'+str(binary)]+['/reference:'+str(x) for x in refs]+(['/main:'+main] if main else [])+[str(x) for x in files];rsp=out/'compile.rsp';rsp.write_text('\n'.join('"'+a+'"' for a in args));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];c=subprocess.run(cmd,capture_output=True,text=True);e=dict(command=cmd,exit=c.returncode,output=c.stdout+c.stderr);logs.append(e);print(e);assert c.returncode==0
 for ref in refs:shutil.copyfile(ref,out/ref.name)
 return binary
def run(binary,args):
 cmd=[str(binary)]+args;c=subprocess.run(cmd,capture_output=True,text=True);e=dict(command=cmd,exit=c.returncode,output=c.stdout+c.stderr);logs.append(e);print(e);assert c.returncode==0
try:
 dll=build('library',prod)
 tests=[]
 for name in ['WorldTests.cs','RecoveryTests.cs']:
  p=r/'unity/Tools/Integration/WorldWeaponTransfers'/name;q=s/name;shutil.copyfile(p,q);tests.append(q)
 old=build('retained',[tests[0]],[dll]);run(old,[])
 durable=s/'DurableWorldTests.cs';durable.write_text(tests[0].read_text().replace('var coordinator=new WorldWeaponCoordinator(lane,weapons,clock,geometry,100);','var journal=new WeaponTransferJournal(Environment.GetCommandLineArgs()[1],10000000);var coordinator=new WorldWeaponCoordinator(lane,weapons,clock,geometry,100,journal);').replace('Console.WriteLine("PASS "+checks','journal.Dispose();Console.WriteLine("PASS "+checks'))
 durableExe=build('durable-retained',[durable],[dll]);run(durableExe,[str(s/'full.journal')])
 exe=build('recovery',[tests[1]],[dll]);run(exe,['pressure',str(s/'pressure.journal')]);run(exe,['capacity',str(s/'capacity.journal')])
 for mode in ['decisionfail','ackfail']:run(exe,[mode,str(s/(mode+'.journal'))])
 for a,b in [('write','inspect'),('writeack','inspectack'),('prepare','inspectprepare')]:
  path=s/(a+'.journal');run(exe,[a,str(path)]);run(exe,[b,str(path)])
 raw=(s/'write.journal').read_bytes();bad=s/'torn.journal';bad.write_bytes(raw[:-1]);run(exe,['corrupt',str(bad)])
finally:
 (d/(label+'-commands.json')).write_text(json.dumps(logs,indent=2));(d/(label+'-inputs.json')).write_text(json.dumps(manifest,indent=2))

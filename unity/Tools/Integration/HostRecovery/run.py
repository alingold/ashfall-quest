from pathlib import Path
import sys,json,hashlib,subprocess
R=Path.cwd();D=R/'docs/agent-runs/20261104-three-chats/unity/CheckpointBundleR1';S=R/'campaign-staging/20261104-three-chats/unity/CheckpointBundleR1';O=S/sys.argv[1];O.mkdir(exist_ok=False);logs=[];dll={}
rows=json.loads((D/'inputs.json').read_text())
for r in rows:assert hashlib.sha256((R/r['snapshot']).read_bytes()).hexdigest()==r['sha256']
def src(folder):return sorted((S/'before/unity/Assets/Ashfall'/folder).glob('*.cs'))
def call(argv,name,expected=0):
 p=subprocess.run(argv,capture_output=True,text=True);logs.append(dict(name=name,argv=argv,exit=p.returncode,stdout=p.stdout,stderr=p.stderr));(O/'commands.json').write_text(json.dumps(logs,indent=2));print(name,p.returncode,p.stdout[-1500:]);assert p.returncode==expected;return p
csc=r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
def build(name,files,refs,exe=False,folder=None):
 dest=folder or O;dest.mkdir(exist_ok=True);target=dest/(name+('.exe' if exe else '.dll'));rsp=dest/(name+'.rsp');rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:'+('exe' if exe else 'library'),'/out:"'+str(target)+'"']+['/r:"'+str(dll[r])+'"' for r in refs]+['"'+str(p)+'"' for p in files]));call([csc,'@'+str(rsp)],name+' compile');dll[name]=target;return target
for name,folder,refs in [('Core','Core',[]),('RecoveryContracts','RecoveryContracts',['Core']),('Health','Modules/HealthStates',['Core','RecoveryContracts']),('Modes','Modules/Modes',['Core']),('Sessions','Modules/Sessions',['Core','RecoveryContracts','Health']),('MatchRounds','Integration/MatchRounds',['Core','RecoveryContracts','Modes','Health']),('MatchRecovery','Integration/MatchRecovery',['Core','RecoveryContracts','Health','Modes','Sessions','MatchRounds']),('Inventory','Modules/InventoryItems',['Core','RecoveryContracts']),('Weapon','Modules/WeaponHandling',['Core','RecoveryContracts']),('InventoryRecovery','Integration/InventoryRecovery',['Core','RecoveryContracts','Inventory','Weapon']),('Pickups','Modules/Pickups',['Core','RecoveryContracts'])]:build(name,src(folder),refs)
# Freeze candidate and tools before compilation, while all dependencies remain the original pin.
files=[]
for parent in ['unity/Assets/Ashfall/Integration/HostRecovery','unity/Tools/Integration/HostRecovery']:
 for p in (R/parent).glob('*.*'):
  q=O/'src'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(p.read_bytes());files.append(q)
refs=list(dll);candidate=[p for p in files if p.name=='CheckpointBundle.cs'];build('HostRecovery',candidate,refs);refs=list(dll);test=[p for p in files if p.name=='Tests.cs'];exe=build('Tests',test,refs,True)
call([str(exe),'capture',str(O)],'capture');call([str(exe),'cold',str(O)],'cold subprocess')
# Compiled production mutants with the same assembly identity and unchanged consumer.
import shutil
original=dll['HostRecovery']
for label,needle,repl in [('missing-binding','expected.Check(r);','/* omitted binding check */ expected.Owner.Check(r); RecoveryWire.Text(r);r.ReadInt64();r.ReadInt64();r.ReadInt64();r.ReadDouble();'),('missing-digest','RecoveryWire.Require(RecoveryWire.Hash(image)==expectedDigest);','/* omitted outer digest */')]:
 folder=O/label;folder.mkdir();text=candidate[0].read_text();assert needle in text;m=folder/'CheckpointBundle.cs';m.write_text(text.replace(needle,repl));build('HostRecovery',[m],[r for r in refs if r!='HostRecovery'],folder=folder)
 for r in refs:
  if r!='HostRecovery':shutil.copyfile(dll[r],folder/dll[r].name)
 shutil.copyfile(exe,folder/exe.name);call([str(folder/exe.name),'mutant-'+label,str(O)],label+' killed',1);dll['HostRecovery']=original
manifest=[]
for base in [O,R/'unity/Assets/Ashfall/Integration/HostRecovery',R/'unity/Tools/Integration/HostRecovery']:
 for p in base.rglob('*'):
  if p.is_file():manifest.append(dict(path=p.relative_to(R).as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest(),bytes=p.stat().st_size))
(O/'manifest.json').write_text(json.dumps(manifest,indent=2));(D/(O.name+'-result.json')).write_text(json.dumps(logs,indent=2))
drift=[dict(path=r['path'],before=r['sha256'],current=hashlib.sha256((R/r['path']).read_bytes()).hexdigest()) for r in rows if (R/r['path']).exists() and hashlib.sha256((R/r['path']).read_bytes()).hexdigest()!=r['sha256']];(D/(O.name+'-drift.json')).write_text(json.dumps(drift,indent=2))

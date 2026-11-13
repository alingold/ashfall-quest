from pathlib import Path
import hashlib,json,subprocess,shutil,sys
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/CheckpointClockEvidenceR2';D=R/'docs/agent-runs/20261104-three-chats/unity/CheckpointClockEvidenceR2'
O=S/sys.argv[1];O.mkdir(exist_ok=False);F=S/'before';dll={};logs=[]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
for row in json.loads((D/'inputs.json').read_text()):assert sha(F/row['path'])==row['sha256']
# Pin new owned overlays and tools before first compiler invocation.
for folder in ['unity/Assets/Ashfall/Integration/HostRecovery','unity/Tools/Integration/CheckpointClockEvidence']:
 for p in (R/folder).glob('*'):
  if p.is_file():
   q=O/'overlay'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q)
for rel in ['Modules/Sessions/CheckpointClockEvidence.cs','Modules/HealthStates/CheckpointClockEvidence.cs']:
 p=R/'unity/Assets/Ashfall'/rel;q=O/'overlay'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q)
def call(argv,label,expected=0):
 p=subprocess.run([str(x) for x in argv],capture_output=True,text=True);logs.append(dict(label=label,argv=[str(x) for x in argv],exit=p.returncode,stdout=p.stdout,stderr=p.stderr));(O/'commands.json').write_text(json.dumps(logs,indent=2));print(label,p.returncode,p.stdout[-1600:],flush=True);assert p.returncode==expected;return p
def src(folder):
 found={p.name:p for p in (F/'unity/Assets/Ashfall'/folder).glob('*.cs')}
 for p in (O/'overlay/unity/Assets/Ashfall'/folder).glob('*.cs'):found[p.name]=p
 return sorted(found.values())
def build(name,files,refs,exe=False,dest=None):
 dest=dest or O;dest.mkdir(exist_ok=True);target=dest/(name+('.exe' if exe else '.dll'));rsp=dest/(name+'.rsp')
 rsp.write_text('\n'.join(['/nologo',r'/r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.dll',r'/r:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Core.dll','/langversion:5','/warn:4','/warnaserror+','/target:'+('exe' if exe else 'library'),'/out:"'+str(target)+'"']+['/r:"'+str(dll[r])+'"' for r in refs]+['"'+str(p)+'"' for p in files]))
 call([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/noconfig','@'+str(rsp)],name+' compile');dll[name]=target;return target
graph=[('Core','Core',[]),('RecoveryContracts','RecoveryContracts',['Core']),('Health','Modules/HealthStates',['Core','RecoveryContracts']),('Modes','Modules/Modes',['Core']),('Sessions','Modules/Sessions',['Core','RecoveryContracts','Health']),('MatchRounds','Integration/MatchRounds',['Core','RecoveryContracts','Modes','Health']),('MatchRecovery','Integration/MatchRecovery',['Core','RecoveryContracts','Health','Modes','Sessions','MatchRounds']),('Inventory','Modules/InventoryItems',['Core','RecoveryContracts']),('Weapon','Modules/WeaponHandling',['Core','RecoveryContracts']),('InventoryRecovery','Integration/InventoryRecovery',['Core','RecoveryContracts','Inventory','Weapon']),('Pickups','Modules/Pickups',['Core','RecoveryContracts'])]
for name,folder,refs in graph:build(name,src(folder),refs)
build('HostRecovery',src('Integration/HostRecovery'),list(dll));refs=list(dll)
test=F/'unity/Tools/Integration/HostRecovery/Tests.cs';exe=build('Tests',[test],refs,True)
call([exe,'capture',O],'retained capture');call([exe,'cold',O],'retained cold')
helper=O/'Fixture.cs';helper.write_text(test.read_text().replace('class Tests','partial class Tests').replace('static int Main(','static int OriginalMain('))
ind=F/'campaign-staging/20261104-three-chats/unity/reviews/CheckpointBundleReviewR1/Independent.cs'
indExe=build('Independent',[helper,ind],refs,True);call([indExe,'capture',O],'retained51 warm');call([indExe,'cold',O],'retained51 cold')
evidence=build('Evidence',[helper,O/'overlay/unity/Tools/Integration/CheckpointClockEvidence/Tests.cs'],refs,True)
call([evidence,'warm',O],'owner evidence warm');call([evidence,'cold',O],'owner evidence cold process')
original=dll['HostRecovery'];candidate=src('Integration/HostRecovery')
for label,needle,repl in [('missing-binding','expected.Check(r);','expected.Owner.Check(r); RecoveryWire.Text(r);r.ReadInt64();r.ReadInt64();r.ReadInt64();r.ReadDouble();'),('missing-digest','RecoveryWire.Require(RecoveryWire.Hash(image)==expectedDigest);','/*mutated*/')]:
 dest=O/label;dest.mkdir();files=[]
 for p in candidate:
  q=dest/p.name;t=p.read_text();q.write_text(t.replace(needle,repl));files.append(q)
 build('HostRecovery',files,[r for r in refs if r!='HostRecovery'],dest=dest)
 for r in refs:
  if r!='HostRecovery':shutil.copyfile(dll[r],dest/dll[r].name)
 shutil.copyfile(exe,dest/exe.name);call([dest/exe.name,'mutant-'+label,O],label+' killed',1);dll['HostRecovery']=original
for label,needle,repl in [('missing-owner-time','RecoveryWire.Require(s.RecordedTime>=0&&h.RecordedTime>=0&&!Double.IsNaN(s.RecordedTime)&&!Double.IsInfinity(s.RecordedTime)&&s.RecordedTime==h.RecordedTime);','/* removed actual recorded clocks check */'),('missing-owner-census','var a=s.CopyCanonicalMembers();var b=h.CopyCanonicalMembers();RecoveryWire.Require(a.Length==b.Length);\n            for(int i=0;i<a.Length;i++)RecoveryWire.Require(a[i]==b[i]);\n            RecoveryWire.Require(Array.IndexOf(a,binding.Owner)>=0);','/* removed local owner census */')]:
 dest=O/label;dest.mkdir();files=[]
 for p in candidate:
  q=dest/p.name;t=p.read_text()
  if p.name=='PartialOwnerEvidence.cs':assert needle in t
  q.write_text(t.replace(needle,repl));files.append(q)
 build('HostRecovery',files,[r for r in refs if r!='HostRecovery'],dest=dest)
 for r in refs:
  if r!='HostRecovery':shutil.copyfile(dll[r],dest/dll[r].name)
 shutil.copyfile(evidence,dest/evidence.name);call([dest/evidence.name,'cold',O],label+' killed',1);dll['HostRecovery']=original
(D/(O.name+'-results.json')).write_text(json.dumps(logs,indent=2))



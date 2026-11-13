from pathlib import Path
import subprocess,shutil,json,hashlib,os
R=Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/CombatCheckpointBoundaryR4';base=S/'final01';out=S/'extra04';out.mkdir();log=[]
csc=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
def call(label,args):
 z=subprocess.run([str(x) for x in args],cwd=out,capture_output=True,text=True);log.append(dict(label=label,argv=[str(x) for x in args],exit=z.returncode,stdout=z.stdout,stderr=z.stderr));(out/'commands.json').write_text(json.dumps(log,indent=2));print(label,z.returncode,(z.stdout+z.stderr)[-1800:]);return z
def build(label,target,files,refs,extra=()):
 rsp=target.with_suffix('.rsp');rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:'+('exe' if target.suffix=='.exe' else 'library'),'/out:"'+str(target)+'"']+list(extra)+['/reference:"'+str(x)+'"' for x in refs]+['"'+str(x)+'"' for x in files]));return call(label,[csc]+(['/noconfig'] if '/nostdlib+' in extra else [])+['@'+str(rsp)])
for p in base.glob('*.dll'):shutil.copyfile(p,out/p.name)
tests=[]
for p in (R/'unity/Tools/Integration/CombatCheckpointBoundary').glob('*.*'):
 q=out/'tools'/p.name;q.parent.mkdir(exist_ok=True);shutil.copyfile(p,q)
os.environ['ASHFALL_CHECKPOINT_BINDING']=hashlib.sha256((base/'inputs.json').read_bytes()).hexdigest()
test=out/'tools/BoundaryTests.cs';refs=list(out.glob('*.dll'));exe=out/'Boundary.exe'
assert build('boundary',exe,[test],refs).returncode==0
assert call('boundary-run',[exe]).returncode==0
sources=list((base/'inputs/unity/Assets/Ashfall/Modules/HealthStates').glob('*.cs'))
for label,file,old,new in [('damage-null','MatchCheckpointRecords.cs','return value;}','return null;}'),('health-field-drift','Ports.cs','public sealed class HealthSnapshot\n    {','public sealed class HealthSnapshot\n    {\n        private int futureArchiveField;')]:
 m=out/label;m.mkdir()
 for dll in base.glob('*.dll'):shutil.copyfile(dll,m/dll.name)
 original=next(p for p in sources if p.name==file);p=m/file;s=original.read_text();assert old in s
 # A readonly initialized field avoids artificial unused/private-field warnings.
 new=new.replace('private int futureArchiveField;','public readonly int FutureArchiveField=1;')
 p.write_text(s.replace(old,new));src=[p if x==original else x for x in sources]
 assert build(label,m/'HealthStates.dll',src,[m/'Core.dll',m/'RecoveryContracts.dll']).returncode==0
 assert build(label+'-consumer',m/'Boundary.exe',[test],list(m.glob('*.dll'))).returncode==0
 assert call(label+'-killed',[m/'Boundary.exe']).returncode!=0
# Actual private Combat field drift must reject checkpoint capture.
m=out/'combat-field-drift';m.mkdir()
for dll in base.glob('*.dll'):shutil.copyfile(dll,m/dll.name)
cs=list((base/'inputs/unity/Assets/Ashfall/Modules/Combat').glob('*.cs'));original=next(p for p in cs if p.name=='Ports.cs');p=m/'Ports.cs';s=original.read_text();old='public sealed class HostHit\n    {';assert old in s;p.write_text(s.replace(old,old+'\n        public readonly int FutureArchiveField=1;'))
assert build('combat-field-drift',m/'Combat.dll',[p if x==original else x for x in cs],[m/'Core.dll',m/'HealthStates.dll',m/'RecoveryContracts.dll']).returncode==0
fixture=base/'Public-CheckpointTests.cs'
assert build('combat-drift-consumer',m/'Checkpoint.exe',[fixture],list(m.glob('*.dll'))).returncode==0
data=m/'data';data.mkdir();assert call('combat-drift-killed',[m/'Checkpoint.exe',data]).returncode!=0
# Installed API compile only: never execute this native fixture.
engine=Path(r'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data\Managed\UnityEngine\UnityEngine.CoreModule.dll')
native=out/'tools/NativeFixture.cs'
assert build('native-compile-only',out/'NativeFixture.dll',[native],refs+[engine,engine.parents[2]/'NetStandard/ref/2.1.0/netstandard.dll',engine.parents[2]/'NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll'],['/define:UNITY_6000_0_OR_NEWER','/nostdlib+']).returncode==0
(out/'manifest.json').write_text(json.dumps([dict(path=str(p.relative_to(R)),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in out.rglob('*') if p.is_file()],indent=2))




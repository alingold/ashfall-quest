from pathlib import Path
import json,shutil,subprocess,sys,os,hashlib
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/CombatProjectileArchiveR5';D=R/'docs/agent-runs/20261104-three-chats/unity/CombatProjectileArchiveR5';O=S/sys.argv[1];O.mkdir(exist_ok=False)
files=[]
for f in ['Core','RecoveryContracts','Modules/HealthStates','Modules/Combat','Modules/Persistence','Integration/CombatPublication']:
 for p in (S/'before/unity/Assets/Ashfall'/f).glob('*.cs'):
  rel=p.relative_to(S/'before');src=R/rel if f=='Modules/Combat' and p.name in ['CheckpointState.cs','LaunchCapabilities.cs'] else p
  q=O/'inputs'/rel;q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(src,q);files.append(q)
p=R/'unity/Assets/Ashfall/Modules/Combat/ProjectileArchiveRecords.cs';q=O/'inputs'/p.relative_to(R);shutil.copyfile(p,q);files.append(q)
p=S/'before/unity/Assets/Ashfall/Integration/CombatHealthBridge.cs';q=O/'inputs'/p.relative_to(S/'before');q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q);files.append(q)
log=[];dll={};csc=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
def run(args,label):
 z=subprocess.run([str(a) for a in args],cwd=O,capture_output=True,text=True);log.append(dict(label=label,argv=[str(a) for a in args],exit=z.returncode,stdout=z.stdout,stderr=z.stderr));(O/'commands.json').write_text(json.dumps(log,indent=2));print(label,z.returncode,(z.stdout+z.stderr)[-2500:]);return z.returncode
def build(n,src,refs,exe=False):
 dst=O/(n+('.exe' if exe else '.dll'));rsp=O/(n+'.rsp');rsp.write_text('\n'.join(['/nologo','/reference:C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.dll','/reference:C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.Core.dll','/langversion:5','/warn:4','/warnaserror+','/target:'+('exe' if exe else 'library'),'/out:"'+str(dst)+'"']+['/reference:"'+str(dll[x])+'"' for x in refs]+['"'+str(p)+'"' for p in src]));dll[n]=dst
 if run([csc,'/noconfig','@'+str(rsp)],n):sys.exit(1)
for f,refs in [('Core',[]),('RecoveryContracts',['Core']),('HealthStates',['Core','RecoveryContracts']),('Combat',['Core','HealthStates','RecoveryContracts']),('Persistence',['Core'])]:build(f,[p for p in files if p.parent.name==f],refs)
build('CombatPublication',[p for p in files if p.parent.name=='CombatPublication' or p.name=='CombatHealthBridge.cs'],list(dll))
refs=list(dll);os.environ['ASHFALL_CHECKPOINT_BINDING']='a'*64
for name in ['CheckpointTests','PublicationTests']:
 p=S/'before/unity/Tools/Integration/CombatPublication'/(name+'.cs');q=O/('Public-'+name+'.cs');q.write_text(p.read_text().replace('HealthService.PreparedPublicationLease lease;Check(health.TryReservePublication(1,out lease)','HealthService.PreparedAdvance lease;Check(health.PrepareAdvance(C(),out lease).Accepted'))
 build(name,[q],refs,True);data=O/(name+'-data');data.mkdir();run([dll[name],data],name+'-run')
q=O/'ArchiveTests.cs';shutil.copyfile(R/'unity/Tools/Integration/CombatProjectileArchive/ArchiveTests.cs',q);build('ArchiveTests',[q],refs,True)
run([dll['ArchiveTests'],'write',O/'cold.bin'], 'archive-write');run([dll['ArchiveTests'],'read',O/'cold.bin'],'archive-read')
rows=[]
for p in O.rglob('*'):
 if p.is_file():rows.append(dict(path=p.relative_to(R).as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
(O/'manifest.json').write_text(json.dumps(rows,indent=2));(D/(sys.argv[1]+'-results.json')).write_text(json.dumps(log,indent=2))
sys.exit(1 if any(x['exit'] for x in log) else 0)



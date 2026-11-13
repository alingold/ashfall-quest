from pathlib import Path
import hashlib,json,shutil,subprocess,sys
root=Path(__file__).resolve().parents[5]
stage=root/'campaign-staging/20261104-three-chats/unity/SourceExplosiveMechanicsR3'
report=root/'docs/agent-runs/20261104-three-chats/unity/SourceExplosiveMechanicsR3'
tool=Path(__file__).parent
out=stage/(sys.argv[1] if len(sys.argv)>1 else 'run02');out.mkdir(exist_ok=False)
frozen=out/'WeaponHandling';shutil.copytree(root/'unity/Assets/Ashfall/Modules/WeaponHandling',frozen)
shutil.copy2(tool/'Tests.cs',out/'Tests.cs')
shutil.copy2(Path(__file__),out/'run.py')
csc=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
records=[]
def run(args,expected=0):
 p=subprocess.run([str(a) for a in args],capture_output=True,text=True)
 records.append(dict(command=subprocess.list2cmdline([str(a) for a in args]),exit=p.returncode,stdout=p.stdout,stderr=p.stderr,expected=expected))
 (report/(out.name+'-commands.json')).write_text(json.dumps(records,indent=2))
 if p.returncode!=expected:raise RuntimeError(p.stdout+p.stderr)
 return p.stdout
def compile(name,sources,refs=(),exe=False):
 target=out/(name+('.exe' if exe else '.dll'))
 run([csc,'/nologo','/noconfig','/langversion:5','/warnaserror+','/reference:C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.dll','/reference:C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.Core.dll','/target:'+('exe' if exe else 'library'),'/out:'+str(target)]+['/reference:'+str(r) for r in refs]+list(sources))
 return target
def sources(path):return sorted(path.glob('*.cs'))
core=compile('Core',sources(stage/'Core'))
recovery=compile('RecoveryContracts',sources(stage/'RecoveryContracts'),[core])
weapon=compile('WeaponHandling',sources(frozen),[core,recovery])
before=compile('BeforeWeaponHandling',sources(stage/'before'),[core,recovery])
# An actual external consumer cannot compile against the pinned before API.
run([csc,'/nologo','/langversion:5','/warnaserror+','/target:library','/out:'+str(out/'AbsentAPIs.dll'),'/reference:'+str(core),'/reference:'+str(recovery),'/reference:'+str(before),out/'Tests.cs'],1)
shutil.copy2(tool/'SchemaProbe.cs',out/'SchemaProbe.cs')
old_probe=compile('SchemaBefore',[out/'SchemaProbe.cs'],[core,recovery,before],True)
new_probe=compile('SchemaAfter',[out/'SchemaProbe.cs'],[core,recovery,weapon],True)
assert run([old_probe])==run([new_probe]),'schema1 bytes changed'
test=compile('Tests',[out/'Tests.cs'],[core,recovery,weapon],True)
print(run([test]).strip())
# Retain existing public regression fixtures with their exact input bytes.
legacy=root/'unity/Tools/Modules/WeaponHandling'
for name in ['BehaviorTests','ReviewerRegressions','R2Tests','R3Tests']:
 src=[legacy/(name+'.cs')]
 if name in ['BehaviorTests','ReviewerRegressions','R2Tests']:src.append(legacy/'TestSpawn.cs')
 pinned=[]
 for p in src:shutil.copy2(p,out/p.name);pinned.append(out/p.name)
 exe=compile(name,pinned,[core,recovery,weapon],True)
 print(run([exe]).strip())
# Real compiled production mutations; each must fail the unchanged consumer.
for name,old,new in [('mint','s.Loaded--;','s.Loaded++;'),('refund','n.Reloads--;','n.Reloads++;'),('cooldown','seconds+.35','seconds+.035'),('exclusive','if(!o.ReserveOperation(token,expires,c.Tick))','if(false)')]:
 mutated=out/('mutant-'+name);shutil.copytree(frozen,mutated)
 p=mutated/'SourceExplosiveMechanics.cs';s=p.read_text();assert old in s;p.write_text(s.replace(old,new))
 # The exclusivity mutation would trigger unreachable-code warning under warnaserror;
 # use a nonconstant equivalent so it tests behavior rather than compiler rejection.
 if name=='exclusive':p.write_text(p.read_text().replace('if(false)','if(id=="impossible-reserved-id")'))
 dll=compile('Mutant-'+name,sources(mutated),[core,recovery])
 md=out/('execute-'+name);md.mkdir();shutil.copy2(core,md/core.name);shutil.copy2(recovery,md/recovery.name);shutil.copy2(dll,md/dll.name)
 exe=compile('Test-'+name,[out/'Tests.cs'],[core,recovery,dll],True);shutil.copy2(exe,md/exe.name)
 result=subprocess.run([str(md/exe.name)],capture_output=True,text=True)
 records.append(dict(mutation=name,exit=result.returncode,stdout=result.stdout,stderr=result.stderr))
 (report/(out.name+'-commands.json')).write_text(json.dumps(records,indent=2))
 assert result.returncode!=0,'survived '+name
manifest=[]
for p in sorted(stage.rglob('*')):
 if p.is_file():manifest.append(dict(path=str(p.relative_to(root)),bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
(report/'manifest.json').write_text(json.dumps(manifest,indent=2))
print('PASS four compiled mutation kills')

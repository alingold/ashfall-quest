from pathlib import Path
import hashlib,json,subprocess,datetime
ROOT=Path(__file__).resolve().parents[4]
OUT=ROOT/'campaign-staging/20261104-three-chats/unity/SettingsMigrationRuntimeR6'
DOC=ROOT/'docs/agent-runs/20261104-three-chats/unity/SettingsMigrationRuntimeR6'
run=OUT/datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
run.mkdir(parents=True)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
inputs=[ROOT/'unity/Assets/Ashfall/Core/Contracts.cs']
for folder in ['unity/Assets/Ashfall/Modules/Persistence','unity/Assets/Ashfall/Modules/SaveMigration','unity/Assets/Ashfall/Integration/MigrationRuntime','unity/Tools/Integration/MigrationRuntime']:
 inputs+=list((ROOT/folder).glob('*.*'))
inputs+=list((ROOT/'unity/Documentation').glob('*.md'))
coord=ROOT/'docs/agent-runs/20261104-three-chats/unity'
inputs += [coord/f'accepted-neutral-v{i}.json' for i in range(1,7)]
inputs += [coord/'phase21/ROOT-REPORT.md',coord/'phase21/source-index.json',coord/'GAMEPLAY-CHECKPOINT-v1.md']
records=[]
for p in inputs:
 rel=p.relative_to(ROOT); dest=run/'frozen'/rel;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(p.read_bytes())
 records.append(dict(path=str(rel),sha256=sha(dest),bytes=dest.stat().st_size,snapshot=str(dest.relative_to(ROOT))))
(run/'inputs.json').write_text(json.dumps(records,indent=2))
# Inspect all acceptance records, verify exact dependency identity rather than borrowing status.
accepted=json.loads((coord/'accepted-neutral-v1.json').read_text(encoding='utf-8-sig'))
for p in (ROOT/'unity/Assets/Ashfall/Modules/Persistence').glob('*.cs'):
 assert any(x.get('expected')==sha(p) for x in accepted['sourceChecks']),p
r5=json.loads((coord/'phase06/SaveMigration-r5/candidate-manifest.json').read_text())
for p in (ROOT/'unity/Assets/Ashfall/Modules/SaveMigration').glob('*.cs'):
 assert any(x['path'].replace('\\','/').endswith('sealed-current/'+str(p.relative_to(ROOT)).replace('\\','/')) and x['sha256']==sha(p) for x in r5['files']),p
summary=[]
for i in range(1,7):
 a=json.loads((coord/f'accepted-neutral-v{i}.json').read_text(encoding='utf-8-sig'))
 summary.append(dict(version=i,status=a.get('status'),accepted=[{k:v for k,v in x.items() if k in ['name','scope','review','manifest']} for x in a.get('accepted',[])]))
(run/'acceptance-summary.json').write_text(json.dumps(summary,indent=2))
csc=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
receipts=[]
def execute(cmd,name,expected=0):
 p=subprocess.run(list(map(str,cmd)),cwd=ROOT,capture_output=True,text=True)
 (run/(name+'.log')).write_text(p.stdout+p.stderr)
 receipts.append(dict(argv=list(map(str,cmd)),exitCode=p.returncode,log=name+'.log'))
 (run/'commands.json').write_text(json.dumps(receipts,indent=2))
 print(name,p.returncode,(p.stdout+p.stderr)[-1800:])
 if expected is not None:assert p.returncode==expected,name
 return p.returncode
def build(name,sources,refs=(),exe=False):
 target=run/(name+('.exe' if exe else '.dll'))
 args=['/nologo','/langversion:5','/warnaserror+','/target:'+('exe' if exe else 'library'),'/out:'+str(target)]
 args+=['/r:C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.dll','/r:C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.Core.dll']+['/r:'+str(r) for r in refs]+[str(p) for p in sources]
 rsp=run/(name+'.rsp');rsp.write_text('\n'.join('"'+a+'"' for a in args))
 execute([csc,'/noconfig','@'+str(rsp)],name+'-compile')
 return target
f=run/'frozen'
core=build('Core',[f/'unity/Assets/Ashfall/Core/Contracts.cs'])
persist=build('Persistence',list((f/'unity/Assets/Ashfall/Modules/Persistence').glob('*.cs')),[core])
migrate=build('SaveMigration',list((f/'unity/Assets/Ashfall/Modules/SaveMigration').glob('*.cs')),[core,persist])
source=f/'unity/Assets/Ashfall/Integration/MigrationRuntime/SettingsMigrationRuntime.cs'
wrapper=build('MigrationRuntime',[source],[core,persist,migrate])
testsource=f/'unity/Tools/Integration/MigrationRuntime/RuntimeTests.cs'
tests=build('RuntimeTests',[testsource],[core,persist,migrate,wrapper],True)
fixtures=run/'fixtures'
execute([tests,'normal',fixtures],'public-tests')
execute([tests,'cold',fixtures],'cold-process')
mutants={
 'foreign-preview':('!Object.ReferenceEquals(preview.Owner,this)','false'),
 'source-snapshot':('return b==null?null:(byte[])b.Clone(); }','return b; }'),
 'profile-selection':('Lobby(Absolute(source),Profile,approvedMaps)','Lobby(Absolute(source),SourceProfile.SkinLibrary,approvedMaps)'),
 'tuning-kind':('kind==MigrationKind.WeaponTiming),s.Bytes)','false),s.Bytes)'),
 'io-classification':('observed.Failed?CommitState.IoFailure','observed.Failed?CommitState.Refused'),
 'conflict-classification':('observed.Occupied?CommitState.Conflict','observed.Occupied?CommitState.Refused')}
mutation=[]
for name,(old,new) in mutants.items():
 assert old in source.read_text(),name
 md=run/'mutants'/name;md.mkdir(parents=True)
 ms=md/'SettingsMigrationRuntime.cs';ms.write_text(source.read_text().replace(old,new))
 # Keep assembly identity so the same separately built consumer exercises mutant behavior.
 args=['/nologo','/langversion:5','/warnaserror+','/target:library','/out:'+str(md/'MigrationRuntime.dll'),'/r:C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.dll','/r:C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.Core.dll']+['/r:'+str(x) for x in [core,persist,migrate]]+[str(ms)]
 rsp=md/'compile.rsp';rsp.write_text('\n'.join('"'+a+'"' for a in args));execute([csc,'/noconfig','@'+str(rsp)],name+'-compile')
 for p in [core,persist,migrate,tests]:(md/p.name).write_bytes(p.read_bytes())
 code=execute([md/tests.name,'normal',md/'fixtures'],name+'-test',None)
 mutation.append(dict(name=name,killed=code!=0))
 assert code!=0,'surviving mutant '+name
(run/'mutations.json').write_text(json.dumps(mutation,indent=2))
drift=[dict(path=x['path'],before=x['sha256'],after=sha(ROOT/x['path'])) for x in records if sha(ROOT/x['path'])!=x['sha256']]
(run/'drift.json').write_text(json.dumps(drift,indent=2))
outputs=[dict(path=str(p.relative_to(ROOT)),sha256=sha(p),bytes=p.stat().st_size) for p in run.rglob('*') if p.is_file()]
(DOC/(run.name+'-manifest.json')).write_text(json.dumps(dict(inputs=records,outputs=outputs,drift=drift),indent=2))
print('EVIDENCE',run)

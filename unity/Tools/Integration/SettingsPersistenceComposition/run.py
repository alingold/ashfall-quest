from pathlib import Path
import json,hashlib,subprocess,shutil,datetime
R=Path(__file__).resolve().parents[4]
B=R/'campaign-staging/20261104-three-chats/unity/SettingsPersistenceCompositionR7'
O=B/datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ');O.mkdir()
F=O/'frozen';shutil.copytree(B/'before',F)
# Accepted dependencies are immutable; pin once, report drift separately at end.
records=json.loads((B/'before-inputs.json').read_text())
accepted=json.loads((F/'docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v1.json').read_text(encoding='utf-8-sig'))
manifest=json.loads((F/'docs/agent-runs/20261104-three-chats/unity/phase06/SaveMigration-r5/candidate-manifest.json').read_text())
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
for p in (F/'unity/Assets/Ashfall/Modules/Persistence').glob('*.cs'):assert any(x.get('expected')==sha(p) for x in accepted['sourceChecks'])
for p in (F/'unity/Assets/Ashfall/Modules/SaveMigration').glob('*.cs'):assert any(x['path'].replace('\\','/').endswith('/'+p.name) and x['sha256']==sha(p) for x in manifest['files'])
for folder in ['unity/Assets/Ashfall/Integration/MigrationRuntime','unity/Tools/Integration/SettingsPersistenceComposition']:
 for p in (R/folder).glob('*'):
  if p.is_file():d=F/p.relative_to(R);d.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,d)
inputs=[dict(path=str(p.relative_to(F)),sha256=sha(p)) for p in F.rglob('*') if p.is_file()]
(O/'inputs.json').write_text(json.dumps(inputs,indent=2))
csc=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe');commands=[]
def go(cmd,name,expected=0):
 p=subprocess.run(list(map(str,cmd)),capture_output=True,text=True,cwd=R);(O/(name+'.log')).write_text(p.stdout+p.stderr)
 commands.append(dict(argv=list(map(str,cmd)),exitCode=p.returncode,log=name+'.log'));(O/'commands.json').write_text(json.dumps(commands,indent=2));print(name,p.returncode,(p.stdout+p.stderr)[-300:])
 if expected is not None:assert p.returncode==expected,name
 return p.returncode
def build(name,sources,refs=(),exe=False,dest=None):
 d=dest or O;d.mkdir(parents=True,exist_ok=True);target=d/(name+('.exe' if exe else '.dll'))
 args=['/nologo','/langversion:5','/warnaserror+','/target:'+('exe' if exe else 'library'),'/out:'+str(target)]+['/r:C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\'+x+'.dll' for x in ['System','System.Core']]+['/r:'+str(p) for p in refs]+list(map(str,sources))
 rsp=d/(name+'.rsp');rsp.write_text('\n'.join('"'+a+'"' for a in args));go([csc,'/noconfig','@'+str(rsp)],str(d.relative_to(O)).replace('\\','-')+'-'+name+'-compile');return target
core=build('Core',[F/'unity/Assets/Ashfall/Core/Contracts.cs'])
persistence=list((F/'unity/Assets/Ashfall/Modules/Persistence').glob('*.cs'))
persist=build('Persistence',persistence,[core]);migration=build('SaveMigration',list((F/'unity/Assets/Ashfall/Modules/SaveMigration').glob('*.cs')),[core,persist])
sources=list((F/'unity/Assets/Ashfall/Integration/MigrationRuntime').glob('*.cs'));refs=[core,persist,migration]
wrapper=build('MigrationRuntime',sources,refs);allrefs=refs+[wrapper]
review=F/'campaign-staging/20261104-three-chats/unity/reviews/SettingsMigrationRuntimeReviewR6'
tests={}
for name,src in [('RuntimeTests',F/'unity/Tools/Integration/MigrationRuntime/RuntimeTests.cs'),('IndependentTests',review/'IndependentTests.cs'),('PartialWriteTests',review/'PartialWriteTests.cs'),('ReconciliationTests',F/'unity/Tools/Integration/SettingsPersistenceComposition/ReconciliationTests.cs')]:
 tests[name]=build(name,[src],allrefs,True)
 go([tests[name]]+(['normal',O/'retained-fixtures'] if name=='RuntimeTests' else [O/(name+'-fixtures')]),name)
for i in range(3):go([tests['RuntimeTests'],'cold',O/'retained-fixtures'],'cold-'+str(i))
for i in range(3):go([tests['ReconciliationTests'],O/'ReconciliationTests-fixtures','cold'],'reconciliation-cold-'+str(i))
mutants=[('foreign-preview','SettingsMigrationRuntime.cs','!Object.ReferenceEquals(preview.Owner,this)','false','RuntimeTests'),('source-snapshot','SettingsMigrationRuntime.cs','return b==null?null:(byte[])b.Clone(); }','return b; }','RuntimeTests'),('profile-selection','SettingsMigrationRuntime.cs','Lobby(Absolute(source),Profile,approvedMaps)','Lobby(Absolute(source),SourceProfile.SkinLibrary,approvedMaps)','RuntimeTests'),('tuning-kind','SettingsMigrationRuntime.cs','kind==MigrationKind.WeaponTiming),s.Bytes)','false),s.Bytes)','RuntimeTests'),('io-classification','SettingsMigrationRuntime.cs','observed.Failed?CommitState.IoFailure','observed.Failed?CommitState.Refused','RuntimeTests'),('conflict-classification','SettingsMigrationRuntime.cs','observed.Occupied?CommitState.Conflict','observed.Occupied?CommitState.Refused','RuntimeTests'),('pending-verification','AtomicSettingsStore.cs','if (Hash(files.Read(pending, MaxDocumentBytes)) != Hash(bytes))','if (pending == null)','IndependentTests'),('precommit-conflict','AtomicSettingsStore.cs','if ((latest == null ? null : Hash(latest)) != observed)','if (pending == null)','IndependentTests'),('false-rollback','MigrationReconciliation.cs','!receipt.PublicationAttempted&&receipt.CommitState==CommitState.IoFailure','receipt.CommitState==CommitState.IoFailure','ReconciliationTests'),('skip-byte-binding','MigrationReconciliation.cs','if(hash!=receipt.ExpectedSha256)','if(hash==null)','ReconciliationTests'),('skip-backup','MigrationReconciliation.cs','files.Exists(Destination+".backup")','Destination==null','ReconciliationTests'),('skip-repeat-read','MigrationReconciliation.cs','Digest(files.Read(Destination,AtomicSettingsStore.MaxDocumentBytes))!=hash','hash==null','ReconciliationTests')]
results=[]
for name,file,old,new,test in mutants:
 d=O/'mutants'/name;d.mkdir(parents=True);ps=persistence if file=='AtomicSettingsStore.cs' else sources;ms=[]
 for p in ps:
  text=p.read_text()
  if p.name==file:assert old in text; text=text.replace(old,new)
  out=d/p.name;out.write_text(text);ms.append(out)
 target=build('Persistence' if file=='AtomicSettingsStore.cs' else 'MigrationRuntime',ms,[core] if file=='AtomicSettingsStore.cs' else refs,dest=d)
 for p in allrefs+[tests[test]]:
  if p.name!=target.name:shutil.copy2(p,d/p.name)
 args=['normal',d/'fixtures'] if test=='RuntimeTests' else [d/'fixtures']
 code=go([d/tests[test].name]+args,name+'-test',None);assert code!=0,'SURVIVED '+name;results.append(dict(name=name,killed=True))
(O/'mutations.json').write_text(json.dumps(results,indent=2))
(O/'drift.json').write_text(json.dumps([dict(path=x['path'],before=x['sha256'],after=sha(R/x['path'])) for x in records if sha(R/x['path'])!=x['sha256'].upper()],indent=2))
(B/'latest.txt').write_text(str(O));print('EVIDENCE',O)


from pathlib import Path
import subprocess,json,hashlib,datetime
r=Path(__file__).resolve().parents[4]
e=r/'docs/agent-runs/20261104-three-chats/unity/SaveMigration/phase03'
o=r/'campaign-staging/20261104-three-chats/unity/SaveMigration/phase03'
run=datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ'); out=o/'runs'/run;out.mkdir(parents=True)
records=json.loads((e/'inputs.json').read_text())
for row in records:
 p=r/row['snapshot'];assert hashlib.sha256(p.read_bytes()).hexdigest().upper()==row.get('snapshotSha256',row['sha256']),row['path']
accepted=json.loads((o/'inputs/docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v1.json').read_text(encoding='utf-8-sig'))
for row in records:
 if '/Modules/Persistence/' in row['path']:
  assert any(x.get('expected')==row['sha256'] and x.get('actual')==row['sha256'] for x in accepted['sourceChecks']), 'check accepted manifest field names'
csc='C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
common=[csc,'/nologo','/langversion:5','/warnaserror+']
core=o/'inputs/unity/Assets/Ashfall/Core/Contracts.cs'
persistence=list((o/'inputs/unity/Assets/Ashfall/Modules/Persistence').glob('*.cs'))
commands=[common+['/target:library','/out:'+str(out/'AcceptedPersistence.dll'),str(core)]+list(map(str,persistence)),common+['/target:exe','/r:'+str(out/'AcceptedPersistence.dll'),'/out:'+str(out/'MigrationTests.exe')]+list(map(str,(r/'unity/Assets/Ashfall/Modules/SaveMigration').glob('*.cs')))+[str(r/'unity/Tools/Modules/SaveMigration/MigrationTests.cs')],[str(out/'MigrationTests.exe'),str(out/'fixtures')]]
receipts=[]
for i,cmd in enumerate(commands):
 p=subprocess.run(cmd,capture_output=True,text=True,cwd=r); log=e/(run+'-'+str(i)+'.log');log.write_text(p.stdout+p.stderr)
 receipts.append(dict(argv=cmd,command=subprocess.list2cmdline(cmd),exitCode=p.returncode,log=str(log.relative_to(r))))
 print(p.stdout+p.stderr)
 (e/(run+'-commands.json')).write_text(json.dumps(receipts,indent=2))
 if p.returncode:raise SystemExit(p.returncode)
print('Run:',run)


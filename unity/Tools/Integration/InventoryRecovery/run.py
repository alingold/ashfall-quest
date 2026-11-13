from pathlib import Path
import json,subprocess,sys,hashlib,shutil
r=Path.cwd();label=sys.argv[1];assert label.isalnum();base=r/'campaign-staging/20261104-three-chats/unity/InventoryRecoveryR1';out=base/label;out.mkdir();docs=r/'docs/agent-runs/20261104-three-chats/unity/InventoryRecoveryR1'/label;docs.mkdir()
records=json.loads((docs.parent/'before.json').read_text());files=[];inputs=[]
owned=[r/'unity/Assets/Ashfall/Modules/InventoryItems/CheckpointState.cs',r/'unity/Assets/Ashfall/Modules/WeaponHandling/CheckpointState.cs',r/'unity/Assets/Ashfall/Modules/WeaponHandling/FiniteAmmoReserve.cs']+list((r/'unity/Assets/Ashfall/Integration/InventoryRecovery').glob('*.cs'))
for record in records:
 if record['path'].endswith('FiniteAmmoReserve.cs'):continue
 files.append(Path(record['snapshot']))
files+=owned
frozen=[]
for i,p in enumerate(files):
 t=out/'src'/('%02d_'%i+p.name);t.parent.mkdir(exist_ok=True);shutil.copyfile(p,t);frozen.append(t);inputs.append({'source':str(p),'snapshot':str(t),'sha256':hashlib.sha256(t.read_bytes()).hexdigest()})
(docs/'inputs.json').write_text(json.dumps(inputs,indent=2));results=[]
def run(cmd):
 p=subprocess.run(cmd,capture_output=True,text=True);results.append({'command':cmd,'exit':p.returncode,'stdout':p.stdout,'stderr':p.stderr});(docs/'results.json').write_text(json.dumps(results,indent=2));print("exit",p.returncode,"PASS lines",p.stdout.count("PASS "),p.stderr[-1200:]);p.check_returncode()
csc=r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe';dll=out/'Actual.dll';rsp=out/'compile.rsp';rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:library','/out:"'+str(dll)+'"']+['"'+str(p)+'"' for p in frozen]));run([csc,'@'+str(rsp)])
test=r/'unity/Tools/Integration/InventoryRecovery/Tests.cs';shutil.copyfile(test,out/'Tests.cs');rsp=out/'tests.rsp';exe=out/'Tests.exe';rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/r:"'+str(dll)+'"','/out:"'+str(exe)+'"','"'+str(out/'Tests.cs')+'"']));run([csc,'@'+str(rsp)]);run([str(exe),str(out)])

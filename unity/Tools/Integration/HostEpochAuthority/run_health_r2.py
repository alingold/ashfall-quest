from health_epoch_r2 import R,D,S
from pathlib import Path
import subprocess,json,shutil,hashlib,sys
CSC=r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
out=S/sys.argv[1];out.mkdir()
rows=[];results=[]
for p in (R/'unity/Assets/Ashfall').rglob('*.cs'):
 if sys.argv[1]=='baseline' and not (S/'before'/p.relative_to(R)).exists():continue
 q=out/'source'/p.relative_to(R/'unity/Assets/Ashfall');q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q)
 if sys.argv[1]=='baseline':shutil.copyfile(S/'before'/p.relative_to(R),q)
 rows.append(dict(source=str(p),snapshot=str(q),sha256=hashlib.sha256(q.read_bytes()).hexdigest()))
(out/'inputs.json').write_text(json.dumps(rows,indent=2))
def run(cmd):
 p=subprocess.run(list(map(str,cmd)),capture_output=True,text=True);results.append(dict(command=list(map(str,cmd)),exit=p.returncode,stdout=p.stdout,stderr=p.stderr));(out/'commands.json').write_text(json.dumps(results,indent=2));print(p.stdout,p.stderr);p.check_returncode()
def compile(name,src,refs=(),kind='library'):
 rsp=out/(name+'.rsp');rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:'+kind,'/out:"'+str(out/(name+('.dll' if kind=='library' else '.exe')))+'"']+['/r:"'+str(out/(x+'.dll'))+'"' for x in refs]+['"'+str(x)+'"' for x in src]));run([CSC,'@'+str(rsp)])
sources=[Path(x['snapshot']) for x in rows] if False else [out/'source'/Path(x['source']).relative_to(R/'unity/Assets/Ashfall') for x in rows]
from pathlib import Path
compile("Core",[out/"source/Core/Contracts.cs"])
compile("Recovery",list((out/"source/RecoveryContracts").glob("*.cs")),["Core"])
compile("Health",list((out/"source/Modules/HealthStates").glob("*.cs")),["Core","Recovery"])
def area(path):return list((out/'source'/path).glob('*.cs'))
compile('HealthOwner',area('Modules/HealthStates'),['Core','Recovery'])
compile('Health',area('Modules/HealthStates')+area('Modules/Combat')+area('Modules/Persistence')+area('Integration/CombatPublication')+[out/'source/Integration/CombatHealthBridge.cs'],['Core','Recovery'])
for name,path,refs in [('Weapon','Modules/WeaponHandling',['Core','Recovery']),('Inventory','Modules/InventoryItems',['Core','Recovery']),('Session','Modules/Sessions',['Core','Recovery','Health'])]:
 compile(name,area(path),refs)
compile('InventoryRecovery',area('Integration/InventoryRecovery'),['Core','Recovery','Inventory','Weapon'])
refs=['Core','Recovery','Health','Weapon','Inventory','Session','InventoryRecovery']
compile('Host',area('Integration/HostEpochAuthority'),refs)
q=out/'HealthEpochTests.cs';shutil.copyfile(R/'unity/Tools/Integration/HostEpochAuthority'/('BaselineBypass.cs' if sys.argv[1]=='baseline' else 'HealthEpochTests.cs'),q)
compile('Tests',[q],refs+['Host'],'exe')
run([out/'Tests.exe',out])
if sys.argv[1]!='baseline':
 q=out/'HealthTests.cs';shutil.copyfile(R/'unity/Tools/Modules/HealthStates/HealthTests.cs',q)
 compile('HealthRegression',[q],['Core','Recovery','HealthOwner'],'exe');run([out/'HealthRegression.exe'])
 q=out/'RegistryTests.cs';t=(S/'before/unity/Tools/Integration/HostEpochAuthority/Tests.cs').read_text()
 t=t.replace('gain.CommitValidated();Check(health.Read("p").Revision>before,"exposed legacy Health commit after close: global blocker");','Check(Throws(delegate{gain.CommitValidated();}),"Health retained commit revoked");Check(health.Read("p").Revision==before,"revocation preserves Health");')
 q.write_text('using Ashfall.RecoveryContracts;'+t);compile('RegistryRegression',[q],refs+['Host'],'exe');fixture=out/'registry-fixture';fixture.mkdir();run([out/'RegistryRegression.exe',fixture])


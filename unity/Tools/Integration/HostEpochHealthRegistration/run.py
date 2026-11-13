from pathlib import Path
import json,hashlib,subprocess,shutil
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/HostEpochHealthRegistrationR2';D=R/'docs/agent-runs/20261104-three-chats/unity/HostEpochHealthRegistrationR2';O=S/'candidate01';O.mkdir()
rows=json.loads((D/'before.json').read_text()); src=O/'source'
for x in rows:
 p=Path(x['source']);q=Path(x['snapshot'])
 if 'Assets' in p.parts:
  dest=src/p.relative_to(R/'unity/Assets/Ashfall');dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(q,dest)
for p in (R/'unity/Assets/Ashfall/Integration/HostEpochAuthority').glob('*.cs'):shutil.copyfile(p,src/'Integration/HostEpochAuthority'/p.name)
# Freeze tests and tools before first compilation.
for p in (R/'unity/Tools/Integration/HostEpochHealthRegistration').glob('*.*'):shutil.copyfile(p,O/p.name)
manifest=[dict(path=str(p),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in O.rglob('*') if p.is_file()];(O/'inputs.json').write_text(json.dumps(manifest,indent=2))
CSC=r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe';commands=[]
def run(cmd,required=True):
 p=subprocess.run(list(map(str,cmd)),capture_output=True,text=True);commands.append(dict(command=list(map(str,cmd)),exit=p.returncode,stdout=p.stdout,stderr=p.stderr));(O/'commands.json').write_text(json.dumps(commands,indent=2));print(p.stdout,p.stderr)
 if required:p.check_returncode()
 return p.returncode
def area(a):return list((src/a).glob('*.cs'))
def comp(n,files,refs=(),kind='library',required=True):
 rsp=O/(n+'.rsp');rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:'+kind,'/out:"'+str(O/(n+('.dll' if kind=='library' else '.exe')))+'"']+['/r:"'+str(O/(x+'.dll'))+'"' for x in refs]+['"'+str(x)+'"' for x in files]));return run([CSC,'@'+str(rsp)],required)
comp('Core',area('Core'));comp('Recovery',area('RecoveryContracts'),['Core']);comp('Health',area('Modules/HealthStates'),['Core','Recovery']);comp('Combat',area('Modules/Combat'),['Core','Recovery','Health']);comp('Persistence',area('Modules/Persistence'),['Core']);comp('Publication',area('Integration/CombatPublication')+[src/'Integration/CombatHealthBridge.cs'],['Core','Recovery','Health','Combat','Persistence'])
for n,a,refs in [('Weapon','Modules/WeaponHandling',['Core','Recovery']),('Inventory','Modules/InventoryItems',['Core','Recovery']),('Session','Modules/Sessions',['Core','Recovery','Health']),('InventoryRecovery','Integration/InventoryRecovery',['Core','Recovery','Inventory','Weapon'])]:comp(n,area(a),refs)
refs=['Core','Recovery','Health','Combat','Persistence','Publication','Weapon','Inventory','Session','InventoryRecovery'];comp('Host',area('Integration/HostEpochAuthority'),refs)
# Public unchanged family and Health owner suites.
for n,path in [('Families','unity/Tools/Integration/HostEpochAuthority/HealthEpochTests.cs'),('HealthRegression','unity/Tools/Modules/HealthStates/HealthTests.cs')]:
 q=O/(n+'.cs');shutil.copyfile(S/'before'/path,q);comp(n,[q],refs+['Host'],'exe');f=O/(n+'-data');f.mkdir();run([O/(n+'.exe'),f])
comp('Registration',[O/'RegistrationTests.cs'],refs+['Host'],'exe');f=O/'registration-data';f.mkdir();run([O/'Registration.exe',f])

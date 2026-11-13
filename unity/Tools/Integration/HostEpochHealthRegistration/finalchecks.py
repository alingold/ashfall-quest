from pathlib import Path
import json,hashlib,shutil,subprocess
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/HostEpochHealthRegistrationR2';D=R/'docs/agent-runs/20261104-three-chats/unity/HostEpochHealthRegistrationR2';base=S/'candidate01';O=S/'finalchecks02';O.mkdir();src=base/'source';CSC=r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe';commands=[]
exec((R/'unity/Tools/Integration/HostEpochHealthRegistration/run.py').read_text().split('def run(cmd,required=True):')[1].split("comp('Core'")[0].join(['def run(cmd,required=True):','']))
rows=json.loads((D/'before.json').read_text())
def frozen(name,needle):
 x=[x for x in rows if Path(x['source']).name==name and needle in x['source'] and ('frozen' not in Path(x['source']).parts)];assert len(x)==1,(name,len(x));return Path(x[0]['snapshot']).read_text()
old= S/'verification01/before';refs=['Core','Recovery','Health','Weapon','Inventory','Session','InventoryRecovery','Host']
for n in refs:shutil.copyfile(old/(n+'.dll'),O/(n+'.dll'))
for name,text in [('Original54',frozen('Tests.cs',r'\unity\Tools\Integration\HostEpochAuthority')),('Original139',frozen('Adversarial.cs',r'\reviews\HostEpochAuthorityReviewR1\Adversarial.cs'))]:
 q=O/(name+'.cs');q.write_text('using Ashfall.RecoveryContracts;'+text);comp(name,[q],refs,'exe');f=O/(name+'-data');f.mkdir();run([O/(name+'.exe'),f])
# Correctly labelled complete respawn final guard removal (single-branch mutant survived).
refs=['Core','Recovery','Health','Combat','Persistence','Publication','Weapon','Inventory','Session','InventoryRecovery','Host']
for n in refs:shutil.copyfile(base/(n+'.dll'),O/(n+'.dll'))
p=src/'Modules/HealthStates/PreparedLifecycle.cs';q=O/p.name;q.write_text(p.read_text().replace('if(!publicationLease.CanCommit||aborted)throw new InvalidOperationException("Aborted respawn");',''))
comp('Health',[q if p.name==q.name else p for p in area('Modules/HealthStates')],['Core','Recovery']);shutil.copyfile(base/'Families.exe',O/'Families.exe');f=O/'mutant-data';f.mkdir();assert run([O/'Families.exe',f],False)!=0
# Native fixture: compile only, no invocation.
shutil.copyfile(base/'Health.dll',O/'Health.dll');q=O/'HealthEpochNativeFixture.cs';shutil.copyfile(S/'before/unity/Tools/Modules/HealthStates/HealthEpochNativeFixture.cs',q);comp('NativeFixture',[q],['Core','Recovery','Health'])
print('Completed original54/original139 and complete-respawn mutant; native fixture compiled only')



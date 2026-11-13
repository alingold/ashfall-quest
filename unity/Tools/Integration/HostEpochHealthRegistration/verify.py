from pathlib import Path
import json,hashlib,subprocess,shutil
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/HostEpochHealthRegistrationR2';D=R/'docs/agent-runs/20261104-three-chats/unity/HostEpochHealthRegistrationR2';base=S/'candidate01';O=S/'verification01';O.mkdir();src=base/'source';CSC=r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe';commands=[]
exec((R/'unity/Tools/Integration/HostEpochHealthRegistration/run.py').read_text().split('def run(cmd,required=True):')[1].split("comp('Core'")[0].join(['def run(cmd,required=True):','']))
refs=['Core','Recovery','Health','Combat','Persistence','Publication','Weapon','Inventory','Session','InventoryRecovery','Host']
for n in refs:shutil.copyfile(base/(n+'.dll'),O/(n+'.dll'))
# Archive is frozen once here; it uses the already pinned candidate owner sources.
p=R/'unity/Tools/Integration/HealthArchiveRecords/Tests.cs';q=O/'Archive.cs';shutil.copyfile(p,q);(O/'archive-input.json').write_text(json.dumps(dict(source=str(p),snapshot=str(q),sha256=hashlib.sha256(q.read_bytes()).hexdigest()),indent=2));comp('Archive',[q],refs,'exe');f=O/'archive-data';f.mkdir();run([O/'Archive.exe',f])
# Historical graph compiled from captured before-source, not historical DLL reuse.
historical=R/'campaign-staging/20261104-three-chats/unity/reviews/HealthEpochReviewR2/before-source';hist=O/'historical-source';shutil.copytree(historical,hist)
(O/'historical-inputs.json').write_text(json.dumps([dict(path=str(p),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in hist.rglob('*.cs')],indent=2))
oldO=O;O=oldO/'before';O.mkdir();src=hist
comp('Core',area('Core'));comp('Recovery',area('RecoveryContracts'),['Core']);comp('Health',area('Modules/HealthStates')+area('Modules/Combat')+area('Modules/Persistence')+area('Integration/CombatPublication')+[src/'Integration/CombatHealthBridge.cs'],['Core','Recovery'])
for n,a,r in [('Weapon','Modules/WeaponHandling',['Core','Recovery']),('Inventory','Modules/InventoryItems',['Core','Recovery']),('Session','Modules/Sessions',['Core','Recovery','Health']),('InventoryRecovery','Integration/InventoryRecovery',['Core','Recovery','Inventory','Weapon'])]:comp(n,area(a),r)
r=['Core','Recovery','Health','Weapon','Inventory','Session','InventoryRecovery'];comp('Host',area('Integration/HostEpochAuthority'),r)
hits=[x for x in json.loads((D/'before.json').read_text()) if Path(x['source']).name=='BeforeFamilies.cs'];q=O/'BeforeSix.cs';shutil.copyfile(hits[0]['snapshot'],q);comp('BeforeSix',[q],r+['Host'],'exe');f=O/'six-data';f.mkdir();run([O/'BeforeSix.exe',f])
# Compile meaningful mutations from candidate source; separate owner and consumer DLLs.
O=oldO;src=base/'source';allresults=[]
mutations=[('key','Integration/HostEpochAuthority/OwnerRegistry.cs','key==null||!Object.ReferenceEquals(key,healthKey)','key==null','Registration'),('participant','Integration/HostEpochAuthority/OwnerRegistry.cs','healthAfterClose=healthClosure.Close()','healthAfterClose=health.EpochAccounting','Families'),('terminal','Integration/HostEpochAuthority/OwnerRegistry.cs','reopened||everOpened||disposed||terminalClosed||!closed','reopened||everOpened||disposed||!closed','Registration'),('gain','Modules/HealthStates/PreparedGains.cs','!lease.CanCommit||','', 'Families'),('advance','Modules/HealthStates/PreparedAdvance.cs','!lease.CanCommit||','', 'Families'),('respawn','Modules/HealthStates/PreparedLifecycle.cs','!publicationLease.CanCommit||aborted','aborted','Families')]
for name,path,a,b,test in mutations:
 O=oldO/('mutant-'+name);O.mkdir()
 for p in base.glob('*.dll'):shutil.copyfile(p,O/p.name)
 p=src/path;t=p.read_text();assert a in t;q=O/p.name;q.write_text(t.replace(a,b))
 if path.startswith('Integration'):
  files=[q if x.name==q.name else x for x in area('Integration/HostEpochAuthority')];comp('Host',files,[n for n in refs if n!='Host'])
 else:comp('Health',[q if x.name==q.name else x for x in area('Modules/HealthStates')],['Core','Recovery'])
 exe=(S/'supplement01/Registration.exe') if test=='Registration' else base/'Families.exe';shutil.copyfile(exe,O/(test+'.exe'));f=O/'data';f.mkdir();code=run([O/(test+'.exe'),f],False);allresults.append(dict(name=name,compiled=True,killed=code!=0,exit=code));assert code!=0,name
O=oldO;(O/'mutations.json').write_text(json.dumps(allresults,indent=2));print(allresults)

import pathlib,json,hashlib,shutil,subprocess,datetime
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/MatchPingRecoveryR12';D=R/'docs/agent-runs/20261104-three-chats/unity/MatchPingRecoveryR12';O=S/('candidate-'+datetime.datetime.now().strftime('%H%M%S%f'));O.mkdir();logs=[];pins=[];csc=r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
def freeze(p,q):
 q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q);pins.append(dict(source=str(p.relative_to(R)),snapshot=str(q.relative_to(R)),sha256=hashlib.sha256(q.read_bytes()).hexdigest()));return q
B=S/'before/unity/Assets/Ashfall';src={}
for folder in ['Core','RecoveryContracts','Modules/Modes','Modules/ModesRecovery','Modules/HealthStates','Modules/Combat','Modules/Persistence','Modules/Sessions','Integration/MatchRounds','Integration/CombatPublication','Integration/MatchDirector','Integration/MatchRecovery']:
 root=R/'unity/Assets/Ashfall'/folder if folder in ['Integration/MatchDirector','Integration/MatchRecovery'] else B/folder
 src[folder]=[freeze(p,O/'source'/folder/p.name) for p in root.glob('*.cs')]
src['Integration/CombatPublication'].append(freeze(B/'Integration/CombatHealthBridge.cs',O/'source/Integration/CombatHealthBridge.cs'))
test=freeze(R/'unity/Tools/Integration/MatchPingRecovery/Tests.cs',O/'source/Tests.cs');freeze(pathlib.Path(__file__),O/'validate.py');(D/(O.name+'-pins.json')).write_text(json.dumps(pins,indent=2))
def call(label,args):
 x=subprocess.run(list(map(str,args)),capture_output=True,text=True);logs.append(dict(label=label,argv=list(map(str,args)),exit=x.returncode,stdout=x.stdout,stderr=x.stderr));(D/(O.name+'-commands.json')).write_text(json.dumps(logs,indent=2));print(label,x.returncode,x.stdout[-2300:]);return x.returncode

def build(name,files,refs=(),exe=False,directory=O):
 out=directory/(name+('.exe' if exe else '.dll'));rsp=directory/(name+'.rsp');rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:'+('exe' if exe else 'library'),'/out:"'+str(out)+'"']+['/reference:"'+str(p)+'"' for p in refs]+['"'+str(p)+'"' for p in files]));assert call(name+'-compile',[csc,'@'+str(rsp)])==0;return out
DLL={}
for name,folder,refs in [('Core','Core',[]),('RecoveryContracts','RecoveryContracts',['Core']),('Modes','Modules/Modes',['Core']),('ModesRecovery','Modules/ModesRecovery',['Core','RecoveryContracts','Modes']),('HealthStates','Modules/HealthStates',['Core','RecoveryContracts']),('Combat','Modules/Combat',['Core','RecoveryContracts','HealthStates']),('Persistence','Modules/Persistence',['Core']),('Sessions','Modules/Sessions',['Core','RecoveryContracts','HealthStates']),('MatchRounds','Integration/MatchRounds',['Core','RecoveryContracts','Modes','HealthStates']),('CombatPublication','Integration/CombatPublication',['Core','RecoveryContracts','HealthStates','Combat','Persistence']),('MatchDirector','Integration/MatchDirector',['Core','RecoveryContracts','Modes','ModesRecovery','HealthStates','Sessions','MatchRounds','Persistence','Combat','CombatPublication']),('MatchRecovery','Integration/MatchRecovery',['Core','RecoveryContracts','Modes','ModesRecovery','HealthStates','Sessions','MatchRounds'])]:
 DLL[name]=build(name,src[folder],[DLL[n] for n in refs])
exe=build('Tests',[test],list(DLL.values()),True);assert call('pair-run',[exe])==0
# Current separate owner-DLL external historical adversarial fixture.
external=freeze(S/'before/campaign-staging/20261104-three-chats/unity/reviews/DirectorArchiveReviewR11/IndependentTests.cs',O/'source/External120.cs')
ext=build('External120',[external],list(DLL.values()),True);assert call('current-external120',[ext])==0
for label,folder,file,old,new,name in [('lost-producer','Integration/MatchDirector','ProducerArchive.cs','x.pending=pending;','x.pending=null;','MatchDirector'),('latest-health','Integration/MatchDirector','DirectorArchive.cs','State=snapshots[i]','State=quarantinedHealth.Read(key)','MatchDirector'),('time-proof','Integration/MatchDirector','DirectorPingArchive.cs','actual==expected','actual>=0','MatchDirector'),('cooldown','Modules/ModesRecovery','RecoverablePingService.cs','now-previous<cooldown','now-previous<0','ModesRecovery')]:
 m=O/label;m.mkdir()
 for p in O.glob('*.dll'):shutil.copyfile(p,m/p.name)
 shutil.copyfile(exe,m/exe.name)
 original=next(p for p in src[folder] if p.name==file);text=original.read_text();assert old in text;mutant=m/file;mutant.write_text(text.replace(old,new))
 files=[mutant if p==original else p for p in src[folder]];build(name,files,[v for k,v in DLL.items() if k!=name],directory=m);assert call(label+'-killed',[m/exe.name])!=0
(D/'latest.json').write_text(json.dumps(dict(run=str(O.relative_to(R)),status='partial-only G2/M1 OPEN'),indent=2))



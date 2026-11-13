from pathlib import Path
import json,subprocess,hashlib,shutil,collections
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/RuntimeLoopBindingsR4';E=R/'docs/agent-runs/20261104-three-chats/unity/RuntimeLoopBindingsR4';O=S/'closure10';L=E/'closure10';O.mkdir(exist_ok=False);L.mkdir(exist_ok=False)
rows=json.loads((E/'final06/sources.json').read_text());files=[Path(x['path']) for x in rows];results={};commands=[]
def comp(n,fs,refs=(),main=None,flags=()):
 out=O/(n+('.exe' if main else '.dll'));args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:'+('exe' if main else 'library'),'/reference:System.Runtime.Serialization.dll','/reference:System.Web.Extensions.dll','/out:'+str(out)]+['/reference:'+str(p) for p in refs]+(['/main:'+main] if main else [])+list(flags)+list(map(str,fs));rsp=L/(n+'.rsp');rsp.write_text('\n'.join(subprocess.list2cmdline([a]) for a in args));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];commands.append(cmd);p=subprocess.run(cmd,capture_output=True);(L/(n+'.log')).write_bytes(p.stdout+p.stderr);results[n]=p.returncode;print(n,p.returncode,p.stdout.decode()[:400]);return out,p.returncode
allout,rc=comp('CurrentClosure',files)
u=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data/Managed/UnityEngine');native=[p for p in files if '/RuntimeLoop/Engine/' in p.as_posix()]+[R/'unity/Tools/Integration/RuntimeLoop/NativeSchedulerFixture.cs']
if not rc:comp('Native',native,[allout,u/'UnityEngine.CoreModule.dll',u/'UnityEngine.PhysicsModule.dll',u.parent.parent/'UnityReferenceAssemblies/unity-4.8-api/Facades/netstandard.dll'],flags=['/define:UNITY_6000_0_OR_NEWER'])
# Exact bounded runtime DLL profile: retain current APIs except codec additions
# that introduce owner-private cross-folder cycles. Use immutable root-proven phase13
# weapon/inventory modules, with phase15 PreparedWeapons accessor.
base=R/'campaign-staging/20261104-three-chats/unity/phase15/reserve-boundary/check/inputs';reserve=json.loads((R/'docs/agent-runs/20261104-three-chats/unity/phase15/reserve-boundary-validation.json').read_text())['inputs'];bindings=[]
for x in reserve:
 p=R/x['snapshot'];assert hashlib.sha256(p.read_bytes()).hexdigest().upper()==x['sha256'].upper()
 if '/Modules/WeaponHandling/' in x['source'].replace('\\','/') or any(z in p.name for z in ['237-','238-','239-','240-','241-','243-','244-','245-','246-','167-','168-','169-','170-','171-','172-','173-']):
  d=O/'reserve'/p.name;d.parent.mkdir(exist_ok=True);shutil.copyfile(p,d);bindings.append(d)
profile=[p for p in files if not any(k in p.as_posix() for k in ['/InventoryItems/','/WeaponHandling/','/InventoryRecovery/'])]+bindings
# CombatPublication includes a CombatHealthBridge partial: same actual type must share one DLL.
groups=collections.defaultdict(list)
for p in profile:
 if p in bindings:key='WeaponHandling' if any(z in p.name for z in ['237-','238-','239-','240-','241-','243-','244-','245-','246-','PreparedWeapons']) else 'InventoryItems'
 else:
  rel=p.as_posix().split('/Assets/Ashfall/')[1].split('/');key='Core' if rel[0]=='Core' else rel[1] if len(rel)>2 else 'CombatHealthBridge'
  if key in ['Combat','HealthStates','CombatPublication','CombatHealthBridge','MatchDirector','Sessions','MatchRounds']:key='CombatHealth'
  if key=='CombatPublication':key='CombatHealth'
 groups[key].append(p)
resolved={};pending=dict(groups);i=0
while pending:
 progress=False
 for k,fs in list(pending.items()):
  i+=1;out,rc=comp('Dll'+str(i)+'-'+k,fs,list(resolved.values()))
  if not rc:resolved[k]=out;del pending[k];progress=True
 if not progress:break
(L/'dll-profile.json').write_text(json.dumps({'sources':[str(p) for p in profile],'resolved':{k:str(v) for k,v in resolved.items()},'unresolved':list(pending),'scope':'Explicit root reserve-boundary runtime profile; excludes new Inventory/Weapon cold codec additions; NOT whole phase15 DLL compatibility'},indent=2))
if not pending:
 setup=O/'R2Setup.cs';setup.write_text((S/'final06/R2Gates.cs').read_text().split('    public void RunR2()')[0]+'}\n')
 exe,rc=comp('SeparateR4',[S/'final06/Scenario.cs',setup,S/'final06/R4Gates.cs'],list(resolved.values()),main='R4Tests')
 if not rc:
  p=subprocess.run([str(exe)],capture_output=True);results['SeparateR4Run']=p.returncode;(L/'SeparateR4Run.log').write_bytes(p.stdout+p.stderr);print(p.stdout.decode()[-200:])
(L/'commands.json').write_text(json.dumps(commands,indent=2));(L/'results.json').write_text(json.dumps(results,indent=2))

import pathlib,json,subprocess,hashlib,shutil,sys
R=pathlib.Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/ProjectileColdOwnerR5';D=R/'docs/agent-runs/20261104-three-chats/unity/ProjectileColdOwnerR5';name=sys.argv[1];O=S/name;O.mkdir();L=D/name;L.mkdir();B=S/'before';A=pathlib.Path('unity/Assets/Ashfall');rows=json.loads((D/'before.json').read_text());files=[]
for row in rows:
 p=pathlib.Path(row['path'])
 if not p.as_posix().startswith('unity/Assets/'):continue
 src=R/p if any(x in p.parts for x in ['Projectiles','ProjectileWeapons']) else B/p
 q=O/'sources'/p;q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(src,q);files.append(q)
for p in (R/A/'Modules/Projectiles').glob('*.cs'):
 q=O/'sources'/p.relative_to(R)
 if q not in files:shutil.copyfile(p,q);files.append(q)
results=[]
def compile(name,paths,refs=(),main=None):
 dest=O/name;rsp=O/(name+'.rsp');args=[r'/reference:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.dll"',r'/reference:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Core.dll"','/nologo','/noconfig','/langversion:5','/warn:4','/warnaserror+','/target:'+('exe' if main else 'library'),'/out:"'+str(dest)+'"']+(['/main:'+main] if main else [])+['/reference:"'+str(x)+'"' for x in refs]+['"'+str(x)+'"' for x in paths];args.remove('/noconfig');rsp.write_text('\n'.join(args));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/noconfig','@'+str(rsp)];p=subprocess.run(cmd,capture_output=True,text=True);(L/(name+'.compile.log')).write_text(p.stdout+p.stderr);results.append({'name':name,'exit':p.returncode,'command':cmd});print(name,p.returncode,p.stdout[-2500:]);return dest
core=compile('Core.dll',[p for p in files if 'Core' in p.parts]);recovery=compile('Recovery.dll',[p for p in files if 'RecoveryContracts' in p.parts],[core]);weapon=compile('Weapon.dll',[p for p in files if 'WeaponHandling' in p.parts or p.name=='Wire.cs'],[core,recovery]);game=compile('Game.dll',[p for p in files if any(x in p.parts for x in ['Combat','HealthStates','Persistence','CombatPublication']) or p.name=='CombatHealthBridge.cs'],[core,recovery]);proj=compile('Projectiles.dll',[p for p in files if any(x in p.parts for x in ['Projectiles','ProjectileWeapons'])],[core,weapon,game]);refs=[core,recovery,weapon,game,proj]
def test(main,paths,args=()):
 exe=compile(main+'.exe',paths,refs,main)
 if results[-1]['exit']==0:
  p=subprocess.run([str(exe)]+list(args),capture_output=True,text=True);(L/(main+('-'+args[-1] if main=='ColdTests' else '')+'.log')).write_text(p.stdout+p.stderr);results.append({'name':main,'exit':p.returncode});print(main,p.returncode,p.stdout[-1200:],p.stderr[-1200:])
tool=R/'unity/Tools/Integration/ProjectileColdOwner';tests=[]
for p in [B/'unity/Tools/Modules/Projectiles/ProjectileTests.cs',B/'unity/Tools/Modules/Projectiles/RetryTests.cs',tool/'ColdTests.cs']:
 q=O/p.name;shutil.copyfile(p,q);tests.append(q)
if all(x['exit']==0 for x in results):
 catalog=O/'CatalogHash.cs';catalog.write_text('static class CatalogHash {public const string Value="'+hashlib.sha256((R/'quest-r22/source/assets/data/one_star_050.json').read_bytes()).hexdigest()+'";}');tests.append(catalog)
 test('ProjectileTests',[tests[0]]);test('RetryTests',tests[:2]+[catalog]);test('ColdTests',[tests[0],tests[2]],[str(O),'write']);test('ColdTests',[tests[0],tests[2]],[str(O),'read'])
if all(x['exit']==0 for x in results):
 launch=O/'PublicLaunchTests.cs';launch.write_text('\n'.join(line for line in (B/'unity/Tools/Integration/ProjectileWeapons/LaunchTests.cs').read_text().splitlines() if 'rules.WriteCheckpoint' not in line));test('LaunchTests',[tests[0],launch,catalog],[str(O)])
if all(x['exit']==0 for x in results):
 prepared=O/'PreparedCaptureTests.cs';text=launch.read_text().replace('p.Abort();p.CommitValidated();', 'Need(PreparedShotLaunch.ValidateColdCapture("s",3).Reason=="projectile-prepared-shot-outstanding","actual prepare excludes capture");p.Abort();Need(PreparedShotLaunch.ValidateColdCapture("s",3).Reason=="combat-launch-capability-restoration-unavailable","abort releases actual mark but never global approval");p.CommitValidated();').replace('p.CommitValidated();p.CommitValidated();p.Abort();','Need(PreparedShotLaunch.ValidateColdCapture("s",3).Reason=="projectile-prepared-shot-outstanding","validated token still excludes capture");p.CommitValidated();p.CommitValidated();p.Abort();Need(PreparedShotLaunch.ValidateColdCapture("s",3).Reason=="combat-launch-capability-restoration-unavailable","commit releases exact mark");');prepared.write_text(text.replace("static class LaunchTests","static class PreparedCaptureTests"));test('PreparedCaptureTests',[tests[0],prepared,catalog],[str(O)])
(L/'results.json').write_text(json.dumps(results,indent=2));(L/'inputs.json').write_text(json.dumps([{'path':str(p.relative_to(R)),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in files+tests],indent=2));sys.exit(0 if all(x['exit']==0 for x in results) else 1)


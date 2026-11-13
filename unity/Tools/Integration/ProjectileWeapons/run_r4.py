import pathlib,json,hashlib,shutil,subprocess,sys
R=pathlib.Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/SourceLaunchProjectilesR4';D=R/'docs/agent-runs/20261104-three-chats/unity/SourceLaunchProjectilesR4';name=sys.argv[1];o=S/name;o.mkdir();d=D/name;d.mkdir();sources=[]
owned=['Modules/Projectiles/','Integration/ProjectileWeapons/','Modules/Combat/CombatRules.cs','Modules/Combat/SourceRadial.cs','Modules/Combat/LaunchCapabilities.cs']
for row in json.loads((D/'before.json').read_text()):
 rel=pathlib.Path(row['original'])
 if not str(rel).replace('\\','/').startswith('unity/Assets/'):continue
 sub=str(rel).replace('\\','/').split('Ashfall/')[1];p=R/rel if any(sub.startswith(x) for x in owned) else S/'before'/rel
 q=o/'sources'/rel;q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q);sources.append(q)
q=o/'Wire.cs';shutil.copyfile(S/'additional/unity/Assets/Ashfall/Integration/InventoryRecovery/Wire.cs',q);sources.append(q)
for folder in ['unity/Assets/Ashfall/Modules/Combat','unity/Assets/Ashfall/Integration/ProjectileWeapons']:
 for p in (R/folder).glob('*.cs'):
  q=o/'sources'/p.relative_to(R)
  if q not in sources:q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q);sources.append(q)
for p in [R/'unity/Tools/Modules/Projectiles/ProjectileTests.cs',R/'unity/Tools/Modules/Projectiles/RetryTests.cs',R/'unity/Tools/Integration/ProjectileWeapons/R4Tests.cs',R/'unity/Tools/Integration/ProjectileWeapons/LaunchTests.cs']:
 q=o/p.name;shutil.copyfile(p,q);sources.append(q)
p=o/'CatalogHash.cs';p.write_text('static class CatalogHash {public const string Value="'+hashlib.sha256((R/'quest-r22/source/assets/data/one_star_050.json').read_bytes()).hexdigest()+'";}');sources.append(p)
results=[]
for main in ['ProjectileTests','RetryTests','R4Tests','LaunchTests']:
 exe=o/(main+'.exe');rsp=o/(main+'.rsp');rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+','/main:'+main,'/out:"'+str(exe)+'"']+['"'+str(p)+'"' for p in sources]));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];c=subprocess.run(cmd,capture_output=True,text=True);(d/(main+'-compile.log')).write_text(c.stdout+c.stderr);row={'main':main,'compile':c.returncode,'command':cmd}
 if not c.returncode:
  t=subprocess.run([str(exe),str(o)],capture_output=True,text=True);(d/(main+'.log')).write_text(t.stdout+t.stderr);row['exit']=t.returncode;print(main,t.returncode,t.stdout[-200:],t.stderr)
 else:print(c.stdout+c.stderr)
 results.append(row)
(d/'results.json').write_text(json.dumps(results,indent=2));(d/'inputs.json').write_text(json.dumps([{'path':str(p.relative_to(R)),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in sources],indent=2));sys.exit(0 if all(x.get('exit')==0 for x in results) else 1)

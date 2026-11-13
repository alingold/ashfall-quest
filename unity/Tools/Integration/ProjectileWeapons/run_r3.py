import pathlib,json,hashlib,shutil,subprocess,sys
R=pathlib.Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/ProjectilesR3';D=R/'docs/agent-runs/20261104-three-chats/unity/ProjectilesR3';name=sys.argv[1];out=S/name;out.mkdir();logs=D/name;logs.mkdir()
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
base=S/('current02' if 'current02' in name else 'before');deps=[base/'unity/Assets/Ashfall/Core/Contracts.cs',base/'unity/Assets/Ashfall/Integration/CombatHealthBridge.cs']
for m in ['Combat','HealthStates','WeaponHandling','Persistence']:deps+=sorted((base/('unity/Assets/Ashfall/Modules/'+m)).glob('*.cs'))
deps+=list((base/'unity/Assets/Ashfall/Integration/CombatPublication').glob('*.cs'))
paths=[(p,p.relative_to(base)) for p in deps]
if 'phase12' in name:
 idx=json.loads((R/'docs/agent-runs/20261104-three-chats/unity/phase12/source-index.json').read_text(encoding='utf-8-sig')); lookup={str(pathlib.Path(x['original'])):R/x['snapshot'] for x in idx};paths=[(lookup.get(str(rel),p),rel) for p,rel in paths]
paths += [(p,p.relative_to(R)) for folder in ['unity/Assets/Ashfall/Modules/Projectiles','unity/Assets/Ashfall/Integration/ProjectileWeapons'] for p in (R/folder).glob('*.cs')]
paths += [(R/p,pathlib.Path(p)) for p in ['unity/Tools/Modules/Projectiles/ProjectileTests.cs','unity/Tools/Modules/Projectiles/RetryTests.cs','unity/Tools/Integration/ProjectileWeapons/R3Tests.cs']]
if name=='baseline-01':
 paths=[(base/rel if str(rel).startswith(str(pathlib.Path('unity/Assets/Ashfall/Modules/Projectiles'))) else p,rel) for p,rel in paths if 'Integration/ProjectileWeapons' not in str(rel).replace('\\','/') and 'Integration\\ProjectileWeapons' not in str(rel)]
 # integration test source is excluded explicitly for the historical baseline
 paths=[(p,rel) for p,rel in paths if rel.name!='R3Tests.cs' and rel.name!='PreparedProjectileWeapons.cs']
sources=[]
for p,rel in paths:
 dest=out/'sources'/rel;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,dest);sources.append(dest)
cat=R/'quest-r22/source/assets/data/one_star_050.json';p=out/'CatalogHash.cs';p.write_text('static class CatalogHash {public const string Value="'+sha(cat)+'";}');sources.append(p)
results=[]
for main in (['ProjectileTests','RetryTests'] if name=='baseline-01' else ['ProjectileTests','RetryTests','R3Tests']):
 exe=out/(main+'.exe');rsp=out/(main+'.rsp');rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/main:'+main,'/out:"'+str(exe)+'"']+['"'+str(p)+'"' for p in sources]));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];c=subprocess.run(cmd,capture_output=True,text=True);(logs/(main+'-compile.log')).write_text(c.stdout+c.stderr);row={'main':main,'compile':c.returncode,'command':subprocess.list2cmdline(cmd)}
 if c.returncode==0:
  t=subprocess.run([str(exe),str(out)],capture_output=True,text=True);(logs/(main+'-tests.log')).write_text(t.stdout+t.stderr);row.update(exit=t.returncode,exe_sha256=sha(exe));print(main,t.returncode,t.stdout[-400:],t.stderr)
 else:print(c.stdout+c.stderr)
 results.append(row)
(logs/'results.json').write_text(json.dumps(results,indent=2));(logs/'consumed-sources.json').write_text(json.dumps([{'path':str(p.relative_to(R)),'sha256':sha(p)} for p in sources],indent=2));sys.exit(0 if all(x.get('exit',-1)==0 for x in results) else 1)


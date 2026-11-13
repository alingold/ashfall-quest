import pathlib,json,hashlib,shutil,subprocess,sys
R=pathlib.Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/ProjectilesR2';D=R/'docs/agent-runs/20261104-three-chats/unity/ProjectilesR2';name=sys.argv[1] if len(sys.argv)>1 else 'final01';out=S/name;out.mkdir();logs=D/name;logs.mkdir()
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def pin(p,base):
 rel=p.relative_to(base);dest=out/'sources'/rel;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,dest);return dest
before=S/'before';deps=[before/'unity/Assets/Ashfall/Core/Contracts.cs',before/'unity/Assets/Ashfall/Integration/CombatHealthBridge.cs']
for m in ['Combat','HealthStates']:deps+=sorted((before/('unity/Assets/Ashfall/Modules/'+m)).glob('*.cs'))
sources=[pin(p,before) for p in deps]
sources += [pin(p,R) for p in sorted((R/'unity/Assets/Ashfall/Modules/Projectiles').glob('*.cs'))]
sources += [pin(R/('unity/Tools/Modules/Projectiles/'+p),R) for p in ['ProjectileTests.cs','RetryTests.cs']]
for p in ['Adversarial-v3.cs','CurrentGains.cs']:
 dest=out/p;shutil.copyfile(S/p,dest);sources.append(dest)
cat=R/'quest-r22/source/assets/data/one_star_050.json';p=out/'CatalogHash.cs';p.write_text('static class CatalogHash { public const string Value="'+sha(cat)+'";}');sources.append(p)
results=[]
for main in ['ProjectileTests','Adversarial','CurrentGains','RetryTests']:
 exe=out/(main+'.exe');rsp=out/(main+'.rsp');rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/main:'+main,'/out:"'+str(exe)+'"']+['"'+str(p)+'"' for p in sources]))
 cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];c=subprocess.run(cmd,capture_output=True,text=True);(logs/(main+'-compile.log')).write_text(c.stdout+c.stderr);row={'main':main,'compile':c.returncode,'command':subprocess.list2cmdline(cmd)}
 if c.returncode==0:
  t=subprocess.run([str(exe)],capture_output=True,text=True);(logs/(main+'-tests.log')).write_text(t.stdout+t.stderr);row.update(exit=t.returncode,exe_sha256=sha(exe));print(main,t.returncode,t.stdout.splitlines()[-1:] or t.stderr)
 else:print(c.stdout+c.stderr)
 results.append(row)
(logs/'results.json').write_text(json.dumps(results,indent=2));(logs/'consumed-sources.json').write_text(json.dumps([{'path':str(p),'sha256':sha(p)} for p in sources],indent=2))
drift=[{'path':str(p.relative_to(before)),'pinned':sha(p),'current':sha(R/p.relative_to(before)),'drift':sha(p)!=sha(R/p.relative_to(before))} for p in deps];(logs/'dependency-drift.json').write_text(json.dumps(drift,indent=2))
sys.exit(0 if all(x.get('exit',-1)==0 for x in results) else 1)

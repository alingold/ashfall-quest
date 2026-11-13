import pathlib,shutil,subprocess,json,hashlib
R=pathlib.Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/ProjectileColdOwnerR5';D=R/'docs/agent-runs/20261104-three-chats/unity/ProjectileColdOwnerR5';B=S/'run09';O=S/'mutations09';O.mkdir();L=D/'mutations09';L.mkdir();results=[]
changes=[('resume','Modules/Projectiles/ProjectileSimulation.cs','!coldQuarantine&&c.IsAuthority','c.IsAuthority','read'),('array-alias','Modules/Projectiles/ProjectileSimulation.cs','(Target[])b.Targets.Clone()','b.Targets','write'),('digest','Modules/Projectiles/ColdState.cs','ProjectileWire.Need(ProjectileWire.Hash(image)==expectedDigest);','/* mutant skips trusted digest comparison */','read'),('prepared-mark','Integration/ProjectileWeapons/PreparedShotLaunch.cs','pending.Add(prepared);','/* mutant loses pending registration */','prepared')]
for name,rel,old,new,mode in changes:
 out=O/name;out.mkdir()
 for p in B.glob('*.dll'):shutil.copyfile(p,out/p.name)
 for p in ['ColdTests.exe','PreparedCaptureTests.exe','cold.bin','cold.bin.sha']:shutil.copyfile(B/p,out/p)
 files=[]
 for folder in ['Modules/Projectiles','Integration/ProjectileWeapons']:
  for p in (B/'sources/unity/Assets/Ashfall'/folder).glob('*.cs'):
   q=out/p.name;content=p.read_text()
   if p.as_posix().endswith(rel):
    assert old in content;content=content.replace(old,new)
   q.write_text(content);files.append(q)
 rsp=out/'mutant.rsp';rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:library','/out:"'+str(out/'Projectiles.dll')+'"']+['/reference:"'+str(out/x)+'"' for x in ['Core.dll','Weapon.dll','Game.dll']]+['"'+str(p)+'"' for p in files]));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];c=subprocess.run(cmd,capture_output=True,text=True);(L/(name+'-compile.log')).write_text(c.stdout+c.stderr)
 t=None
 if c.returncode==0:
  exe=out/('PreparedCaptureTests.exe' if mode=='prepared' else 'ColdTests.exe');t=subprocess.run([str(exe),str(out)]+([] if mode=='prepared' else [mode]),capture_output=True,text=True);(L/(name+'-test.log')).write_text(t.stdout+t.stderr)
 results.append({'mutant':name,'compile':c.returncode,'exit':None if t is None else t.returncode,'killed':c.returncode==0 and t.returncode!=0,'command':cmd,'source':[{'path':str(p.relative_to(R)),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in files]})
 print(name,results[-1]['killed'])
(L/'results.json').write_text(json.dumps(results,indent=2));assert all(x['killed'] for x in results)


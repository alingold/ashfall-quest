from pathlib import Path
import shutil,subprocess,json
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/CombatProjectileArchiveR5';F=S/'final03';O=S/'mutations02';O.mkdir(exist_ok=False)
cases=[('quarantine','LaunchCapabilities.cs','if(archiveQuarantine||!committed||aborted||physicalSubhit','if(!committed||aborted||physicalSubhit','write'),('registry','LaunchCapabilities.cs','TrackProjectileArchive(token);','/* registration omitted */','write'),('canonical','ProjectileArchiveRecords.cs','RecoveryWire.Require(RecoveryWire.Hash(rules.CapturePendingProjectileArchive(build,catalog,generation,hits))==expectedDigest);','/* canonical validation omitted */','read'),('field','LaunchCapabilities.cs','readonly CombatRules owner;','public int Unaccounted; readonly CombatRules owner;','write')]
logs=[]
for name,file,old,new,mode in cases:
 d=O/name;d.mkdir()
 for p in F.glob('*.dll'):shutil.copyfile(p,d/p.name)
 shutil.copyfile(F/'ArchiveTests.exe',d/'ArchiveTests.exe');shutil.copyfile(F/'cold.bin',d/'cold.bin')
 sources=[]
 for p in (F/'inputs/unity/Assets/Ashfall/Modules/Combat').glob('*.cs'):
  q=d/p.name;text=p.read_text()
  if p.name==file:assert old in text;text=text.replace(old,new)
  q.write_text(text);sources.append(q)
 rsp=d/'Combat.rsp';base=(F/'Combat.rsp').read_text();lines=[x for x in base.splitlines() if not x.startswith('"') and not x.startswith('/out:') and not x.startswith('/reference:')]
 refs=[d/(n+'.dll') for n in ['Core','RecoveryContracts','HealthStates']]+[Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319')/(n+'.dll') for n in ['System','System.Core']]
 rsp.write_text('\n'.join(lines+['/out:"'+str(d/'Combat.dll')+'"']+['/reference:"'+str(p)+'"' for p in refs]+['"'+str(p)+'"' for p in sources]))
 argv=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/noconfig','@'+str(rsp)];c=subprocess.run(argv,capture_output=True,text=True);row=dict(name=name,compileArgv=argv,compileExit=c.returncode,compileLog=c.stdout+c.stderr)
 if c.returncode==0:
  argv=[str(d/'ArchiveTests.exe'),mode,str(d/'cold.bin')];z=subprocess.run(argv,capture_output=True,text=True,cwd=d);row.update(testArgv=argv,testExit=z.returncode,testLog=z.stdout+z.stderr,killed=z.returncode!=0)
 logs.append(row);print(name,row)
(O/'results.json').write_text(json.dumps(logs,indent=2));assert all(x.get('killed') for x in logs)


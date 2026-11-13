from pathlib import Path
import json,hashlib,subprocess
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/RuntimeLoopR3/final05';E=R/'docs/agent-runs/20261104-three-chats/unity/RuntimeLoopR3/final05'
def h(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
rows=json.loads((E/'candidate.json').read_text());files=[S/'candidate'/x['path'] for x in rows];host=next(p for p in files if p.name=='HostLoop.cs');original=host.read_text()
mutations={
 'pressure':original.replace('if(!receipt.Result.Accepted&&receipt.Result.Reason!="publication-pressure")','if(!receipt.Result.Accepted)'),
 'session-readiness':original.replace('!m.Loaded||m.WaitingRound||!m.HasPosition||!m.Position.IsFinite||m.SpawnRevision<1||m.SpawnRevision!=a.SpawnRevision||!h.Connected||','!h.Connected||').replace('(m.Life!=Ashfall.Modules.Sessions.SessionLife.Alive&&m.Life!=Ashfall.Modules.Sessions.SessionLife.Eliminated)||h.Life==LifeState.Spectator||','h.Life==LifeState.Spectator||').replace('(h.Life==LifeState.Alive&&m.Life!=Ashfall.Modules.Sessions.SessionLife.Alive)','false'),
 'spawn-revision':original.replace('m.SpawnRevision!=a.SpawnRevision||',''),
 'connection-generation':original.replace('b.ConnectionGeneration!=a.ConnectionGeneration','false')}
results=[]
for label,code in mutations.items():
 d=S/'mutations'/label;d.mkdir(parents=True,exist_ok=False);p=d/'HostLoop.cs';p.write_text(code)
 cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/main:R3Tests','/reference:System.Runtime.Serialization.dll','/reference:System.Web.Extensions.dll','/out:'+str(d/'Mutation.exe')]+list(map(str,[p if x==host else x for x in files]+[S/'Scenario.cs',S/'R2Gates.cs',S/'R3Gates.cs']))
 (E/(label+'-command.txt')).write_text(subprocess.list2cmdline(cmd));c=subprocess.run(cmd,capture_output=True);(E/(label+'-compile.log')).write_bytes(c.stdout+c.stderr)
 r=subprocess.run([str(d/'Mutation.exe')],capture_output=True) if c.returncode==0 else None
 if r:(E/(label+'-mutation.log')).write_bytes(r.stdout+r.stderr)
 results.append(dict(mutation=label,compile=c.returncode,exit=r.returncode if r else None,killed=c.returncode==0 and r.returncode==1,output=r.stdout.decode().split('FAIL')[-1].strip() if r else c.stdout.decode()))
(E/'mutations.json').write_text(json.dumps(results,indent=2));print(json.dumps(results,indent=2))
drift=[]
for x in json.loads((R/'docs/agent-runs/20261104-three-chats/unity/RuntimeLoopR3/run01/source-index.json').read_text()):
 p=R/x['original'];current=h(p)
 if current!=x['sha256'].upper():drift.append(dict(path=x['original'],baseline=x['sha256'],current=current,owned='/Integration/RuntimeLoop/' in p.as_posix()))
(E/'end-drift.json').write_text(json.dumps(drift,indent=2));assert all(x['owned'] for x in drift),drift
manifest=[]
for p in sorted(list((R/'unity/Assets/Ashfall/Integration/RuntimeLoop').rglob('*'))+list((R/'unity/Tools/Integration/RuntimeLoop').glob('*'))):
 if p.is_file():manifest.append(dict(path=p.relative_to(R).as_posix(),sha256=h(p),bytes=p.stat().st_size))
(E/'changed-files-sha256.json').write_text(json.dumps(manifest,indent=2))
assert (E/'Scenario.log').read_bytes()==(E/'Scenario-repeat.log').read_bytes()
refs=[]
for p in [Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'),Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data/Managed/UnityEngine/UnityEngine.CoreModule.dll'),Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data/Managed/UnityEngine/UnityEngine.PhysicsModule.dll'),Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data/UnityReferenceAssemblies/unity-4.8-api/Facades/netstandard.dll')]:refs.append(dict(path=str(p),sha256=h(p)))
(E/'compiler-reference-hashes.json').write_text(json.dumps(refs,indent=2))
report={'candidate_manifest_sha256':h(E/'candidate.json'),'changed_manifest_sha256':h(E/'changed-files-sha256.json'),'production_sources':len(rows),'repeat_identical':True,'all_mutations_killed':all(x['killed'] for x in results),'PreparedAdvance_unchanged_sha256':h(R/'unity/Assets/Ashfall/Modules/HealthStates/PreparedAdvance.cs'),'only_owned_production_drift':True}
(E/'seal.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))

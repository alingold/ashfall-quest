from pathlib import Path
import json,hashlib
R=Path.cwd();E=R/'docs/agent-runs/20261104-three-chats/unity/RuntimeLoopR3/final05';S=R/'campaign-staging/20261104-three-chats/unity/RuntimeLoopR3/final05'
def h(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
manifest=[]
for p in sorted(list((R/'unity/Assets/Ashfall/Integration/RuntimeLoop').rglob('*'))+list((R/'unity/Tools/Integration/RuntimeLoop').glob('*'))):
 if p.is_file():manifest.append(dict(path=p.relative_to(R).as_posix(),sha256=h(p),bytes=p.stat().st_size))
(E/'changed-files-sha256.json').write_text(json.dumps(manifest,indent=2))
rows=json.loads((E/'candidate.json').read_text());index=json.loads((R/'docs/agent-runs/20261104-three-chats/unity/RuntimeLoopR3/run01/source-index.json').read_text());drift=[]
for x in index:
 p=R/x['original'];current=h(p)
 if current!=x['sha256'].upper():drift.append(dict(path=x['original'],baseline=x['sha256'],current=current,owned='/Integration/RuntimeLoop/' in p.as_posix(),consumed=any(y['path']==p.relative_to(R).as_posix() for y in rows)))
(E/'end-drift-final.json').write_text(json.dumps(drift,indent=2))
assert (E/'Scenario.log').read_bytes()==(E/'Scenario-repeat.log').read_bytes()
refs=[]
for p in [Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'),Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data/Managed/UnityEngine/UnityEngine.CoreModule.dll'),Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data/Managed/UnityEngine/UnityEngine.PhysicsModule.dll'),Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data/UnityReferenceAssemblies/unity-4.8-api/Facades/netstandard.dll')]:refs.append(dict(path=str(p),sha256=h(p)))
(E/'compiler-reference-hashes.json').write_text(json.dumps(refs,indent=2))
mutations=json.loads((E/'mutations.json').read_text());assert all(x['killed'] for x in mutations)
report={'candidate_manifest_sha256':h(E/'candidate.json'),'changed_manifest_sha256':h(E/'changed-files-sha256.json'),'production_sources':len(rows),'repeat_identical':True,'all_four_mutations_killed':True,'PreparedAdvance_unchanged_sha256':h(R/'unity/Assets/Ashfall/Modules/HealthStates/PreparedAdvance.cs'),'external_live_drift_count':sum(not x['owned'] for x in drift),'external_consumed_drift_count':sum(not x['owned'] and x['consumed'] for x in drift),'integration_current_drift_gate':'OPEN: consumed frozen phase12, not concurrent live changes'}
(E/'seal.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))

from pathlib import Path
import json,hashlib,re
R=Path(__file__).resolve().parents[4];B=R/'campaign-staging/20261104-three-chats/unity/SettingsPersistenceCompositionR7'
D=R/'docs/agent-runs/20261104-three-chats/unity/SettingsPersistenceCompositionR7';O=Path((B/'latest.txt').read_text())
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
before=json.loads((B/'before-inputs.json').read_text());C=R/'docs/agent-runs/20261104-three-chats/unity'
old=next(x for x in before if x['path'].replace('\\','/').endswith('/MigrationRuntime/SettingsMigrationRuntime.cs'))
assert old['sha256'].upper()=='DF2CE1B3F52CC1D32A3E0FF53315A26A49A24C167B5B5F9903B7D721D59B76D8'
drift=[x for x in json.loads((O/'drift.json').read_text()) if x['before'].upper()!=x['after'].upper()]
dependency_drift=[x for x in drift if '/Modules/' in x['path'].replace('\\','/') or '/Core/' in x['path'].replace('\\','/')]
assert not dependency_drift,dependency_drift
totals={p.name:int(re.findall(r'TOTAL (\d+)',p.read_text())[-1]) for p in O.glob('*.log') if re.findall(r'TOTAL (\d+)',p.read_text())}
for p in O.glob('reconciliation-cold-*.log'):
 totals[p.name]=len(re.findall(r'^PASS ',p.read_text(),re.M));assert totals[p.name]==4
assert len(list(O.glob('reconciliation-cold-*.log')))==3
assert totals['RuntimeTests.log']==60 and totals['IndependentTests.log']==72 and totals['PartialWriteTests.log']==20 and totals['ReconciliationTests.log']==68
mutants=json.loads((O/'mutations.json').read_text());assert len(mutants)==12 and all(x['killed'] for x in mutants)
summaries=[]
for i in range(1,7):
 p=C/f'accepted-neutral-v{i}.json';a=json.loads(p.read_text(encoding='utf-8-sig'))
 summaries.append(dict(version=i,sha256=sha(p),keys=list(a),sourceChecks=len(a.get('sourceChecks',[])),accepted=[{k:v for k,v in x.items() if k in ['name','scope','review','manifest']} for x in a.get('accepted',[])]))
index=json.loads((C/'phase21/source-index.json').read_text(encoding='utf-8-sig'))
result=dict(status='implementation candidate; independent review and root adoption open',run=str(O.relative_to(R)),totals=totals,mutants=mutants,accepted=summaries,phase21IndexType=type(index).__name__,phase21IndexKeys=list(index) if isinstance(index,dict) else None,dependencyDrift=dependency_drift,expectedWrapperDelta=[x for x in drift if '/Integration/MigrationRuntime/' in x['path'].replace('\\','/')],coordinationDrift=[x for x in drift if x['path'].startswith('docs')],
 tools=[dict(path=str(p),sha256=sha(p)) for p in [Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'),Path('C:/Users/aling/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe')]])
(D/'results.json').write_text(json.dumps(result,indent=2));print(json.dumps(dict(run=result['run'],totals=totals,mutants=len(mutants),dependencyDrift=result['dependencyDrift']),indent=2))
outputs=[dict(path=str(p.relative_to(R)),bytes=p.stat().st_size,sha256=sha(p)) for folder in [B,R/'unity/Assets/Ashfall/Integration/MigrationRuntime',R/'unity/Tools/Integration/SettingsPersistenceComposition',D] for p in folder.rglob('*') if p.is_file() and p.name not in ['output-sha256.json','output-sha256.digest.txt']]
manifest=D/'output-sha256.json';manifest.write_text(json.dumps(outputs,indent=2));(D/'output-sha256.digest.txt').write_text(sha(manifest)+'\n')

import json,hashlib
from pathlib import Path
R=Path.cwd();B=R/'campaign-staging/20261104-three-chats/unity/ConstructionPreparedR4';D=R/'docs/agent-runs/20261104-three-chats/unity/ConstructionPreparedR4'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
accepted=[]
def walk(x):
 if isinstance(x,dict):
  if 'path' in x and 'expected' in x and (R/x['path']).is_file():accepted.append(dict(path=x['path'],expected=x['expected'].upper(),actual=sha(R/x['path'])))
  for v in x.values():walk(v)
 elif isinstance(x,list):
  for v in x:walk(v)
for p in (B/'context').glob('accepted-neutral-v*.json'):walk(json.loads(p.read_text(encoding='utf-8-sig')))
(D/'accepted-checks.json').write_text(json.dumps(accepted,indent=2))
before={r['original'].replace('\\','/'):r for r in json.loads((B/'before.json').read_text())}
folders=['unity/Assets/Ashfall/Integration/ConstructionRuntime','unity/Assets/Ashfall/Integration/ConstructionTransactions','unity/Assets/Ashfall/Modules/ConstructionCandidate','unity/Tools/Modules/ConstructionCandidate']
files=[p for folder in folders for p in (R/folder).rglob('*') if p.is_file() and '__pycache__' not in str(p)]+[R/'unity/Assets/Ashfall/Modules/InventoryItems/BuildChargeLedger.cs']
changed=[]
for p in files:
 rel=p.relative_to(R).as_posix();old=before.get(rel)
 if old and old['sha256'].upper()==sha(p):continue
 changed.append(dict(path=rel,beforeSha256=old['sha256'].upper() if old else None,sha256=sha(p),bytes=p.stat().st_size))
(D/'changed-files-sha256.json').write_text(json.dumps(changed,indent=2))
phase=json.loads((B/'pins.json').read_text());drift=[dict(path=r['original'],pinned=r['sha256'],current=sha(R/r['original'])) for r in phase if (R/r['original']).exists() and sha(R/r['original'])!=r['sha256'].upper()]
(D/'phase15-drift.json').write_text(json.dumps(drift,indent=2))
inputs=json.loads((B/'run09/sources.json').read_text());drift2=[]
for r in inputs:
 p=R/r['original']
 if p.is_file() and sha(p)!=r['sha256'].upper():drift2.append(dict(path=r['original'],consumed=r['sha256'],current=sha(p)))
(D/'consumed-drift.json').write_text(json.dumps(drift2,indent=2))
# Small source provenance only, no world assets/caches.
provenance=[]
for folder in ['game/scripts','quest-r22/source/scripts']:
 for name in ['field_builder.gd','construction_piece.gd','construction_support.gd','build_grid.gd','building_input.gd']:
  p=R/folder/name;b=p.read_bytes();q=B/'source-reference'/folder/name;q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b);provenance.append(dict(path=str(p.relative_to(R)),sha256=sha(q),bytes=len(b)))
(D/'source-provenance.json').write_text(json.dumps(provenance,indent=2))
immutable=[r for r in before.values() if '/Modules/Construction/' in r['original'].replace('\\','/') or '/Integration/ConstructionGeometry/' in r['original'].replace('\\','/')]
summary=dict(candidateManifestSha256=sha(D/'changed-files-sha256.json'),changedFiles=len(changed),acceptedComparisons=len(accepted),acceptedMismatches=sum(r['expected']!=r['actual'] for r in accepted),acceptedConstructionGeometryChanged=sum(sha(R/r['original'])!=r['sha256'].upper() for r in immutable),phase15Pins=len(phase),phase15Drift=len(drift),consumedInputs=len(inputs),consumedDrift=len(drift2),InputGenerationPortsUnchanged=sha(R/'unity/Assets/Ashfall/Integration/ConstructionRuntime/Ports.cs')==before['unity/Assets/Ashfall/Integration/ConstructionRuntime/Ports.cs']['sha256'].upper(),CanonicalBuilderUnchanged=sha(R/'unity/Assets/Ashfall/Integration/ConstructionTransactions/CanonicalBuilder.cs')==before['unity/Assets/Ashfall/Integration/ConstructionTransactions/CanonicalBuilder.cs']['sha256'].upper())
(D/'audit-summary.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2))
print([(r['main'],r['compileExit'],r.get('testExit')) for r in json.loads((B/'run09/results.json').read_text())])
print((B/'run09/PreparedR4Tests-tests.log').read_text().splitlines()[-1])
assert summary['acceptedMismatches']==0 and summary['acceptedConstructionGeometryChanged']==0


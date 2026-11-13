from pathlib import Path
import json,hashlib
R=Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/PlayerR6';D=R/'docs/agent-runs/20261104-three-chats/unity/PlayerR6'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def write(name,value):(D/name).write_text(json.dumps(value,indent=2))
before=json.loads((D/'before.json').read_text());drift=[]
for e in before:
 p=Path(e['path']);snapshot=Path(e['snapshot'])
 if sha(snapshot)!=e['sha256'].upper():raise RuntimeError('preserved bytes changed')
 drift.append(dict(path=str(p),before=e['sha256'].upper(),current=sha(p),changed=sha(p)!=e['sha256'].upper(),player='\\Modules\\Player\\' in str(p)))
known={e['path'] for e in before}
for folder in ['Modules/Flight','Integration/PersonalSettings','Integration/ClimbingMotor','Modules/Climbing']:
 for p in (R/'unity/Assets/Ashfall'/folder).rglob('*.cs'):
  if str(p) not in known:drift.append(dict(path=str(p),newDependency=True,current=sha(p)))
write('dependency-drift.json',drift)
inputs=json.loads((D/'compile-inputs.json').read_text());current=[]
for e in inputs:
 p=Path(e['path']);snap=Path(e['snapshot'])
 if sha(snap)!=e['sha256'].upper():raise RuntimeError('test inputs changed')
 if str(p).startswith(str(R/'unity/Assets')):current.append(dict(path=str(p),tested=sha(snap),current=sha(p),match=sha(p)==sha(snap)))
write('tested-current-stability.json',current)
sources=list((R/'unity/Assets/Ashfall/Modules/Player').rglob('*.cs'))+list((R/'unity/Tools/Modules/Player/r6').rglob('*'))
sourceRecords=[]
for p in sources:
 if not p.is_file():continue
 dest=S/'candidate-source'/p.relative_to(R);dest.parent.mkdir(parents=True,exist_ok=True)
 if dest.exists():raise RuntimeError('candidate source already sealed')
 dest.write_bytes(p.read_bytes());sourceRecords.append(dict(path=str(p),snapshot=str(dest),sha256=sha(dest),bytes=dest.stat().st_size))
write('source-manifest.json',sourceRecords)
write('handoff.json',dict(status='UNREVIEWED CANDIDATE',retained=dict(passed=936,failed=0),reviewerRegression=dict(passed=51,failed=0),newR6=dict(passed=94,failed=0),currentComposition=dict(passed=6,failed=0),currentSettingsComposition=dict(passed=3,failed=0),native='UNRUN; coordinator lane required',angular='OPEN; current real backend rejects nonzero yaw',independentReview='REQUIRED',testedCurrentStable=all(e['match'] for e in current)))
changed=[]
beforeMap={e['path']:e for e in before}
for p in (R/'unity/Assets/Ashfall/Modules/Player').rglob('*.cs'):
 old=beforeMap.get(str(p))
 if old is None or old['sha256'].upper()!=sha(p):changed.append(dict(path=str(p),before=old['sha256'].upper() if old else None,sha256=sha(p),bytes=p.stat().st_size))
write('changed-production.json',changed)
files=list((R/'unity/Tools/Modules/Player/r6').rglob('*'))+list(S.rglob('*'))+list(D.rglob('*'))+[Path(e['path']) for e in changed]
manifest=[]
for p in sorted(set(files)):
 if p.is_file() and p.name not in ['changed-file-manifest.json','manifest-receipt.json']:manifest.append(dict(path=str(p),sha256=sha(p),bytes=p.stat().st_size))
write('changed-file-manifest.json',manifest)
write('manifest-receipt.json',dict(path=str(D/'changed-file-manifest.json'),sha256=sha(D/'changed-file-manifest.json'),entries=len(manifest)))
print(json.dumps(dict(productionChanges=len(changed),sourceFiles=len(sourceRecords),manifestEntries=len(manifest),testedCurrentStable=all(e['match'] for e in current),manifestSha256=sha(D/'changed-file-manifest.json')),indent=2))

from pathlib import Path
import json,hashlib
R=Path(__file__).resolve().parents[5];S=R/'campaign-staging/20261104-three-chats/unity/SessionsR4/r5';D=R/'docs/agent-runs/20261104-three-chats/unity/SessionsR4/r5'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
verification=[]
for name in ['Sessions/r3','Networking/r3']:
 p=R/'docs/agent-runs/20261104-three-chats/unity'/name/'candidate-manifest.json';data=json.loads(p.read_text());entries=data.get('artifacts',data.get('files',[]))
 if not entries:
  entries=next(v for v in data.values() if isinstance(v,list) and v and isinstance(v[0],dict) and 'sha256' in v[0])
 checks=[]
 for x in entries:
  path=Path(x['path']);path=path if path.is_absolute() else R/path
  # Historical manifests also list then-live production, now deliberately changed.
  checks.append(dict(path=str(path),expected=x['sha256'],actual=sha(path) if path.exists() else None,historical_artifact='unity/Assets' not in str(path).replace('\\','/')))
 verification.append(dict(manifest=str(p),sha256=sha(p),checks=checks))
(D/'historical-manifest-verification.json').write_text(json.dumps(verification,indent=2))
accepted=[]
def walk(x):
 if isinstance(x,dict):
  if 'path' in x and 'expected' in x:
   p=Path(x['path']);p=p if p.is_absolute() else R/p
   accepted.append(dict(path=str(p),expected=x['expected'],actual=sha(p) if p.exists() else None))
  for v in x.values():walk(v)
 elif isinstance(x,list):
  for v in x:walk(v)
for n in ['accepted-neutral-v1.json','accepted-neutral-v2.json']:walk(json.loads((R/'docs/agent-runs/20261104-three-chats/unity'/n).read_text(encoding='utf-8-sig')))
(D/'accepted-verification.json').write_text(json.dumps(accepted,indent=2))
latest=sorted(D.glob('attempt-*'))[-1];inputs=json.loads((latest/'inputs.json').read_text())
(D/'final-dependency-verification.json').write_text(json.dumps([dict(x,current_sha256=sha(Path(x['path'])),matches=sha(Path(x['path']))==x['sha256']) for x in inputs],indent=2))
roots=[S,D,R/'unity/Assets/Ashfall/Modules/Sessions',R/'unity/Assets/Ashfall/Integration/Sessions',R/'unity/Tools/Integration/Sessions/r5']
entries=[dict(path=str(p.relative_to(R)),bytes=p.stat().st_size,sha256=sha(p)) for root in roots for p in sorted(root.rglob('*')) if p.is_file() and p.name not in ['candidate-manifest.json','manifest.sha256.txt'] and '__pycache__' not in str(p)]
p=D/'candidate-manifest.json';p.write_text(json.dumps(dict(status='unreviewed-candidate',artifacts=entries),indent=2));(D/'manifest.sha256.txt').write_text(sha(p)+'\n')
print('MANIFEST',sha(p),'entries',len(entries));print('accepted mismatches',sum(x['expected'].upper()!=str(x['actual']).upper() for x in accepted));print('dependency drift',sum(sha(Path(x['path']))!=x['sha256'] for x in inputs))

from pathlib import Path
import hashlib,json
R=Path(r'C:\Users\aling\Documents\ashfall');E=R/'docs/agent-runs/20261104-three-chats/unity/ConsumableCompositionR3';O=R/'campaign-staging/20261104-three-chats/unity/ConsumableCompositionR3'
# Candidate bytes must equal the final executed inputs and sealed source snapshots.
source=json.loads((E/'candidate-source.json').read_text())
for x in source:
 assert hashlib.sha256((R/x['path']).read_bytes()).hexdigest().upper()==x['sha256']
 assert hashlib.sha256((R/x['snapshot']).read_bytes()).hexdigest().upper()==x['sha256']
results=json.loads((E/'run04/results.json').read_text());assert all(x['compile']==0 and x.get('exit',0)==0 for x in results)
# Preserve authoring helpers with final evidence, without changing the code manifest.
for p in (R/'unity/Tools/Integration/ConsumableTransactions').glob('*R3*'):
 q=O/'final-tools'/p.name;q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(p.read_bytes())
rows=[]
for base in [E,O]:
 for p in sorted(base.rglob('*')):
  if p.is_file():rows.append(dict(path=str(p.relative_to(R)),bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest().upper()))
p=E/'SHA256-manifest.json';assert not p.exists();p.write_text(json.dumps(rows,indent=2));h=hashlib.sha256(p.read_bytes()).hexdigest().upper();(E/'SHA256-manifest.sha256').write_text(h+'\n');print('Sealed',len(rows),'artifacts; SHA256',h);print('Final suites',len(results),'all compile/execute success; candidate source bytes unchanged')

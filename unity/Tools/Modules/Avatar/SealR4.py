from pathlib import Path
import json,hashlib,shutil
R=Path(__file__).resolve().parents[4];S=R/'campaign-staging/20261104-three-chats/unity/AvatarR4';E=R/'docs/agent-runs/20261104-three-chats/unity/AvatarR4'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
if (E/'candidate-manifest.json').exists():raise SystemExit('Frozen already; allocate a new revision')
# Tools added after initial source freeze are included now before sealing.
for p in (R/'unity/Tools/Modules/Avatar').rglob('*'):
 if p.is_file() and not (S/'frozen'/p.relative_to(R)).exists():
  q=S/'frozen'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,q)
records=json.loads((E/'consumed-before.json').read_text())
after=[dict(x,currentSha256=sha(Path(x['path'])),matches=sha(Path(x['path']))==x['sha256']) for x in records]
(E/'dependency-drift.json').write_text(json.dumps(after,indent=2));assert all(x['matches'] for x in after)
files=sorted([p for root in [S,E] for p in root.rglob('*') if p.is_file()]+list((R/'unity/Assets/Ashfall/Modules/Avatar').rglob('*.cs'))+list((R/'unity/Tools/Modules/Avatar').glob('*')))
rows=[dict(path=str(p.relative_to(R)),bytes=p.stat().st_size,sha256=sha(p)) for p in files if p.is_file()]
compiler=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe');python=Path(r'C:\Users\aling\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe')
manifest=dict(schema=1,candidate='AvatarR4/source-grip-material-admission-1',status='unreviewed-candidate',editor='6000.6.8f1',engineResult='NOT_RUN_OR_COMPILED',files=rows,tools=[dict(path=str(p),sha256=sha(p)) for p in [compiler,python]])
p=E/'candidate-manifest.json';p.write_text(json.dumps(manifest,indent=2));digest=sha(p);(E/'candidate-manifest.sha256').write_text(digest+'\n');print('SEALED',len(rows),'entries',digest)

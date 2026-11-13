from pathlib import Path
import json,hashlib,re
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/MotionRuntimeLoopCompositionR5';E=R/'docs/agent-runs/20261104-three-chats/unity/MotionRuntimeLoopCompositionR5'
h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
rows=json.loads((E/'inputs.json').read_text());drift=[]
for x in rows:
 p=R/x['path'];snap=R/x['snapshot'];assert h(snap)==x['sha256']
 if p.exists() and h(p)!=x['sha256']:drift.append(dict(path=x['path'],pinned=x['sha256'],now=h(p),owned='/RuntimeLoop/' in x['path']))
(E/'dependency-drift.json').write_text(json.dumps(drift,indent=2))
index=json.loads((R/'docs/agent-runs/20261104-three-chats/unity/phase21/source-index.json').read_text(encoding='utf-8-sig'))
(E/'phase21-read.json').write_text(json.dumps(dict(count=len(index),sha256=h(R/'docs/agent-runs/20261104-three-chats/unity/phase21/source-index.json'),scope='coordination evidence; current motion sources independently pinned once'),indent=2))
symbols=[]
for p in (R/'unity/Assets/Ashfall/Integration/RuntimeLoop').glob('*.cs'):
 symbols.append(dict(path=str(p.relative_to(R)),sha256=h(p),symbols=[dict(line=i,text=s.strip()) for i,s in enumerate(p.read_text().splitlines(),1) if re.search(r'public .*\(|public enum|PublicationBlocker|MotionMode!=',s)]))
(E/'source-symbols.json').write_text(json.dumps(symbols,indent=2))
manifest=[]
for root in [S,E,R/'unity/Assets/Ashfall/Integration/RuntimeLoop',R/'unity/Tools/Integration/RuntimeLoop']:
 for p in root.rglob('*'):
  if p.is_file() and p.name!='sha256-manifest.json':manifest.append(dict(path=p.relative_to(R).as_posix(),bytes=p.stat().st_size,sha256=h(p)))
(E/'sha256-manifest.json').write_text(json.dumps(manifest,indent=2));print(len(manifest),'hashed artifacts;',len(drift),'changes since pin (owned or recorded drift)')

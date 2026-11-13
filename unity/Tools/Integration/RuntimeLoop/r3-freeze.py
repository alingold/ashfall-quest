from pathlib import Path
import json,hashlib
R=Path.cwd(); S=R/'campaign-staging/20261104-three-chats/unity/RuntimeLoopR3/run01'; E=R/'docs/agent-runs/20261104-three-chats/unity/RuntimeLoopR3/run01'
S.mkdir(parents=True,exist_ok=False); E.mkdir(parents=True,exist_ok=False)
def h(b): return hashlib.sha256(b).hexdigest().upper()
rows=json.loads((R/'docs/agent-runs/20261104-three-chats/unity/phase12/source-index.json').read_text())
drift=[]
for row in rows:
 p=R/row['original']; src=R/row['snapshot']; b=src.read_bytes(); assert h(b)==row['sha256'].upper(),src
 d=S/'baseline'/row['original']; d.parent.mkdir(parents=True,exist_ok=True); d.write_bytes(b)
 if p.exists() and h(p.read_bytes())!=h(b): drift.append(dict(path=row['original'],frozen=h(b),current=h(p.read_bytes())))
(E/'source-index.json').write_text(json.dumps(rows,indent=2)); (E/'initial-drift.json').write_text(json.dumps(drift,indent=2))
context=[]
paths=list((R/'unity/Documentation').glob('*.md'))+list((R/'docs/agent-runs/20261104-three-chats/unity').glob('accepted-neutral-v*.json'))+[R/'docs/agent-runs/20261104-three-chats/unity/phase12/root-validation.json',R/'docs/agent-runs/20261104-three-chats/unity/phase12/player-retry-validation.json']
for p in paths:
 b=p.read_bytes(); p.read_text(encoding='utf-8-sig'); context.append(dict(path=str(p.relative_to(R)),sha256=h(b),bytes=len(b)))
(E/'context.json').write_text(json.dumps(context,indent=2))
baseline=[]
for p in list((R/'unity/Assets/Ashfall/Integration/RuntimeLoop').rglob('*'))+[R/'unity/Assets/Ashfall/Modules/HealthStates/PreparedAdvance.cs']+list((R/'unity/Tools/Integration/RuntimeLoop').glob('*')):
 if p.is_file():
  b=p.read_bytes(); d=S/'live-before'/p.relative_to(R);d.parent.mkdir(parents=True,exist_ok=True);d.write_bytes(b);baseline.append(dict(path=p.relative_to(R).as_posix(),sha256=h(b),bytes=len(b)))
(E/'live-before.json').write_text(json.dumps(baseline,indent=2))
print('Frozen',len(rows),'phase12 sources; live drift',len(drift));print(json.dumps(drift,indent=2))

from pathlib import Path
import json,hashlib,re,shutil
R=Path(r'C:\Users\aling\Documents\ashfall');D=R/'docs/agent-runs/20261104-three-chats/unity/MatchPingRecoveryR12';S=R/'campaign-staging/20261104-three-chats/unity/MatchPingRecoveryR12'
# Freeze the policy envelope documents separately; no source/dependency refresh.
for name in ['TWO-CHAT-20-AGENT-OVERRIDE.md','HILL-CLIMB-ORCHESTRATION.md','OVERNIGHT-AGENT-PLAN-2026-11-03.md','AGENT-DISPATCH-BRIEFS-2026-11-03.md','agent-runs/20261104-0020/preflight.json']:
 p=R/'docs'/name;q=S/'context/docs'/name;q.parent.mkdir(parents=True,exist_ok=True)
 if not q.exists():shutil.copyfile(p,q)
paths=set(p for root in [S,D,R/'unity/Tools/Integration/MatchPingRecovery',R/'unity/Assets/Ashfall/Integration/MatchDirector',R/'unity/Assets/Ashfall/Integration/MatchRecovery'] for p in root.rglob('*') if p.is_file() and p.name!='SHA256-manifest.json')
for rsp in S.rglob('*.rsp'):
 for v in re.findall(r'"([^"]+)"',rsp.read_text()):
  p=Path(v)
  if p.exists() and p.is_file():paths.add(p)
rows=[dict(path=str(p.relative_to(R)),bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in sorted(paths)]
(D/'SHA256-manifest.json').write_text(json.dumps(rows,indent=2))
latest=json.loads((D/'latest.json').read_text());O=R/latest['run'];pins=json.loads((D/(O.name+'-pins.json')).read_text())
for row in pins:
 p=R/row['snapshot'];assert hashlib.sha256(p.read_bytes()).hexdigest()==row['sha256']
for label in ['DirectorArchive.cs','DirectorPingArchive.cs']:
 p=R/'unity/Assets/Ashfall/Integration/MatchDirector'/label
 print(label,hashlib.sha256(p.read_bytes()).hexdigest())
print('manifest entries',len(rows));print('report',D/'REPORT.md')

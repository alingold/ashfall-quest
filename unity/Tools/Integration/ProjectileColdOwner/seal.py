from pathlib import Path
import json,hashlib
R=Path.cwd();D=R/'docs/agent-runs/20261104-three-chats/unity/ProjectileColdOwnerR5';S=R/'campaign-staging/20261104-three-chats/unity/ProjectileColdOwnerR5'
p=D/'REPORT.md';s=p.read_text().replace('Final run07','Final run09').replace('Four run07','Four run09').replace('mutations07','mutations09').replace('pinned to run07','pinned to run09').replace('run03 through run07','run03 through run09')
s=s.replace("The runner preserves compiler and executable logs; the second LaunchTests log is the extended generated fixture and replaces only that run directory's initial public log, with both result records retained.","Final run09 preserves separate public LaunchTests, PreparedCaptureTests and cold write/read logs. Earlier run directories preserve their initial log-naming limitations (shared executable main names overwrote the corresponding log within those runs); no earlier evidence was edited.")
p.write_text(s)
trace=[]
for row in json.loads((R/'docs/agent-runs/20261104-three-chats/unity/ProjectilesR3/actual-source-symbols.json').read_text()):
 p=Path(row['path']);trace.append({'path':str(p),'expected':row['sha256'],'actual':hashlib.sha256(p.read_bytes()).hexdigest() if p.is_file() else None,'symbols':row['symbols']})
(D/'original-source-trace.json').write_text(json.dumps(trace,indent=2))
files=[]
for root in [D,S,R/'unity/Tools/Integration/ProjectileColdOwner',R/'unity/Assets/Ashfall/Modules/Projectiles',R/'unity/Assets/Ashfall/Integration/ProjectileWeapons']:
 for p in root.rglob('*'):
  if p.is_file() and p.name!='SHA256-manifest.json':files.append({'path':p.relative_to(R).as_posix(),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
(D/'SHA256-manifest.json').write_text(json.dumps(files,indent=2));print('sealed',len(files),'files; original source traces',len(trace),'matching',sum(x['actual']==x['expected'] for x in trace))

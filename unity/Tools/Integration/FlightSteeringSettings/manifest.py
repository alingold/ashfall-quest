from pathlib import Path
import json,hashlib
r=Path.cwd();d=r/'docs/agent-runs/20261104-three-chats/unity/FlightSteeringSettingsR1';s=r/'campaign-staging/20261104-three-chats/unity/FlightSteeringSettingsR1'
paths=list(s.rglob('*'))+list(d.glob('*'))+list((r/'unity/Tools/Integration/FlightSteeringSettings').glob('*'))
paths += [r/x['path'] for x in json.loads((d/'production-tool-sha256.json').read_text()) if 'Assets' in x['path']]
rows=[dict(path=str(p.relative_to(r)),bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in sorted(set(paths)) if p.is_file() and p.name!='output-manifest.json']
(d/'output-manifest.json').write_text(json.dumps(rows,indent=2));print('SHA manifest',len(rows),'files')

from pathlib import Path
import shutil,hashlib,json
r=Path(r'C:\Users\aling\Documents\ashfall');o=r/'campaign-staging/20261104-three-chats/unity/PlayerIntegrationR5/r5'
paths=list((r/'unity/Assets/Ashfall/Modules/Player').rglob('*.cs'))+[r/'unity/Assets/Ashfall/Core/Contracts.cs']
paths+=list((r/'unity/Assets/Ashfall/Modules/Flight').rglob('*.cs'))+list((r/'unity/Assets/Ashfall/Modules/Climbing').rglob('*.cs'))+list((r/'unity/Assets/Ashfall/Integration/PersonalSettings').glob('*.cs'))
paths += [r/'quest-r22/source/scripts/player.gd',r/'game/scripts/player.gd']
rows=[]
for p in paths:
 q=o/'before'/p.relative_to(r);q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q);rows.append(dict(path=str(p.relative_to(r)),sha256=hashlib.sha256(p.read_bytes()).hexdigest(),bytes=p.stat().st_size))
(o/'before.json').write_text(json.dumps(rows,indent=2))
print(len(rows),'small source snapshots preserved')

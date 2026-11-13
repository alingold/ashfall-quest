from pathlib import Path
import hashlib,json,shutil
R=Path(r'C:\Users\aling\Documents\ashfall')
S=R/'campaign-staging/20261104-three-chats/unity/PlayerR6'
D=R/'docs/agent-runs/20261104-three-chats/unity/PlayerR6'
files=list((R/'unity/Assets/Ashfall/Modules/Player').rglob('*.cs'))
files += list((R/'unity/Assets/Ashfall/Integration/ClimbingMotor').rglob('*.cs'))
files += list((R/'unity/Assets/Ashfall/Modules/Climbing').rglob('*.cs'))
files += list((R/'unity/Assets/Ashfall/Modules/Flight').rglob('*.cs'))
files += list((R/'unity/Assets/Ashfall/Integration/PersonalSettings').rglob('*.cs'))
files += [R/'unity/Assets/Ashfall/Core/Contracts.cs',R/'game/scripts/player.gd',R/'quest-r22/source/scripts/player.gd']
records=[]
for p in files:
 b=p.read_bytes(); dest=S/'before'/p.relative_to(R);dest.parent.mkdir(parents=True,exist_ok=True)
 if dest.exists():raise RuntimeError('preserved input already exists')
 dest.write_bytes(b);records.append(dict(path=str(p),snapshot=str(dest),sha256=hashlib.sha256(b).hexdigest(),bytes=len(b)))
(D/'before.json').write_text(json.dumps(records,indent=2))
# Parse all bounded acceptance receipts without expanding their thousands of embedded records.
receipts=[]
for p in sorted((R/'docs/agent-runs/20261104-three-chats/unity').glob('accepted-neutral-v*.json')):
 v=json.loads(p.read_text(encoding='utf-8-sig'));receipts.append(dict(path=str(p),sha256=hashlib.sha256(p.read_bytes()).hexdigest(),keys=list(v),bytes=p.stat().st_size))
(D/'accepted-receipts.json').write_text(json.dumps(receipts,indent=2))
print('Preserved',len(records),'small source files; read',len(receipts),'acceptance receipts')

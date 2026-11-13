from pathlib import Path
import json,hashlib
root=Path(__file__).resolve().parents[5]
paths=[root/'unity/Assets/Ashfall/Modules/Flight',root/'unity/Assets/Ashfall/Integration/FlightMotor',root/'unity/Tools/Modules/Flight/r3',root/'unity/Tools/Integration/FlightMotor',root/'campaign-staging/20261104-three-chats/unity/Flight/r3',root/'docs/agent-runs/20261104-three-chats/unity/Flight/phase03']
manifest=paths[-1]/'candidate-manifest.json'
files=sorted({p for d in paths for p in d.rglob('*') if p.is_file() and p!=manifest})
records=[{'path':p.relative_to(root).as_posix(),'sha256':hashlib.sha256(p.read_bytes()).hexdigest().upper()} for p in files]
manifest.write_text(json.dumps({'candidate':'FlightR3-phase03-candidate-01','status':'unreviewed','finalEvidence':'run03','files':records},indent=2)+'\n')
print(str(manifest));print(hashlib.sha256(manifest.read_bytes()).hexdigest().upper());print(len(records),'frozen entries')

from pathlib import Path
import json,hashlib
r=Path.cwd();d=r/'docs/agent-runs/20261104-three-chats/unity/HostEpochAuthorityR1';s=r/'campaign-staging/20261104-three-chats/unity/HostEpochAuthorityR1'
records=[]
for p in [r/'unity/Assets/Ashfall/Integration/CombatPublication/ProducerRouting.cs',r/'unity/Assets/Ashfall/Integration/MatchDirector/HealthProducerDrain.cs']:
 b=p.read_bytes();q=s/'match-r9-readonly'/p.name;q.parent.mkdir(exist_ok=True);q.write_bytes(b);records.append(dict(source=str(p),snapshot=str(q),sha256=hashlib.sha256(b).hexdigest(),compiled=False))
(d/'match-r9-readonly.json').write_text(json.dumps(records,indent=2))
files=[p for base in [r/'unity/Assets/Ashfall/Integration/HostEpochAuthority',r/'unity/Tools/Integration/HostEpochAuthority',d,s] for p in base.rglob('*') if p.is_file() and p.name not in ['SHA256-manifest.json','SHA256-manifest.digest.txt']]
manifest=[dict(path=str(p.relative_to(r)),bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in sorted(files)]
p=d/'SHA256-manifest.json';assert not p.exists();p.write_text(json.dumps(manifest,indent=2));digest=hashlib.sha256(p.read_bytes()).hexdigest();(d/'SHA256-manifest.digest.txt').write_text(digest+'\n');print('Manifest',digest,'files',len(manifest))

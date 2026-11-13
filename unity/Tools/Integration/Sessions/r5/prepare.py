from pathlib import Path
import shutil,json,hashlib
R=Path(__file__).resolve().parents[5]
S=R/'campaign-staging/20261104-three-chats/unity/SessionsR4/r5'
D=R/'docs/agent-runs/20261104-three-chats/unity/SessionsR4/r5'
records=[]
for folder in ['unity/Assets/Ashfall/Modules/Sessions','unity/Assets/Ashfall/Integration/Sessions','unity/Assets/Ashfall/Modules/Networking','unity/Assets/Ashfall/Core']:
 for p in (R/folder).glob('*.cs'):
  q=S/'before'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q)
  records.append(dict(path=str(p),snapshot=str(q),sha256=hashlib.sha256(q.read_bytes()).hexdigest()))
(D/'before.json').write_text(json.dumps(records,indent=2))
(S/'owned-contracts-before-implementation.txt').write_text('Session.JoinTrusted/DisconnectTrusted: canonical sessions-host/{sequence}, authenticated gate, independent monotonic lifetime watermark; no gameplay receipt consumption. MemberSnapshot.CanonicalActorId immutable across authenticated token remaps. Coordinator.ConnectionGeneration(peer), Disconnect(peer,generation,tick,kicked) reject stale generations; server-derived client receipt namespace. Networking lease API absent in initial snapshot: fail closed, no identity-history deletion or synthetic success. Trusted RecordPosition binds active actor and epoch. All ports serialized synchronous nonthrowing.\n')

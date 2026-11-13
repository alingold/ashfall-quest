from pathlib import Path
import shutil,hashlib,json
R=Path(r'C:\Users\aling\Documents\ashfall'); S=R/'campaign-staging/20261104-three-chats/unity/SessionAdmissionR6';D=R/'docs/agent-runs/20261104-three-chats/unity/SessionAdmissionR6'
files=[*R.glob('unity/Assets/Ashfall/Modules/Networking/**/*.cs'),R/'unity/Assets/Ashfall/Modules/Sessions/Session.cs',R/'unity/Assets/Ashfall/Modules/Sessions/Ports.cs',*R.glob('unity/Assets/Ashfall/Integration/Sessions/*.cs')]
records=[]
for p in files:
 q=S/'before'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q);records.append(dict(path=str(p),snapshot=str(q),sha256=hashlib.sha256(q.read_bytes()).hexdigest()))
(D/'before.json').write_text(json.dumps(records,indent=2))
(S/'contracts-before.txt').write_text('PeerLease owns raw ingress Sequence/Pending/Seen; AuthorityHost maps accepted ingress to epoch+generation framed canonical receipts before dispatch, canonical Actor sequence/tick/history retained. Session nested PreparedJoin: PrepareJoinTrusted(context,request,out token); ValidateJoin(token); CommitJoinValidated(token); AbortJoin(token). Prepare copies bounded publication stores, clones reconnect member; validate exact session revision/context/time; commit swaps preallocated stores without callbacks. Coordinator prepares both, validates domain then peer on pure serialized trusted lane, publishes both immediately. Explicit authenticated callback generation required for reusable peer keys. No logical damage/event history resets.\n')

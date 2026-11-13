from pathlib import Path
import hashlib,json,sys
R=Path(r'C:\Users\aling\Documents\ashfall')
D=R/'docs/agent-runs/20261104-three-chats/unity/HostMotionArbitrationR1/run01'
O=R/'campaign-staging/20261104-three-chats/unity/HostMotionArbitrationR1/run01'
index=D/'consumed.json'
suffix=sys.argv[1];assert suffix.isalnum()
D=D/suffix;D.mkdir()
admin_out=O/suffix;admin_out.mkdir()
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
admin=[R/'docs/TWO-CHAT-20-AGENT-OVERRIDE.md',R/'docs/HILL-CLIMB-ORCHESTRATION.md',R/'docs/OVERNIGHT-AGENT-PLAN-2026-11-03.md',R/'docs/AGENT-DISPATCH-BRIEFS-2026-11-03.md',R/'docs/agent-runs/20261104-0020/preflight.json']
admin+=list((R/'unity/Documentation').glob('*.md'))
admin+=list((R/'docs/agent-runs/20261104-three-chats/unity').glob('accepted-neutral-v*.json'))
admin+=[R/'docs/agent-runs/20261104-three-chats/unity/phase12'/x for x in ['source-index.json','root-validation.json','player-retry-validation.json']]
rows=[]
for i,p in enumerate(admin):
    data=p.read_bytes();q=admin_out/'administrative'/(str(i)+'-'+p.name);q.parent.mkdir(exist_ok=True);q.write_bytes(data)
    rows.append(dict(original=str(p),snapshot=str(q),sha256=sha(q)))
(D/'administrative.json').write_text(json.dumps(rows,indent=2))
inputs=json.loads(index.read_text(encoding='utf-8-sig'))
drift=[]
for x in inputs:
    p=R/x['original'];q=R/x['consumed'];assert sha(q)==x['sha256']
    if sha(p)!=x['sha256']:drift.append(dict(original=x['original'],consumed=x['sha256'],current=sha(p)))
(D/'final-drift.json').write_text(json.dumps(dict(frozenVerified=len(inputs),currentDrift=drift),indent=2))
owned=[]
for folder in [R/'unity/Assets/Ashfall/Integration/MotionAuthority',R/'unity/Tools/Integration/MotionAuthority']:
    for p in folder.rglob('*'):
        if p.is_file():owned.append(dict(path=str(p),sha256=sha(p),bytes=p.stat().st_size))
owned.append(dict(path=str(O/'NativeFixture.cs'),sha256=sha(O/'NativeFixture.cs')))
(D/'candidate-hashes.json').write_text(json.dumps(owned,indent=2))
symbols=[]
for x in inputs:
    if any(s in x['original'] for s in ['PlayerFlightMotor.cs','ClimbingRootMotor.cs','Capsules.cs','PlayerMotorFeedback.cs','PreparedPlayerStep.cs','RootMotionSafety.cs','ClimbController.cs','FlightMachine.cs']):symbols.append(x)
(D/'source-symbol-revisions.json').write_text(json.dumps(symbols,indent=2))
print('sealed',len(owned),'candidate files;',len(inputs),'frozen source hashes verified;',len(drift),'live drift entries')

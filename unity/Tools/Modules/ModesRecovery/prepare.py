from pathlib import Path
import hashlib,json
R=Path('C:/Users/aling/Documents/ashfall'); S=R/'campaign-staging/20261104-three-chats/unity/PingOwnerRecoveryR1'; D=R/'docs/agent-runs/20261104-three-chats/unity/PingOwnerRecoveryR1'
def sha(b): return hashlib.sha256(b).hexdigest()
reads=[]
paths=[R/'docs'/x for x in ['TWO-CHAT-20-AGENT-OVERRIDE.md','HILL-CLIMB-ORCHESTRATION.md','OVERNIGHT-AGENT-PLAN-2026-11-03.md','AGENT-DISPATCH-BRIEFS-2026-11-03.md']]
paths+=list((R/'unity/Documentation').glob('*.md'))
U=R/'docs/agent-runs/20261104-three-chats/unity'
paths+=[R/'docs/agent-runs/20261104-0020/preflight.json',U/'GAMEPLAY-CHECKPOINT-v1.md']+list(U.glob('accepted-neutral-v*.json'))
for p in [U/'phase16',U/'phase18',U/'phase19',U/'Modes']:
 paths+=list(p.glob('*.json'))+list(p.glob('*.md'))
paths+=list((U/'Modes/evidence/r2').glob('*'))
for p in paths:
 if p.is_file():
  b=p.read_bytes(); t=b.decode('utf-8-sig');
  if p.suffix=='.json': json.loads(t)
  reads.append(dict(path=str(p.relative_to(R)),bytes=len(b),sha256=sha(b)))
(D/'read-inputs.json').write_text(json.dumps(reads,indent=2))
inputs=list((R/'unity/Assets/Ashfall/Modules/Modes').glob('*.cs'))+[R/'unity/Assets/Ashfall/Core/Contracts.cs',R/'unity/Assets/Ashfall/RecoveryContracts/RecoveryWire.cs']
pins=[]
for p in inputs:
 b=p.read_bytes(); q=S/'inputs'/p.name;q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b);pins.append(dict(path=str(p.relative_to(R)),snapshot=str(q.relative_to(R)),sha256=sha(b)))
accepted=json.loads((U/'accepted-neutral-v1.json').read_text(encoding='utf-8-sig'))
for x in accepted['sourceChecks']:
 if x['name']=='Modes':
  p=Path(x['path']); assert sha(p.read_bytes()).lower()==x['expected'].lower()
(D/'input-pins.json').write_text(json.dumps(pins,indent=2))
p=(S/'inputs/PingService.cs').read_text();p=p.replace('namespace Ashfall.Modules.Modes','namespace Ashfall.Modules.ModesRecovery').replace('using Ashfall.Core;','using Ashfall.Core;\nusing Ashfall.Modules.Modes;').replace('PingRecord','RecoverablePingRecord').replace('PingService','RecoverablePingService').replace('CommandLedger','RecoveryPingLedger').replace('public sealed class','public sealed partial class')
(R/'unity/Assets/Ashfall/Modules/ModesRecovery/RecoverablePingService.cs').write_text(p)
p=(S/'inputs/CommandLedger.cs').read_text().replace('namespace Ashfall.Modules.Modes','namespace Ashfall.Modules.ModesRecovery').replace('CommandLedger','RecoveryPingLedger').replace('internal sealed class','internal sealed partial class')
(R/'unity/Assets/Ashfall/Modules/ModesRecovery/RecoveryPingLedger.cs').write_text(p)
print('Read',len(reads),'documents; froze',len(pins),'actual source inputs; accepted Modes hashes MATCH')

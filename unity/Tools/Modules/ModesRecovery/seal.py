from pathlib import Path
import hashlib,json
R=Path('C:/Users/aling/Documents/ashfall');S=R/'campaign-staging/20261104-three-chats/unity/PingOwnerRecoveryR1';D=R/'docs/agent-runs/20261104-three-chats/unity/PingOwnerRecoveryR1';U=R/'docs/agent-runs/20261104-three-chats/unity'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
evidence=[]
for p in list((U/'desktop-playtest/r1').glob('*'))+[U/'phase18/source-index.json',U/'phase18/root-manifest.json',U/'phase16/source-index.json',U/'phase16/dispatch-plan.json']:
 if p.is_file():
  data=p.read_bytes();text=data.decode('utf-8-sig');obj=json.loads(text) if p.suffix=='.json' else None
  evidence.append(dict(path=str(p.relative_to(R)),bytes=len(data),sha256=sha(p),entries=len(obj) if isinstance(obj,list) else None))
(D/'root-evidence-read.json').write_text(json.dumps(evidence,indent=2))
# Exact mechanical derivation proof: no gameplay method body changes.
p=(S/'inputs/PingService.cs').read_text();p=p.replace('namespace Ashfall.Modules.Modes','namespace Ashfall.Modules.ModesRecovery').replace('using Ashfall.Core;','using Ashfall.Core;\nusing Ashfall.Modules.Modes;').replace('PingRecord','RecoverablePingRecord').replace('PingService','RecoverablePingService').replace('CommandLedger','RecoveryPingLedger').replace('public sealed class','public sealed partial class')
assert p==(R/'unity/Assets/Ashfall/Modules/ModesRecovery/RecoverablePingService.cs').read_text()
p=(S/'inputs/CommandLedger.cs').read_text().replace('namespace Ashfall.Modules.Modes','namespace Ashfall.Modules.ModesRecovery').replace('CommandLedger','RecoveryPingLedger').replace('internal sealed class','internal sealed partial class')
assert p==(R/'unity/Assets/Ashfall/Modules/ModesRecovery/RecoveryPingLedger.cs').read_text()
(D/'source-derivation.json').write_text(json.dumps({'pingGameplayBodiesUnchanged':True,'ledgerGameplayBodiesUnchanged':True,'transformations':['namespace Modes -> ModesRecovery','add using Modes in ping source','rename PingRecord/PingService/CommandLedger','add partial modifiers'],'acceptedPingSHA256':sha(S/'inputs/PingService.cs'),'acceptedLedgerSHA256':sha(S/'inputs/CommandLedger.cs')},indent=2))
print('Exact derivation verified; root artifacts read:',len(evidence))

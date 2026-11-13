"""Seal only current owned code/tools and NEW r3 artifacts; never touch old seals."""
from pathlib import Path
import inventory as tool
from reconciliation import verify_receipt
verify_receipt(tool.read(tool.EVIDENCE/'capture-receipt.json'))
contracts=['unity/Documentation/CONTRACTS-v1.md','unity/Documentation/CONTRACTS-v2.md','unity/Documentation/COLLISION-LAYERS-v1.md','unity/Assets/Ashfall/Core/Contracts.cs','docs/agent-runs/20261104-three-chats/unity/ownership.json','docs/agent-runs/20261104-three-chats/unity/phase03-ownership.json']
tool.write(tool.EVIDENCE/'input-contracts.json',[{'path':p,'sha256':tool.sha(tool.ROOT/p)} for p in contracts])
roots=[tool.ROOT/'unity/Assets/Ashfall/Modules/Inventory',tool.ROOT/'unity/Tools/Modules/Inventory',tool.OUT,tool.EVIDENCE]
dest=tool.EVIDENCE/'candidate-manifest.json'
files=[{'path':p.relative_to(tool.ROOT).as_posix(),'bytes':p.stat().st_size,'sha256':tool.sha(p)} for root in roots for p in sorted(root.rglob('*')) if p.is_file() and p!=dest]
tool.write(dest,{'schema':1,'status':'candidate awaiting fresh independent review','baseline_reference':tool.BASELINE,'scope':'current owned code/tools and new r3 artifacts only; prior frozen artifacts untouched','files':files})
print('Sealed',len(files),'files. SHA256',tool.sha(dest))

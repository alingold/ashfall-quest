from pathlib import Path
import hashlib,json,shutil
R=Path(r'C:\Users\aling\Documents\ashfall')
O=R/'campaign-staging/20261104-three-chats/unity/ConsumableTransactionsR2'
E=R/'docs/agent-runs/20261104-three-chats/unity/ConsumableTransactionsR2'
E.mkdir(parents=True,exist_ok=True)
paths=[R/'unity/Assets/Ashfall/Core/Contracts.cs']
for m in ['HealthStates','InventoryItems','Pickups','Player','WeaponHandling']:
 paths+=list((R/'unity/Assets/Ashfall/Modules'/m).rglob('*.cs'))
for m in ['ConsumableTransactions','InventoryTransfers']:
 paths+=list((R/'unity/Assets/Ashfall/Integration'/m).rglob('*.cs'))
paths+=list((R/'unity/Tools/Integration/ConsumableTransactions').glob('*'))
paths+=list((R/'unity/Documentation').glob('*.md'))
paths+=list((R/'docs/agent-runs/20261104-three-chats/unity').glob('accepted-neutral-v*.json'))
paths += [R/('docs/'+x) for x in ['TWO-CHAT-20-AGENT-OVERRIDE.md','HILL-CLIMB-ORCHESTRATION.md','OVERNIGHT-AGENT-PLAN-2026-11-03.md','AGENT-DISPATCH-BRIEFS-2026-11-03.md','agent-runs/20261104-0020/preflight.json']]
paths += list((R/'docs/agent-runs/20261104-three-chats/unity').glob('phase0*-ownership.json'))
records=[]
for p in sorted(set(paths)):
 if not p.is_file():continue
 b=p.read_bytes();q=O/'before'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True)
 if q.exists():raise RuntimeError('immutable snapshot exists '+str(q))
 q.write_bytes(b);records.append(dict(path=str(p),snapshot=str(q),sha256=hashlib.sha256(b).hexdigest(),bytes=len(b)))
(E/'before-manifest.json').write_text(json.dumps(records,indent=2))
(E/'OWNED-CONTRACTS.md').write_text('Candidate R2: keep PreparedPublication internal API. One outstanding global Health publication lease, expires after five host seconds; conflicting gains reject Prepare. Inventory prepared consumable admit and selection use preallocated collections and pure eligibility; mixed weapon inventories fail closed pending actual prepared suspension API. Pickups calls projection before no-fail local publication. Supply/replacement remain OPEN without actual prepared resource/geometry ports. No acceptance or durability claim.\n')
print('preserved',len(records),'files')

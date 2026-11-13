from pathlib import Path
import json,hashlib,re
r=Path.cwd();d=r/'docs/agent-runs/20261104-three-chats/unity/InventoryRecoveryR1';o=r/'campaign-staging/20261104-three-chats/unity/InventoryRecoveryR1'
inputs=json.loads((d/'before.json').read_text());drift=[]
for i in inputs:
 p=r/i['path'];now=hashlib.sha256(p.read_bytes()).hexdigest();drift.append({'path':i['path'],'before':i['sha256'],'current':now,'changed':now!=i['sha256'],'owned':p.name=='FiniteAmmoReserve.cs'})
(d/'dependency-drift.json').write_text(json.dumps(drift,indent=2));print('External drift:',[v['path'] for v in drift if v['changed'] and not v['owned']])
refs=[r/'docs'/f for f in ['TWO-CHAT-20-AGENT-OVERRIDE.md','HILL-CLIMB-ORCHESTRATION.md','OVERNIGHT-AGENT-PLAN-2026-11-03.md','AGENT-DISPATCH-BRIEFS-2026-11-03.md','agent-runs/20261104-0020/preflight.json']]
refs+=list((r/'unity/Documentation').glob('*v[34].md'))
u=r/'docs/agent-runs/20261104-three-chats/unity';refs += [u/f for f in ['GAMEPLAY-CHECKPOINT-v1.md','WeaponTransferRecoveryR3/API-REQUESTS.md','WeaponTransferRecoveryR3/REPORT.md','CombatCheckpointR3/APIs.md','CombatCheckpointR3/REPORT.md','reviews/CombatCheckpointReviewR3/REPORT.md']]
records=[]
for i,p in enumerate(refs):
 b=p.read_bytes();text=b.decode('utf-8-sig',errors='replace');t=o/'context'/('%02d_'%i+p.name);t.write_bytes(b);records.append({'source':str(p),'snapshot':str(t),'sha256':hashlib.sha256(b).hexdigest(),'lines':len(text.splitlines())})
(d/'reference-reads.json').write_text(json.dumps(records,indent=2))
symbols=[]
for p in list((r/'unity/Assets/Ashfall/Integration/InventoryRecovery').glob('*.cs'))+[r/'unity/Assets/Ashfall/Modules/InventoryItems/CheckpointState.cs',r/'unity/Assets/Ashfall/Modules/WeaponHandling/CheckpointState.cs',r/'unity/Assets/Ashfall/Modules/WeaponHandling/FiniteAmmoReserve.cs']+[r/f'{base}/scripts/{file}.gd' for base in ['game','quest-r22/source'] for file in ['weapon_inventory','weapon_rack','closure_flick']]:
 b=p.read_bytes();lines=b.decode('utf-8-sig').splitlines();symbols.append({'path':str(p.relative_to(r)),'sha256':hashlib.sha256(b).hexdigest(),'symbols':[{'line':i+1,'text':line} for i,line in enumerate(lines) if re.search(r'\b(func |public |internal |private |readonly |static )',line)]})
(d/'source-symbols.json').write_text(json.dumps(symbols,indent=2));print('Recorded complete governing reads',len(records),'and source symbols',len(symbols))
# Freeze the exact compiled field inventory as evidence.
(d/'field-inventory.txt').write_bytes((o/'fields.txt').read_bytes())

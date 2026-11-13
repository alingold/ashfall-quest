from pathlib import Path
import hashlib,json,sys
R=Path(r'C:\Users\aling\Documents\ashfall');D=R/'docs/agent-runs/20261104-three-chats/unity/ClimbingBackendR5';S=R/'campaign-staging/20261104-three-chats/unity/ClimbingBackendR5'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def check(p,expected):
 p=Path(p);p=p if p.is_absolute() else R/p
 actual=sha(p) if p.is_file() else None
 return dict(path=str(p),expected=expected,actual=actual,match=actual==expected.upper())
drift=[]
for row in read(D/'before.json'):
 result=check(row['path'],row['sha256']);result['snapshotMatch']=sha(R/row['snapshot'])==row['sha256'];drift.append(result)
(D/'dependency-drift.json').write_text(json.dumps(drift,indent=2))
frozen=[]
for row in read(R/'docs/agent-runs/20261104-three-chats/unity/Climbing/r4/sealed-inputs.json'):
 frozen.append(check(row['snapshot'],row['sha256']))
(D/'frozen-r4-verification.json').write_text(json.dumps(frozen,indent=2))
accepted=[]
def walk(obj):
 if isinstance(obj,dict):
  if 'path' in obj and 'expected' in obj and isinstance(obj['expected'],str) and len(obj['expected'])==64:
   accepted.append(check(obj['path'],obj['expected']))
  for value in obj.values():walk(value)
 elif isinstance(obj,list):
  for value in obj:walk(value)
for name in ['accepted-neutral-v1.json','accepted-neutral-v2.json','accepted-neutral-v3.json']:
 walk(read(R/'docs/agent-runs/20261104-three-chats/unity'/name))
(D/'accepted-verification.json').write_text(json.dumps(accepted,indent=2))
print('Frozen r4',len(frozen),'mismatches',sum(not x['match'] for x in frozen))
print('Accepted records',len(accepted),'mismatches',sum(not x['match'] for x in accepted))
print('Current consumed drift',[(x['path'],x['snapshotMatch']) for x in drift if not x['match']])
if len(sys.argv)>1 and sys.argv[1]=='seal':
 artifacts=[]
 roots=[R/'unity/Assets/Ashfall/Modules/Climbing',R/'unity/Assets/Ashfall/Integration/ClimbingMotor',R/'unity/Tools/Integration/ClimbingMotor',S,D]
 for base in roots:
  for p in sorted(base.rglob('*')):
   if p.is_file() and p.name not in ['candidate-manifest.json','manifest-receipt.json']:
    artifacts.append(dict(path=str(p.relative_to(R)),sha256=sha(p),bytes=p.stat().st_size))
 manifest=D/'candidate-manifest.json';manifest.write_text(json.dumps(dict(status='unreviewed partial integration candidate; native and angular gates open',artifacts=artifacts),indent=2))
 (D/'manifest-receipt.json').write_text(json.dumps(dict(path=str(manifest),sha256=sha(manifest)),indent=2))
 print('Manifest',sha(manifest),'artifacts',len(artifacts))

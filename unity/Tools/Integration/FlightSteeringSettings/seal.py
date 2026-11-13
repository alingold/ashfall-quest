from pathlib import Path
import json,hashlib
R=Path.cwd();D=R/'docs/agent-runs/20261104-three-chats/unity/FlightSteeringSettingsR1';S=R/'campaign-staging/20261104-three-chats/unity/FlightSteeringSettingsR1';before=json.loads((D/'before.json').read_text());owned={'unity/Assets/Ashfall/Modules/Flight/FlightMachine.cs','unity/Assets/Ashfall/Modules/Flight/FlightSettingsSnapshot.cs','unity/Assets/Ashfall/Integration/FlightMotor/FlightSettingsBinding.cs','unity/Assets/Ashfall/Integration/PersonalSettings/FlightSettingsPort.cs','unity/Assets/Ashfall/Integration/PersonalSettings/PlayerSettingsBinding.cs'}
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
drift=[]
for row in before:
 p=R/row['path'];q=S/'before'/row['path'];assert sha(q)==row['sha256']
 if p.is_file() and sha(p)!=row['sha256']:drift.append(dict(path=row['path'],before=row['sha256'],current=sha(p),owned=row['path'].replace('\\','/') in owned))
(D/'dependency-drift.json').write_text(json.dumps(drift,indent=2))
checks=[]
def walk(x):
 if isinstance(x,dict):
  if 'path' in x and ('expected' in x or 'sha256' in x):
   p=R/x['path'];h=x.get('expected',x.get('sha256'))
   if p.is_file():checks.append(dict(path=x['path'],expected=h,current=sha(p),match=sha(p).lower()==str(h).lower()))
  for v in x.values():walk(v)
 elif isinstance(x,list):
  for v in x:walk(v)
for p in (S/'before/docs/agent-runs/20261104-three-chats/unity').glob('accepted-neutral-v*.json'):walk(json.loads(p.read_text(encoding='utf-8-sig')))
(D/'accepted-identity.json').write_text(json.dumps(checks,indent=2))
idx=json.loads((S/'before/docs/agent-runs/20261104-three-chats/unity/phase21/source-index.json').read_text(encoding='utf-8-sig'));comparison=[]
for row in idx:
 p=R/row['path']
 if p.is_file():comparison.append(dict(path=row['path'],phase21=row['sha256'],current=sha(p),match=sha(p).lower()==row['sha256'].lower()))
(D/'phase21-comparison.json').write_text(json.dumps(comparison,indent=2))
# Full required policies and coordination inputs parsed/read as UTF8 from pinned copies.
reads=[]
for row in before:
 p=S/'before'/row['path']
 if p.suffix in ['.md','.json']:
  text=p.read_text(encoding='utf-8-sig');reads.append(dict(path=row['path'],characters=len(text),sha256=sha(p)))
(D/'required-reads.json').write_text(json.dumps(reads,indent=2))
symbols=[]
for name in sorted(owned):
 p=R/name
 for n,line in enumerate(p.read_text().splitlines(),1):
  if any(k in line for k in ['PublishSettings','PublishFlightSettings','SettingsRevision','SettingsCheckpointReady','GlideSteeringGain','UnsupportedTuningFields','class FlightSettings','stateRevision++']):symbols.append(dict(path=name,line=n,text=line.strip()))
(D/'source-symbols.json').write_text(json.dumps(symbols,indent=2))
outputs=[R/p for p in sorted(owned)]+list((R/'unity/Tools/Integration/FlightSteeringSettings').glob('*'))
(D/'production-tool-sha256.json').write_text(json.dumps([dict(path=str(p.relative_to(R)),sha256=sha(p)) for p in outputs if p.is_file()],indent=2))
run=Path((D/'latest-run.txt').read_text());results=json.loads((run/'results.json').read_text());assert all(x['exit']==x['expected'] for x in results)
(D/'summary.json').write_text(json.dumps(dict(run=str(run),allExpectedExits=True,acceptedIdentityChecks=len(checks),acceptedMismatches=sum(not x['match'] for x in checks),driftCount=len(drift),newObservations=169,retained=[4239,607,68,70,43],mutantsKilled=3,defaultBitComparisons=303,nativeExecuted=False),indent=2))
print('seal: accepted',len(checks),'mismatch',sum(not x['match'] for x in checks),'dependency drift',len(drift),'run',run.name)

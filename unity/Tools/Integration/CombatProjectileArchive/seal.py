from pathlib import Path
import hashlib,json,re
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/CombatProjectileArchiveR5';D=R/'docs/agent-runs/20261104-three-chats/unity/CombatProjectileArchiveR5'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
owned=['unity/Assets/Ashfall/Modules/Combat/'+n for n in ['CheckpointState.cs','LaunchCapabilities.cs','ProjectileArchiveRecords.cs']]
before=json.loads((D/'before.json').read_text());lookup={x['path']:x for x in before}
(D/'changed-files.json').write_text(json.dumps([dict(path=x,before=lookup.get(x,{}).get('sha256'),after=sha(R/x)) for x in owned],indent=2))
rows=[]
for x in owned:
 p=R/x
 for i,line in enumerate(p.read_text().splitlines(),1):
  if any(k in line for k in ['WritePendingArchive(','ReadPendingArchive(','TrackProjectileArchive(','CapturePendingProjectileArchive(','RestorePendingProjectileArchive(','archiveQuarantine','WriteCheckpoint(','ReadCheckpoint(']):rows.append(dict(path=x,line=i,text=line.strip(),sha256=sha(p)))
(D/'source-symbols.json').write_text(json.dumps(rows,indent=2))
drift=[]
for x in before:
 if x['path'] in owned:continue
 p=R/x['path'];h=sha(p)
 if h!=x['sha256']:drift.append(dict(path=x['path'],frozen=x['sha256'],current=h))
(D/'dependency-drift.json').write_text(json.dumps(drift,indent=2))
n=(S/'final03/cold.bin').stat().st_size;p=D/'REPORT.md';p.write_text(p.read_text().replace('all 503 truncation lengths','all '+str(n)+' truncation lengths'))
allfiles=[R/x for x in owned]+[p for root in [S,D,R/'unity/Tools/Integration/CombatProjectileArchive'] for p in root.rglob('*') if p.is_file() and p.name!='SHA256-manifest.json']
(D/'SHA256-manifest.json').write_text(json.dumps([dict(path=p.relative_to(R).as_posix(),sha256=sha(p),bytes=p.stat().st_size) for p in allfiles],indent=2))
print('cold bytes',n,'drift',len(drift),'manifest',len(allfiles))

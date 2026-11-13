from pathlib import Path
import json,hashlib,re,sys
R=Path.cwd();D=R/'docs/agent-runs/20261104-three-chats/unity/CheckpointClockEvidenceR2';S=R/'campaign-staging/20261104-three-chats/unity/CheckpointClockEvidenceR2'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
rows=json.loads((D/'inputs.json').read_text());drift=[]
for r in rows:
 assert sha(S/'before'/r['path'])==r['sha256']
 p=R/r['path'];current=sha(p) if p.is_file() else None
 if current!=r['sha256']:drift.append(dict(path=r['path'],pinned=r['sha256'],current=current))
old={r['path'] for r in rows if r['path'].endswith('.cs')};new=sorted(p.relative_to(R).as_posix() for p in (R/'unity/Assets/Ashfall').rglob('*.cs') if p.relative_to(R).as_posix() not in old)
(D/'drift.json').write_text(json.dumps(dict(changed=drift,added_membership=new),indent=2))
sources=['unity/Assets/Ashfall/Modules/Sessions/CheckpointClockEvidence.cs','unity/Assets/Ashfall/Modules/HealthStates/CheckpointClockEvidence.cs','unity/Assets/Ashfall/Integration/HostRecovery/PartialOwnerEvidence.cs']
symbols=[]
for rel in sources:
 p=R/rel
 for n,line in enumerate(p.read_text().splitlines(),1):
  if re.search(r'public |internal |readonly |private ',line):symbols.append(dict(path=rel,line=n,text=line.strip()))
(D/'source-symbols.json').write_text(json.dumps(symbols,indent=2))
accepted=[]
for p in sorted((R/'docs/agent-runs/20261104-three-chats/unity').glob('accepted-neutral-v*.json')):
 b=p.read_bytes();v=json.loads(b.decode('utf-16') if b[:2] in [b'\xff\xfe',b'\xfe\xff'] else b.decode('utf-8-sig'))
 accepted.append(dict(path=p.relative_to(R).as_posix(),sha256=sha(p),top_level=list(v) if isinstance(v,dict) else 'array',preserved=sha(p)==sha(S/'before'/p.relative_to(R))))
(D/'accepted-read.json').write_text(json.dumps(accepted,indent=2))
index=R/'docs/agent-runs/20261104-three-chats/unity/phase21/source-index.json';v=json.loads(index.read_text(encoding='utf-8-sig'));(D/'coordination-read.json').write_text(json.dumps(dict(phase21_index_sha256=sha(index),type=type(v).__name__,entries=len(v)),indent=2))
tools=[Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'),Path(sys.executable),Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.dll'),Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Core.dll')]
manifest=[]
for p in [q for root in [S,D,R/'unity/Tools/Integration/CheckpointClockEvidence'] for q in root.rglob('*') if q.is_file() and q.name!='SHA256-manifest.json']+[R/p for p in sources]+tools:
 manifest.append(dict(path=str(p),sha256=sha(p),bytes=p.stat().st_size))
(D/'SHA256-manifest.json').write_text(json.dumps(manifest,indent=2));print('sealed',len(manifest),'files;',len(drift),'changed pinned paths;',len(new),'added source members');print('\n'.join(new))

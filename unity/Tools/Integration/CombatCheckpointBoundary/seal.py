from pathlib import Path
import json,hashlib,re,shutil
R=Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/CombatCheckpointBoundaryR4';D=R/'docs/agent-runs/20261104-three-chats/unity/CombatCheckpointBoundaryR4'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
admin=[R/'docs'/p for p in ['TWO-CHAT-20-AGENT-OVERRIDE.md','HILL-CLIMB-ORCHESTRATION.md','OVERNIGHT-AGENT-PLAN-2026-11-03.md','AGENT-DISPATCH-BRIEFS-2026-11-03.md','agent-runs/20261104-0020/preflight.json']]+list((R/'unity/Documentation').glob('*.md'))
U=R/'docs/agent-runs/20261104-three-chats/unity';admin+=list(U.glob('accepted-neutral-v*.json'))+[U/'GAMEPLAY-CHECKPOINT-v1.md',U/'phase21/ROOT-REPORT.md',U/'phase21/source-index.json',U/'DirectorArchiveR11/REPORT.md',U/'reviews/MatchRecoveryReviewR10/REPORT.md',U/'reviews/CombatCheckpointReviewR3/REPORT.md']
rows=[]
for p in admin:
 b=p.read_bytes();q=S/'context'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b);rows.append(dict(path=str(p.relative_to(R)),bytes=len(b),sha256=sha(p)))
(D/'administrative-inputs.json').write_text(json.dumps(rows,indent=2))
before=json.loads((D/'before.json').read_text());current=[];changed=[]
for x in before:
 p=R/x['path'];now=sha(p);owned=p.name=='CheckpointState.cs' and p.parent.name=='Combat' or p.name=='MatchCheckpointRecords.cs' or p.parent.name in ['CombatPublication','CombatCheckpointBoundary']
 row=dict(path=x['path'],before=x['sha256'],after=now,owned=owned,drift=now!=x['sha256']);current.append(row)
 if row['drift'] and owned:changed.append(row)
for p in [R/'unity/Assets/Ashfall/Integration/CombatPublication/ArchiveWire.cs']+list((R/'unity/Tools/Integration/CombatCheckpointBoundary').glob('*.*')):
 if not any(x['path']==str(p.relative_to(R)) for x in current):changed.append(dict(path=str(p.relative_to(R)),before=None,after=sha(p),owned=True))
(D/'current-one-time-comparison.json').write_text(json.dumps(current,indent=2));(D/'changed-files.json').write_text(json.dumps(changed,indent=2))
symbols=[]
for x in changed:
 p=R/x['path']
 if p.suffix=='.cs':
  for i,line in enumerate(p.read_text().splitlines(),1):
   if re.search(r'\b(public|private|internal)\b.*\(',line):symbols.append(dict(path=x['path'],line=i,text=line.strip()))
(D/'source-symbols.json').write_text(json.dumps(symbols,indent=2))
for run in ['baseline','final01','extra04','backup-probe01']:
 shutil.copyfile(S/run/'commands.json',D/(run+'-commands.json'))
(D/'compiler.json').write_text(json.dumps(dict(path=r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe',sha256=sha(Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'))),indent=2))
manifest=[dict(path=str(p.relative_to(R)),bytes=p.stat().st_size,sha256=sha(p)) for root in [S,D,R/'unity/Tools/Integration/CombatCheckpointBoundary'] for p in root.rglob('*') if p.is_file() and p.name!='SHA256-manifest.json']
(D/'SHA256-manifest.json').write_text(json.dumps(manifest,indent=2));print('sealed',len(manifest),'files; external drift',sum(x['drift'] and not x['owned'] for x in current))

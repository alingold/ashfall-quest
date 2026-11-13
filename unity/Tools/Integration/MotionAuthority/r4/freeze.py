from pathlib import Path
import hashlib,json,subprocess,re
R=Path.cwd();O=R/'campaign-staging/20261104-three-chats/unity/CertifiedMotionPublicationR4';D=R/'docs/agent-runs/20261104-three-chats/unity/CertifiedMotionPublicationR4'
O.mkdir(exist_ok=False);D.mkdir(exist_ok=False)
rows=[]
def pin(p):
 q=O/'before'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(p.read_bytes());rows.append(dict(source=str(p.relative_to(R)),snapshot=str(q),sha256=hashlib.sha256(q.read_bytes()).hexdigest()));return q
for folder in ['unity/Assets/Ashfall/Core','unity/Assets/Ashfall/Modules/Player','unity/Assets/Ashfall/Modules/Flight','unity/Assets/Ashfall/Modules/Climbing','unity/Assets/Ashfall/Integration/FlightMotor','unity/Assets/Ashfall/Integration/ClimbingMotor','unity/Assets/Ashfall/Integration/MotionAuthority','unity/Tools/Integration/MotionAuthority','unity/Documentation']:
 for p in (R/folder).rglob('*'):
  if p.is_file():pin(p)
for p in (R/'docs/agent-runs/20261104-three-chats/unity').glob('accepted-neutral-v*.json'):pin(p)
for rel in ['phase21/ROOT-REPORT.md','phase21/source-index.json','GAMEPLAY-CHECKPOINT-v1.md','MotionPreparedOwnersR3/run05-results.json','reviews/MotionPreparedReviewR3/REPORT.md','reviews/HostMotionArbitrationReviewR1/REPORT.md']:
 pin(R/'docs/agent-runs/20261104-three-chats/unity'/rel)
# Freeze actual separate DLLs and source used by preservation oracles, then rebuild.
S=R/'campaign-staging/20261104-three-chats/unity/reviews/MotionPreparedReviewR3';B=O/'preservation';B.mkdir();results=[]
for p in (S/'reproduced/compile04').glob('*.dll'):pin(p)
for name in ['RootPreservation','PadPreservation','FrameRetry']:
 src=pin(S/'independent'/(name+'.cs'));text=(S/'independent'/(name+'.rsp')).read_text()
 for old in re.findall(r'"([^"]+)"',text):
  p=Path(old)
  if p.suffix=='.dll' and str(p).startswith(str(R)):
   q=O/'before'/p.relative_to(R);text=text.replace(old,str(q))
 text=text.replace(str(S/'independent'/(name+'.cs')),str(src)).replace(str(S/'independent'/(name+'.exe')),str(B/(name+'.exe')))
 rsp=B/(name+'.rsp');rsp.write_text(text)
 for argv in [[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/noconfig','@'+str(rsp)],[str(B/(name+'.exe'))]]:
  p=subprocess.run(argv,capture_output=True,text=True);results.append(dict(argv=argv,exit=p.returncode,output=p.stdout+p.stderr));print(name,p.returncode,(p.stdout+p.stderr)[-300:])
(D/'inputs.json').write_text(json.dumps(rows,indent=2));(D/'preservation.json').write_text(json.dumps(results,indent=2))

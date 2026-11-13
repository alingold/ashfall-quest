from pathlib import Path
import subprocess,json,shutil
R=Path.cwd();O=R/'campaign-staging/20261104-three-chats/unity/CertifiedMotionPublicationR4';B=O/'preservation02';B.mkdir();results=[]
for p in (O/'before/campaign-staging/20261104-three-chats/unity/reviews/MotionPreparedReviewR3/reproduced/compile04').glob('*.dll'):shutil.copyfile(p,B/p.name)
for name in ['RootPreservation','PadPreservation','FrameRetry']:
 rsp=B/(name+'.rsp');rsp.write_text((O/'preservation'/(name+'.rsp')).read_text().replace(str(O/'preservation'/(name+'.exe')),str(B/(name+'.exe'))))
 for argv in [[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/noconfig','@'+str(rsp)],[str(B/(name+'.exe'))]]:
  p=subprocess.run(argv,capture_output=True,text=True);results.append(dict(argv=argv,exit=p.returncode,output=p.stdout+p.stderr));print(name,p.returncode,(p.stdout+p.stderr)[-450:])
(R/'docs/agent-runs/20261104-three-chats/unity/CertifiedMotionPublicationR4/preservation02.json').write_text(json.dumps(results,indent=2))

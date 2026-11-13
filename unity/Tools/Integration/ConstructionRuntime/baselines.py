import json,hashlib,subprocess,re
from pathlib import Path
root=Path.cwd();out=root/'campaign-staging/20261104-three-chats/unity/ConstructionRuntimeR2/Baselines';out.mkdir(exist_ok=False)
prior=root/'campaign-staging/20261104-three-chats/unity/reviews/ConstructionRuntime-r1'
results=[]
for folder in ['FrozenRuntime','AcceptedBuilder','AcceptedR1','AcceptedR2','AcceptedBroad','AcceptedBoundary','AcceptedWallR4','AcceptedGeometry','AcceptedAdversarialR3','AcceptedHoldoutsR4','FinalIndependent']:
 old=next((prior/folder).glob('*.rsp'));dest=out/folder;dest.mkdir();exe=dest/'gate.exe';rsp=dest/'gate.rsp';txt=old.read_text();txt=re.sub(r'/out:"[^"]+"','/out:"'+str(exe).replace('\\','/')+'"',txt);rsp.write_text(txt)
 cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];c=subprocess.run(cmd,capture_output=True,text=True);(dest/'compile.log').write_text(c.stdout+c.stderr);r={'label':folder,'command':cmd,'compileExit':c.returncode,'sourceInputs':[{'path':x,'sha256':hashlib.sha256(Path(x).read_bytes()).hexdigest()} for x in re.findall(r'^"([^"]+\.cs)"',txt,re.M)]}
 if c.returncode==0:
  t=subprocess.run([str(exe)],capture_output=True,text=True);(dest/'tests.log').write_text(t.stdout+t.stderr);r.update(testExit=t.returncode,summary=t.stdout.splitlines()[-1],binarySha256=hashlib.sha256(exe.read_bytes()).hexdigest());print(folder,r['summary'])
 results.append(r)
(out/'results.json').write_text(json.dumps(results,indent=2))
checks=[]
for ver in range(1,6):
 p=root/('docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v%d.json'%ver);d=json.loads(p.read_text(encoding='utf-8-sig'));n=[]
 def walk(x):
  if isinstance(x,dict):
   if 'path' in x and 'expected' in x and isinstance(x['expected'],str) and len(x['expected'])==64:
    q=Path(x['path']);q=q if q.is_absolute() else root/q
    actual=hashlib.sha256(q.read_bytes()).hexdigest() if q.is_file() else None;n.append({'path':str(q),'expected':x['expected'],'actual':actual,'match':actual==x['expected'].lower()})
   for v in x.values():walk(v)
  elif isinstance(x,list):
   for v in x:walk(v)
 walk(d);checks.append({'version':ver,'manifestSha256':hashlib.sha256(p.read_bytes()).hexdigest(),'entries':n});print('accepted',ver,len(n),'mismatch',sum(not c['match'] for c in n))
(out/'accepted-verification.json').write_text(json.dumps(checks,indent=2))

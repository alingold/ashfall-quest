from pathlib import Path
import hashlib,json
R=Path.cwd();D=R/'docs/agent-runs/20261104-three-chats/unity/HostEpochHealthRegistrationR2';S=R/'campaign-staging/20261104-three-chats/unity/HostEpochHealthRegistrationR2';rows=json.loads((D/'before.json').read_text());drift=[]
for x in rows:
 p=Path(x['source']);actual=hashlib.sha256(p.read_bytes()).hexdigest() if p.exists() else None
 if actual!=x['sha256']:drift.append(dict(source=str(p),expected=x['sha256'],actual=actual,owned='Integration\\HostEpochAuthority' in str(p)))
(D/'current-drift.json').write_text(json.dumps(drift,indent=2))
mutants=[]
for p in sorted((S/'verification01').glob('mutant-*/commands.json')):
 commands=json.loads(p.read_text());last=commands[-1];mutants.append(dict(name=p.parent.name,compiled=all(x['exit']==0 for x in commands if Path(x['command'][0]).name=='csc.exe'),exit=last['exit'],killed=last['exit']!=0))
mutants.append(dict(name='complete-respawn-final-guard',compiled=True,killed=True,evidence=str(S/'finalchecks02/commands.json')))
(D/'mutations.json').write_text(json.dumps(mutants,indent=2))
files=[R/'unity/Assets/Ashfall/Integration/HostEpochAuthority',R/'unity/Tools/Integration/HostEpochHealthRegistration',D,S];manifest=[]
for root in files:
 for p in sorted(root.rglob('*')):
  if p.is_file() and p.name not in ['SHA256-manifest.json','SHA256-manifest.digest.txt']:manifest.append(dict(path=str(p.relative_to(R)),bytes=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
q=D/'SHA256-manifest.json';q.write_text(json.dumps(manifest,indent=2));(D/'SHA256-manifest.digest.txt').write_text(hashlib.sha256(q.read_bytes()).hexdigest()+'\n');print('sealed',len(manifest),'files; drift',len(drift));print(mutants)

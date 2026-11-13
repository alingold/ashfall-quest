from pathlib import Path
import json,hashlib,subprocess
R=Path(r'C:\Users\aling\Documents\ashfall');E=R/'docs/agent-runs/20261104-three-chats/unity/ConsumableCompositionR3';O=R/'campaign-staging/20261104-three-chats/unity/ConsumableCompositionR3'
rows=json.loads((E/'run04/inputs.json').read_text());prod=[]
for row in rows:
 orig=Path(row['original']);p=Path(row['snapshot']);assert hashlib.sha256(p.read_bytes()).hexdigest().upper()==row['sha256']
 if 'Tools' in orig.parts:continue
 baseline=O/'baseline'/orig
 if orig.name in ['ConsumableBridge.cs','PreparedConsumables.cs','PreparedRemoval.cs']:p=baseline
 prod.append(p)
out=O/'before-repro';out.mkdir(exist_ok=False);test=R/'unity/Tools/Integration/ConsumableTransactions/BeforeSafetyR3.cs';q=out/test.name;q.write_bytes(test.read_bytes());exe=out/'BeforeSafety.exe';rsp=out/'BeforeSafety.rsp';args=['/nologo','/langversion:5','/warnaserror+','/target:exe','/out:'+str(exe)]+[str(p) for p in prod]+[str(q)];rsp.write_text('\n'.join('"'+a+'"' for a in args));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];p=subprocess.run(cmd,capture_output=True,text=True);result=dict(command=cmd,compile=p.returncode,output=p.stdout+p.stderr)
if p.returncode==0:
 t=subprocess.run([str(exe)],capture_output=True,text=True);result.update(exit=t.returncode,output=t.stdout+t.stderr)
(E/'before-repro.json').write_text(json.dumps(result,indent=2));print(result)
# Parse full accepted ledgers and attest all listed checks read-only.
checks=[]
def walk(d,v):
 if isinstance(d,dict):
  if isinstance(d.get('path'),str) and isinstance(d.get('expected'),str):
   p=Path(d['path']);p=p if p.is_absolute() else R/p
   actual=hashlib.sha256(p.read_bytes()).hexdigest().upper() if p.is_file() else None
   checks.append(dict(version=v,path=str(p),expected=d['expected'],actual=actual,match=actual==d['expected'].upper()))
  for value in d.values():walk(value,v)
 elif isinstance(d,list):
  for value in d:walk(value,v)
for v in range(1,7):walk(json.loads((E.parent/f'accepted-neutral-v{v}.json').read_text(encoding='utf-8-sig')),v)
(E/'accepted-checks.json').write_text(json.dumps(checks,indent=2));print('Accepted checks',len(checks),'mismatch',sum(not x['match'] for x in checks))
print('Current drift from phase12:',[x['original'] for x in json.loads((E/'phase12-baseline.json').read_text()) if x['drift']])
for name in ['root-validation.json','player-retry-validation.json']:
 p=E.parent/'phase12'/name;q=O/'context'/name;q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(p.read_bytes())

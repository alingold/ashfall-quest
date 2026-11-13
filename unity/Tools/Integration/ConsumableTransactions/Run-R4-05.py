from pathlib import Path
import json,hashlib,subprocess
R=Path.cwd();E=R/'docs/agent-runs/20261104-three-chats/unity/ConsumableLiveBindingsR4';O=R/'campaign-staging/20261104-three-chats/unity/ConsumableLiveBindingsR4/run05';E=E/'run05';E.mkdir(exist_ok=False);O.mkdir(exist_ok=False)
rows=json.loads((E.parent/'before.json').read_text());sources=[];inputs=[]
owned=['Integration\\ConsumableTransactions\\','Integration\\InventoryTransfers\\TransferBridge.cs','Modules\\InventoryItems\\PreparedConsumables.cs','Modules\\InventoryItems\\PreparedRemoval.cs','Modules\\Pickups\\PreparedSelection.cs']
for row in rows:
 p=Path(row['original']); s=str(p)
 # Exclude unrelated world handoff implementation and quarantined construction candidate.
 if any(x in s for x in ['WorldCustody','ConstructionCandidate','WorldState','WorldTransfers','PreparedWorld']):continue
 src=R/p if any(x in s for x in owned) else R/row['snapshot'];b=src.read_bytes();q=O/'candidate'/p;q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b);sources.append(q);inputs.append(dict(original=s,snapshot=str(q.relative_to(R)),sha256=hashlib.sha256(b).hexdigest().upper()))
# Include new owned additive sources, which have no before bytes.
for p in (R/'unity/Assets/Ashfall/Integration/ConsumableTransactions').glob('*.cs'):
 if any(x['original']==str(p.relative_to(R)) for x in inputs):continue
 b=p.read_bytes();q=O/'candidate'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b);sources.append(q);inputs.append(dict(original=str(p.relative_to(R)),snapshot=str(q.relative_to(R)),sha256=hashlib.sha256(b).hexdigest().upper()))
results=[]
for name,test in [('Library',None),('R3','TestsR3.cs'),('R4','TestsR4.cs'),('Composition','Tests.cs'),('Cold','ColdR4.cs')]:
 out=O/(name+('.exe' if test else '.dll'));args=['/nologo','/langversion:5','/warnaserror+',r'/reference:C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data\Managed\UnityEngine\UnityEngine.CoreModule.dll','/target:'+('exe' if test else 'library'),'/out:'+str(out)]+[str(p) for p in sources]
 if test:args.append(str(R/'unity/Tools/Integration/ConsumableTransactions'/test))
 rsp=O/(name+'.rsp');rsp.write_text('\n'.join('"'+a+'"' for a in args));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];p=subprocess.run(cmd,capture_output=True,text=True);log=p.stdout+p.stderr;result=dict(name=name,command=cmd,compile=p.returncode,output=log)
 if not p.returncode and test:
  commands=[[str(out)]] if name!='Cold' else [[str(out),'capture',str(O/'custody.bin')],[str(out),'restore',str(O/'custody.bin')]]
  executions=[]
  for command in commands:
   t=subprocess.run(command,capture_output=True,text=True);executions.append(dict(command=command,exit=t.returncode,output=t.stdout+t.stderr))
  result.update(executions=executions,exit=max(t['exit'] for t in executions),output=''.join(t['output'] for t in executions))
 (E/(name+'.log')).write_text(result['output']);results.append(result);print(name,result)
(E/'inputs.json').write_text(json.dumps(inputs,indent=2));(E/'results.json').write_text(json.dumps(results,indent=2))


from pathlib import Path
import json,hashlib,subprocess
R=Path.cwd();E=R/'docs/agent-runs/20261104-three-chats/unity/ConsumableLiveBindingsR4';O=R/'campaign-staging/20261104-three-chats/unity/ConsumableLiveBindingsR4'
rows=json.loads((E/'before.json').read_text());sources=[];inputs=[]
owned=['Integration\\ConsumableTransactions\\','Integration\\InventoryTransfers\\TransferBridge.cs','Modules\\InventoryItems\\PreparedConsumables.cs','Modules\\InventoryItems\\PreparedRemoval.cs','Modules\\Pickups\\PreparedSelection.cs']
for row in rows:
 p=Path(row['original']); s=str(p)
 # Exclude unrelated world handoff implementation and quarantined construction candidate.
 if any(x in s for x in ['WorldCustody','ConstructionCandidate','WorldState','WorldTransfers','PreparedWorld']):continue
 src=R/p if any(x in s for x in owned) else R/row['snapshot'];b=src.read_bytes();q=O/'candidate'/p;q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b);sources.append(q);inputs.append(dict(original=s,snapshot=str(q.relative_to(R)),sha256=hashlib.sha256(b).hexdigest().upper()))
results=[]
for name,test in [('Library',None),('R3','TestsR3.cs'),('R2','TestsR2.cs'),('Composition','Tests.cs')]:
 out=O/(name+('.exe' if test else '.dll'));args=['/nologo','/langversion:5','/warnaserror+',r'/reference:C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data\Managed\UnityEngine\UnityEngine.CoreModule.dll','/target:'+('exe' if test else 'library'),'/out:'+str(out)]+[str(p) for p in sources]
 if test:args.append(str(R/'unity/Tools/Integration/ConsumableTransactions'/test))
 rsp=O/(name+'.rsp');rsp.write_text('\n'.join('"'+a+'"' for a in args));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];p=subprocess.run(cmd,capture_output=True,text=True);log=p.stdout+p.stderr;result=dict(name=name,command=cmd,compile=p.returncode,output=log)
 if not p.returncode and test:t=subprocess.run([str(out)],capture_output=True,text=True);result.update(exit=t.returncode,output=t.stdout+t.stderr)
 (E/(name+'.log')).write_text(result['output']);results.append(result);print(name,result)
(E/'inputs.json').write_text(json.dumps(inputs,indent=2));(E/'results.json').write_text(json.dumps(results,indent=2))

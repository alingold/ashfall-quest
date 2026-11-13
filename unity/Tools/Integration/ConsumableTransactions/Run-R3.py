from pathlib import Path
import json,hashlib,subprocess,sys
R=Path(r'C:\Users\aling\Documents\ashfall'); tag=sys.argv[1];E=R/'docs/agent-runs/20261104-three-chats/unity/ConsumableCompositionR3'/tag;O=R/'campaign-staging/20261104-three-chats/unity/ConsumableCompositionR3'/tag
E.mkdir(parents=True,exist_ok=False);O.mkdir(parents=True,exist_ok=False)
idx=json.loads((R/'docs/agent-runs/20261104-three-chats/unity/phase12/source-index.json').read_text()); sources=[];rows=[]
def pin(original,p):
 b=p.read_bytes();q=O/'sources'/original;q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b);rows.append(dict(original=str(original),consumed=str(p),snapshot=str(q),sha256=hashlib.sha256(b).hexdigest().upper()));return q
for x in idx:
 orig=x['original'];p=Path(orig)
 if any(v in orig for v in ['Core\\Contracts.cs','Integration\\CombatHealthBridge.cs','Modules\\HealthStates\\','Modules\\InventoryItems\\','Modules\\Pickups\\','Modules\\Player\\','Modules\\Combat\\','Modules\\WeaponHandling\\']) or orig=='unity\\Assets\\Ashfall\\Integration\\InventoryTransfers\\Ports.cs':
  path=R/x['snapshot'];assert hashlib.sha256(path.read_bytes()).hexdigest().upper()==x['sha256'].upper()
  if p.name in ['PreparedConsumables.cs','PreparedRemoval.cs']:path=R/orig
  sources.append(pin(p,path))
for p in (R/'unity/Assets/Ashfall/Integration/ConsumableTransactions').glob('*.cs'):sources.append(pin(p.relative_to(R),p))
# All dependency drift remains excluded: these runs consume pinned phase12 versions.
results=[]
for name,test in [('DefineOFF',None),('R3','unity/Tools/Integration/ConsumableTransactions/TestsR3.cs'),('R2','unity/Tools/Integration/ConsumableTransactions/TestsR2.cs'),('Composition','unity/Tools/Integration/ConsumableTransactions/Tests.cs'),('Health','unity/Tools/Modules/HealthStates/HealthTests.cs'),('Combat','unity/Tools/Integration/CombatHealthBridgeTests.cs'),('Inventory','unity/Tools/Modules/InventoryItems/InventoryTests.cs'),('InventoryTransactions','unity/Tools/Modules/InventoryItems/TransactionTests.cs'),('Pickups','unity/Tools/Modules/Pickups/Tests.cs'),('Batch','unity/Tools/Modules/Pickups/BatchTests.cs'),('PickupsR4','unity/Tools/Modules/Pickups/r4/TestsR4.cs')]:
 if test and not (R/test).exists():continue
 out=O/(name+('.exe' if test else '.dll'));args=['/nologo','/langversion:5','/warnaserror+',r'/reference:C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data\Managed\UnityEngine\UnityEngine.CoreModule.dll','/target:'+('exe' if test else 'library'),'/out:'+str(out)]+[str(p) for p in sources]+([str(pin(Path(test),R/test))] if test else [])
 rsp=O/(name+'.rsp');rsp.write_text('\n'.join('"'+a+'"' for a in args));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];p=subprocess.run(cmd,capture_output=True,text=True);log=p.stdout+p.stderr;(E/(name+'-compile.log')).write_text(log);result=dict(name=name,command=cmd,compile=p.returncode,output=log)
 if not p.returncode and test:
  t=subprocess.run([str(out)],capture_output=True,text=True);(E/(name+'-test.log')).write_text(t.stdout+t.stderr);result.update(exit=t.returncode,output=t.stdout+t.stderr)
 results.append(result);print(name,'compile',result['compile'],'exit',result.get('exit'),'tail',result['output'][-240:])
(E/'inputs.json').write_text(json.dumps(rows,indent=2));(E/'results.json').write_text(json.dumps(results,indent=2))

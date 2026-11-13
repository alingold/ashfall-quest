from pathlib import Path
import hashlib,json,subprocess,sys
R=Path(r'C:\Users\aling\Documents\ashfall'); tag=sys.argv[1]
O=R/'campaign-staging/20261104-three-chats/unity/ConsumableTransactionsR2'/tag
E=R/'docs/agent-runs/20261104-three-chats/unity/ConsumableTransactionsR2'/tag
O.mkdir(parents=True,exist_ok=False);E.mkdir(parents=True,exist_ok=False)
sources=[R/'unity/Assets/Ashfall/Core/Contracts.cs',R/'unity/Assets/Ashfall/Integration/CombatHealthBridge.cs']
for m in ['HealthStates','InventoryItems','Pickups','Player','Combat']:sources+=list((R/'unity/Assets/Ashfall/Modules'/m).rglob('*.cs'))
sources+=list((R/'unity/Assets/Ashfall/Integration/ConsumableTransactions').glob('*.cs'))
records=[]
def pin(p):
 q=O/'sources'/p.relative_to(R)
 if not q.exists():
  b=p.read_bytes();q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b);records.append(dict(path=str(p),snapshot=str(q),sha256=hashlib.sha256(b).hexdigest()))
 return q
prod=[pin(p) for p in sources];results=[]
def run(name,test=None):
 out=O/(name+('.exe' if test else '.dll'));rsp=O/(name+'.rsp')
 args=['/nologo','/langversion:5','/warnaserror+','/target:'+('exe' if test else 'library'),'/out:'+str(out)]+[str(p) for p in prod]+([str(pin(R/test))] if test else [])
 rsp.write_text('\n'.join('"'+a+'"' for a in args));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)]
 p=subprocess.run(cmd,capture_output=True,text=True);(E/(name+'-compile.log')).write_text(p.stdout+p.stderr);r=dict(name=name,command=cmd,compileExit=p.returncode)
 if not p.returncode and test:
  t=subprocess.run([str(out)],capture_output=True,text=True);(E/(name+'-test.log')).write_text(t.stdout+t.stderr);r.update(testExit=t.returncode,output=t.stdout+t.stderr)
 results.append(r);print(name,json.dumps(r)[-1200:])
run('DefineOFF')
run('ProducerAdoptionOPEN','unity/Tools/Integration/ConsumableTransactions/ProducerOpenR2.cs')
for name,path in [('R2','unity/Tools/Integration/ConsumableTransactions/TestsR2.cs'),('Composition152','unity/Tools/Integration/ConsumableTransactions/Tests.cs'),('Health49','unity/Tools/Modules/HealthStates/HealthTests.cs'),('Combat131','unity/Tools/Integration/CombatHealthBridgeTests.cs'),('Inventory','unity/Tools/Modules/InventoryItems/InventoryTests.cs'),('InventoryTransactions','unity/Tools/Modules/InventoryItems/TransactionTests.cs'),('Pickups','unity/Tools/Modules/Pickups/Tests.cs'),('PickupsBatch','unity/Tools/Modules/Pickups/BatchTests.cs'),('PickupsR4','unity/Tools/Modules/Pickups/r4/TestsR4.cs')]:run(name,path)
(E/'inputs.json').write_text(json.dumps(records,indent=2));(E/'results.json').write_text(json.dumps(results,indent=2))

from pathlib import Path
import json,subprocess,hashlib,sys
R=Path.cwd();E=R/'docs/agent-runs/20261104-three-chats/unity/ConsumableCustodyRecoveryR5';O=R/'campaign-staging/20261104-three-chats/unity/ConsumableCustodyRecoveryR5'
run=sys.argv[1];D=O/run;D.mkdir();logs=E/run;logs.mkdir();rows=[]
# Nonowned bytes come exclusively from the initial before freeze, which includes root phase18 repairs.
before=json.loads((E/'before.json').read_text())
owned=['Modules/Pickups/','Integration/ConsumableTransactions/','Integration/InventoryTransfers/TransferBridge.cs']
for row in before:
 p=Path(row['path']);key=p.as_posix();src=R/p if any(x in key for x in owned) else R/row['snapshot'];q=D/'src'/p;q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(src.read_bytes());rows.append(dict(path=str(p),snapshot=str(q.relative_to(R)),sha256=hashlib.sha256(q.read_bytes()).hexdigest().upper()))
for folder in ['unity/Assets/Ashfall/Modules/Pickups','unity/Assets/Ashfall/Integration/ConsumableTransactions','unity/Tools/Integration/ConsumableTransactions','unity/Tools/Integration/InventoryRecovery']:
 for p in (R/folder).glob('*.*'):
  if any(row['path']==str(p.relative_to(R)) for row in rows):continue
  q=D/'src'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(p.read_bytes());rows.append(dict(path=str(p.relative_to(R)),snapshot=str(q.relative_to(R)),sha256=hashlib.sha256(q.read_bytes()).hexdigest().upper()))
(logs/'inputs.json').write_text(json.dumps(rows,indent=2));results=[];dlls=[]
def build(name,sources,refs,exe=False,args=None):
 out=D/(name+('.exe' if exe else '.dll'));flags=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:'+('exe' if exe else 'library'),'/out:'+str(out)]+['/reference:'+str(x) for x in refs]+[str(x) for x in sources];rsp=D/(name+'.rsp');rsp.write_text('\n'.join('"'+f+'"' for f in flags));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];p=subprocess.run(cmd,capture_output=True,text=True);row=dict(name=name,command=cmd,compile=p.returncode,output=p.stdout+p.stderr)
 if not p.returncode and exe:
  runs=[]
  for extra in args or [[]]:
   c=[str(out)]+extra;t=subprocess.run(c,capture_output=True,text=True);runs.append(dict(command=c,exit=t.returncode,output=t.stdout+t.stderr))
  row['executions']=runs
 (logs/(name+'.json')).write_text(json.dumps(row,indent=2));results.append(row);print(name,row)
 if p.returncode: (logs/'results.json').write_text(json.dumps(results,indent=2));sys.exit(1)
 return out
A=D/'src/unity/Assets/Ashfall';T=D/'src/unity/Tools'
modules={}
for name,folder,deps in [('Core','Core',[]),('RecoveryContracts','RecoveryContracts',[]),('Inventory','Modules/InventoryItems',['Core','RecoveryContracts']),('Weapon','Modules/WeaponHandling',['Core','RecoveryContracts']),('Pickups','Modules/Pickups',['Core','RecoveryContracts']),('Health','Modules/HealthStates',['Core','RecoveryContracts']),('Player','Modules/Player',['Core']),('Sessions','Modules/Sessions',['Core','RecoveryContracts','Health'])]:
 result=build(name,list((A/folder).rglob('*.cs')),[modules[x] for x in deps]);modules[name]=result;dlls.append(result)
transfers=build('Transfers',list((A/'Integration/InventoryTransfers').glob('*.cs')),dlls);dlls.append(transfers)
recovery=build('Recovery',list((A/'Integration/InventoryRecovery').glob('*.cs')),dlls);dlls.append(recovery)
composition=build('Consumables',list((A/'Integration/ConsumableTransactions').glob('*.cs')),dlls);dlls.append(composition)
build('Reservations',[T/'Integration/InventoryRecovery/ReservationTests.cs'],dlls,True)
for name,path in [('PickupOriginal','Modules/Pickups/Tests.cs'),('PickupBatch','Modules/Pickups/BatchTests.cs'),('PickupR4','Modules/Pickups/r4/TestsR4.cs'),('R3','Integration/ConsumableTransactions/TestsR3.cs'),('R4','Integration/ConsumableTransactions/TestsR4.cs'),('Composition','Integration/ConsumableTransactions/Tests.cs')]:
 build(name,[T/path],dlls,True)
if (T/'Integration/ConsumableTransactions/TestsR5.cs').exists():
 base=D/'TestsR5Base.cs';text=(T/'Integration/ConsumableTransactions/TestsR4.cs').read_text().replace('sealed class TestsR4','sealed partial class TestsR5').replace('TestsR4','TestsR5').replace('static void Main()','static void RetainedMain()')
 text=text.replace('public bool denied,','public Action onValid,onActorRead;\n public bool denied,').replace('get{return !aborted&&host.valid;}','get{var hook=host.onValid;host.onValid=null;if(hook!=null)hook();return !aborted&&host.valid;}').replace('var h=health.Read(actor);frame=', 'var hook=onActorRead;onActorRead=null;if(hook!=null)hook();var h=health.Read(actor);frame=')
 text=text.replace('Desktop=true,Forward=', 'Desktop=!xr,LocalHand=sample,Forward=')
 base.write_text(text)
 build('R5',[base,T/'Integration/ConsumableTransactions/TestsR5.cs'],dlls,True,[['capture',str(D/'pickup.bin')],['restore',str(D/'pickup.bin')]])
 cold=D/'ColdR4Lower.cs';cold.write_text('using Ashfall.RecoveryContracts;\n'+(T/'Integration/ConsumableTransactions/ColdR4.cs').read_text())
 build('ColdInventory',[cold],dlls,True,[['capture',str(D/'inventory.bin')],['restore',str(D/'inventory.bin')]])
(logs/'results.json').write_text(json.dumps(results,indent=2))

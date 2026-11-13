from pathlib import Path
import json,subprocess
r=Path.cwd();base=r/'campaign-staging/20261104-three-chats/unity/InventoryRecoveryR1';out=base/'fieldguards01';out.mkdir();docs=r/'docs/agent-runs/20261104-three-chats/unity/InventoryRecoveryR1';records=json.loads((docs/'final01/inputs.json').read_text());sources=[Path(x['snapshot']) for x in records];results=[]
probe=out/'Probe.cs';probe.write_text('''using System;using System.IO;using Ashfall.Core;using Ashfall.Modules.InventoryItems;using Ashfall.Modules.WeaponHandling;using Ashfall.Integration.InventoryRecovery;class Probe{sealed class Gate:IAuthorityGate{public RuleResult Validate(CommandContext c){return RuleResult.Accept();}}sealed class Boundary:IInventoryBoundary{public RuleResult Validate(CommandContext c,InventoryOperation o,ItemInstance i,ItemInstance[] a){return RuleResult.Accept();}}static void Main(){var cat=SourceCatalog.Create();var inv=new Inventory(cat,new Boundary(),"s","a",0,false);var w=new WeaponHandling("s","a",0,WeaponMechanism.ServiceRifle,new FiniteAmmoReserve(10,5),new Gate());try{ColdInventoryCheckpoint.Capture(new CheckpointBinding("b",ColdInventoryCheckpoint.CatalogFingerprint(cat),"s","a",0,0,0,1),cat,inv,new[]{new WeaponCheckpointMember("w","p",w)});}catch(InvalidDataException){Console.WriteLine("PASS new unaccounted field fails closed");return;}throw new Exception("unaccounted field admitted");}}''')
csc=r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
for target,marker in [('Inventory.cs','public sealed partial class Inventory'),('WeaponHandling.cs','public sealed partial class WeaponHandling'),('FiniteAmmoReserve.cs','public sealed class FiniteAmmoReserve : IRevisionedAmmoReserve'),('ClosureFlick.cs','internal sealed class ClosureFlick')]:
 folder=out/target;folder.mkdir();paths=[]
 for original in sources:
  text=original.read_text()
  if original.name.endswith('_'+target):
   pos=text.index('{',text.index(marker))+1;text=text[:pos]+'\n private int unaccountedCheckpointField=7; public int ProbeField {get{return unaccountedCheckpointField;}}\n'+text[pos:]
  p=folder/original.name;p.write_text(text);paths.append(p)
 exe=folder/'Probe.exe';rsp=folder/'compile.rsp';rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:"'+str(exe)+'"']+['"'+str(p)+'"' for p in paths+[probe]]))
 for cmd in [[csc,'@'+str(rsp)],[str(exe)]]:
  result=subprocess.run(cmd,capture_output=True,text=True);results.append({'command':cmd,'exit':result.returncode,'stdout':result.stdout,'stderr':result.stderr});(docs/'fieldguards01-results.json').write_text(json.dumps(results,indent=2));print(target,result.returncode,result.stdout);result.check_returncode()

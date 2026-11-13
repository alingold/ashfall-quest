from pathlib import Path
import hashlib,json,subprocess,shutil,sys
R=Path.cwd(); label=sys.argv[1];assert label.isalnum()
S=R/'campaign-staging/20261104-three-chats/unity/WorldTransferRecoveryR5'/label;S.mkdir(exist_ok=False)
D=R/'docs/agent-runs/20261104-three-chats/unity/WorldTransferRecoveryR5'; logs=[];manifest=[]
def copy(p):
 q=S/'inputs'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(p.read_bytes() if 'WorldWeaponTransfers' in p.parts else (R/'campaign-staging/20261104-three-chats/unity/WorldTransferRecoveryR5/before'/p.relative_to(R)).read_bytes());manifest.append(dict(original=str(p.relative_to(R)),snapshot=str(q.relative_to(R)),sha256=hashlib.sha256(q.read_bytes()).hexdigest(),bytes=q.stat().st_size));return q
base=R/'unity/Assets/Ashfall'
groups={}
for folder in ['Core','RecoveryContracts','Modules/WeaponHandling','Modules/InventoryItems','Modules/Pickups','Integration/InventoryRecovery','Integration/WorldWeaponTransfers']:
 groups[folder]=[copy(p) for p in sorted((base/folder).rglob('*.cs')) if 'Engine' not in p.parts]
# The deliberate cross-assembly SessionWeapons surface uses only these four actual files.
groups['Integration/InventoryTransfers']=[copy(p) for p in sorted((base/'Integration/InventoryTransfers').glob('*.cs'))]
world=copy(R/'unity/Tools/Integration/WorldWeaponTransfers/WorldTests.cs')
(D/(label+'-inputs.json')).write_text(json.dumps(manifest,indent=2))
def execute(cmd,expect=0):
 c=subprocess.run([str(x) for x in cmd],capture_output=True,text=True);logs.append(dict(command=[str(x) for x in cmd],exit=c.returncode,output=c.stdout+c.stderr));(D/(label+'-commands.json')).write_text(json.dumps(logs,indent=2));print(c.stdout+c.stderr);assert c.returncode==expect;return c
def build(name,files,refs=(),exe=False):
 out=S/(name+('.exe' if exe else '.dll'));rsp=S/(name+'.rsp');args=['/nologo','/langversion:5','/warnaserror+','/target:'+('exe' if exe else 'library'),'/out:'+str(out),'/reference:C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.dll','/reference:C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.Core.dll']+['/reference:'+str(p) for p in refs]+[str(p) for p in files];rsp.write_text('\n'.join('"'+a+'"' for a in args));execute([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/noconfig','@'+str(rsp)]);return out
core=build('Ashfall.Core',groups['Core']); wire=build('Ashfall.RecoveryContracts',groups['RecoveryContracts'],[core]);weapon=build('Ashfall.WeaponHandling',groups['Modules/WeaponHandling'],[core,wire]);inventory=build('Ashfall.InventoryItems',groups['Modules/InventoryItems'],[core,wire]);pickups=build('Ashfall.Pickups',groups['Modules/Pickups'],[core,wire,inventory]);recovery=build('Ashfall.InventoryRecovery',groups['Integration/InventoryRecovery'],[core,wire,weapon,inventory]);worlddll=build('Ashfall.CustodyIntegration',groups['Integration/InventoryTransfers']+groups['Integration/WorldWeaponTransfers'],[core,wire,weapon,inventory,pickups]);refs=[core,wire,weapon,inventory,pickups,recovery,worlddll]

raw=world.read_text();raw=raw.replace('static void Main(){Run();}','static string[] args;static void Main(string[] a){args=a;Run();}')
at='  var clock=new Clock();'
# reader branches before any original fixture grant
reader="""
  if(args.Length>0&&args[0]=="read"){
   var host=new Host();var binding=new Ashfall.RecoveryContracts.CheckpointBinding("fixture-build","fixture-catalog","s","b",2,2,2,2);
   var image=System.IO.File.ReadAllBytes(args[1]+".checkpoint");var prefix=System.IO.File.ReadAllBytes(args[1]+".journal");
   var restored=WorldWeaponCoordinator.RestoreWorldCheckpoint(image,prefix,binding,PairCatalog(),host,host,host);
   A(restored.Inventory.Items.Length==0&&restored.History.Records.Length==2,"cold exact owners");
   A(restored.History.Sequence==3&&restored.History.Pending.Length==2&&restored.History.Pending[0].Sequence==2&&restored.History.Pending[1].Sequence==3,"sparse ACK continuity");
   foreach(var record in restored.History.Records){
    A(record.Placement.BodyId=="body:"+record.Item.InstanceId&&record.Placement.SemanticOwner=="semantic:"+record.Item.InstanceId,"exact body semantic");
    A(record.Placement.Position.X==(record.Item.InstanceId=="one"?0:1)&&record.Placement.Position.Y==0&&record.Placement.Position.Z==0,"exact position");
    A(record.Actor=="b"&&record.Epoch==2&&record.Generation==3,"exact generation");
    A(!restored.Weapons.IsActive(record.Item,"b",2),"world is never active carried");
   }
   A(restored.Weapons.Inspect("one").Loaded==2&&restored.Weapons.Inspect("one").SpentChamber&&!restored.Weapons.Inspect("one").Chambered,"exact spent chamber");
   int ammo;long rev;A(restored.Weapons.TryReadCustodyReserve("one",out ammo,out rev)&&ammo==11,"exact finite reserve");
   A(!restored.Weapons.Grant(new CommandContext("s","b","forged-grant",100,2,true),new ItemInstance("one","longwatch",1),WeaponMechanism.PrecisionRifle,3,new FiniteAmmoReserve(20,20)).Accepted,"registry prevents second grant");
   A(!restored.Weapons.Capture(new CommandContext("s","b","forged-capture",100,2,true),"one").Accepted,"world quarantine prevents legacy theft");
   bool denied=false;try{WorldWeaponCoordinator.RestoreWorldCheckpoint(image,new byte[0],binding,PairCatalog(),host,host,host);}catch(System.IO.InvalidDataException){denied=true;}A(denied,"wrong journal anchor denied");
   denied=false;var wrong=new Ashfall.RecoveryContracts.CheckpointBinding("fixture-build","wrong-catalog","s","b",2,2,2,2);try{WorldWeaponCoordinator.RestoreWorldCheckpoint(image,prefix,wrong,PairCatalog(),host,host,host);}catch(System.IO.InvalidDataException){denied=true;}A(denied,"mixed catalog denied");
   denied=false;var trailing=new byte[image.Length+1];Array.Copy(image,trailing,image.Length);try{WorldWeaponCoordinator.RestoreWorldCheckpoint(trailing,prefix,binding,PairCatalog(),host,host,host);}catch(System.IO.InvalidDataException){denied=true;}A(denied,"trailing bytes denied");
   Console.WriteLine("PASS "+checks+" cold world history/ACK/actual no-grant quarantine assertions");return;
  }
"""
import re
reader=re.sub(r'\bhost\b','coldHost',reader);reader=re.sub(r'\bammo\b','coldAmmo',reader)
raw=raw.replace(at,reader+at)
raw=raw.replace('var coordinator=new WorldWeaponCoordinator(lane,weapons,clock,geometry,100);','var journal=new WeaponTransferJournal(args[1]+".journal",4000000);var coordinator=new WorldWeaponCoordinator(lane,weapons,clock,geometry,100,journal);')
at='  var spent=coordinator.Records;'
insert="""
  var binding2=new Ashfall.RecoveryContracts.CheckpointBinding("fixture-build","fixture-catalog","s","b",2,2,2,2);
  var ids=new[]{new LifetimeCheckpointIdentity("one","finite-one",binding2),new LifetimeCheckpointIdentity("two","finite-two",binding2)};
  var image2=coordinator.CaptureWorldCheckpoint(binding2,ids,target);using(var output=new System.IO.FileStream(args[1]+".checkpoint",System.IO.FileMode.CreateNew,System.IO.FileAccess.Write,System.IO.FileShare.None,4096,System.IO.FileOptions.WriteThrough)){output.Write(image2,0,image2.Length);output.Flush(true);}journal.Dispose();
"""
raw=raw.replace(at,insert+at)
p=S/'ColdHistoryTests.cs';p.write_text(raw);exe=build('ColdHistoryTests',[p],refs,True);execute([exe,'write',S/'world']);execute([exe,'read',S/'world'])
cut=copy(R/'unity/Tools/Integration/WorldWeaponTransfers/HistoryCutTests.cs');exe=build('HistoryCutTests',[cut],refs,True)
for mode in ['cuts','malformed']:execute([exe,mode,S/'world.journal'])
(D/(label+'-inputs.json')).write_text(json.dumps(manifest,indent=2))

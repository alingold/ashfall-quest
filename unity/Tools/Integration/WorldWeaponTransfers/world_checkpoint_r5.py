from pathlib import Path
import hashlib,json,subprocess,shutil,sys
R=Path.cwd(); label=sys.argv[1];assert label.isalnum()
S=R/'campaign-staging/20261104-three-chats/unity/WorldTransferRecoveryR5'/label;S.mkdir(exist_ok=False)
D=R/'docs/agent-runs/20261104-three-chats/unity/WorldTransferRecoveryR5'; logs=[];manifest=[]
def copy(p):
 q=S/'inputs'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes((R/'campaign-staging/20261104-three-chats/unity/WorldTransferRecoveryR5/before'/p.relative_to(R)).read_bytes());manifest.append(dict(original=str(p.relative_to(R)),snapshot=str(q.relative_to(R)),sha256=hashlib.sha256(q.read_bytes()).hexdigest(),bytes=q.stat().st_size));return q
base=R/'unity/Assets/Ashfall'
groups={}
for folder in ['Core','RecoveryContracts','Modules/WeaponHandling','Modules/InventoryItems','Modules/Pickups','Integration/InventoryRecovery','Integration/WorldWeaponTransfers']:
 groups[folder]=[copy(p) for p in sorted((base/folder).rglob('*.cs')) if 'Engine' not in p.parts and p.name!='WorldTransferCheckpoint.cs']
# The deliberate cross-assembly SessionWeapons surface uses only these four actual files.
groups['Integration/InventoryTransfers']=[copy(p) for p in sorted((base/'Integration/InventoryTransfers').glob('*.cs'))]
test=copy(R/'unity/Tools/Integration/WorldWeaponTransfers/LifetimeCheckpointTests.cs');world=copy(R/'unity/Tools/Integration/WorldWeaponTransfers/WorldTests.cs');None
(D/(label+'-inputs.json')).write_text(json.dumps(manifest,indent=2))
def execute(cmd,expect=0):
 c=subprocess.run([str(x) for x in cmd],capture_output=True,text=True);logs.append(dict(command=[str(x) for x in cmd],exit=c.returncode,output=c.stdout+c.stderr));(D/(label+'-commands.json')).write_text(json.dumps(logs,indent=2));print(c.stdout+c.stderr);assert c.returncode==expect;return c
def build(name,files,refs=(),exe=False):
 out=S/(name+('.exe' if exe else '.dll'));rsp=S/(name+'.rsp');args=['/nologo','/langversion:5','/warnaserror+','/target:'+('exe' if exe else 'library'),'/out:'+str(out),'/reference:C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.dll','/reference:C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.Core.dll']+['/reference:'+str(p) for p in refs]+[str(p) for p in files];rsp.write_text('\n'.join('"'+a+'"' for a in args));execute([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/noconfig','@'+str(rsp)]);return out
core=build('Ashfall.Core',groups['Core']); wire=build('Ashfall.RecoveryContracts',groups['RecoveryContracts'],[core]);weapon=build('Ashfall.WeaponHandling',groups['Modules/WeaponHandling'],[core,wire]);inventory=build('Ashfall.InventoryItems',groups['Modules/InventoryItems'],[core,wire]);pickups=build('Ashfall.Pickups',groups['Modules/Pickups'],[core,wire,inventory]);recovery=build('Ashfall.InventoryRecovery',groups['Integration/InventoryRecovery'],[core,wire,weapon,inventory]);worlddll=build('Ashfall.CustodyIntegration',groups['Integration/InventoryTransfers']+groups['Integration/WorldWeaponTransfers'],[core,wire,weapon,inventory,pickups]);refs=[core,wire,weapon,inventory,pickups,recovery,worlddll]
exe=build('LifetimeTests',[test],refs,True);execute([exe]);execute([exe,'write',S/'lifetime.bin']);execute([exe,'read',S/'lifetime.bin'])
# Retain actual existing WorldTests, adding a process boundary after initial admission.
raw=world.read_text();raw=raw.replace('static void Main(){Run();}','static string[] arguments;static void Main(string[] args){arguments=args;Run();}')
raw=raw.replace('for(int n=0;n<2;n++){A(weapons.Grant','if(arguments.Length==0||arguments[0]!="read")for(int n=0;n<2;n++){A(weapons.Grant')
at='  A(source.SlotCount==1&&source.Items.Length==2,"actual whole pair fixture");'
insert='''
  var binding=new Ashfall.RecoveryContracts.CheckpointBinding("fixture-build","fixture-catalog","s","a",1,1,1,1);
  var ids=new[]{new LifetimeCheckpointIdentity("one","finite-one",binding),new LifetimeCheckpointIdentity("two","finite-two",binding)};
  if(arguments.Length>0&&arguments[0]=="write"){
   System.IO.File.WriteAllBytes(arguments[1]+".session",weapons.CaptureCustodyCheckpoint(binding,ids));
   System.IO.File.WriteAllBytes(arguments[1]+".inventory",InventoryCheckpointCodec.Capture(source,binding));
   System.IO.File.WriteAllText(arguments[1]+".serial",serial.ToString(System.Globalization.CultureInfo.InvariantCulture));
   Console.WriteLine("persisted actual carried custody/inventory before process exit; world is empty");return;
  }
  if(arguments.Length>0&&arguments[0]=="read"){
   weapons=SessionWeapons.RestoreQuarantinedCustody(System.IO.File.ReadAllBytes(arguments[1]+".session"),binding,host,host);
   source=InventoryCheckpointCodec.Restore(System.IO.File.ReadAllBytes(arguments[1]+".inventory"),PairCatalog(),host,binding);
   serial=Int32.Parse(System.IO.File.ReadAllText(arguments[1]+".serial"),System.Globalization.CultureInfo.InvariantCulture);
   A(weapons.IsActive(items[0],"a",1)&&weapons.IsActive(items[1],"a",1),"cold exact carried identity");
   // Actual reserves are privately reconstructed. Legacy test mutates its original
   // fixture reserves later for ABA, so the full legacy ABA run is kept separate.
  }
'''
assert at in raw;raw=raw.replace(at,insert+at)
# Cold continuation omits only the legacy reference-specific reserve ABA injection,
# and the final original-fixture reserve drain. Unchanged WorldTests runs separately.
start='  A(coordinator.PrepareDrop(C(clock),principal,60,source,new[]{"one","two"},out op),"reserve ABA prepare");'
end='  A(coordinator.PrepareDrop(C(clock),principal,60,source,new[]{"one","two"},out op),"drop prepare");'
i=raw.index(start);j=raw.index(end,i);raw=raw[:i]+raw[j:]
drain='  A(ammo[0].Take(100)==11&&ammo[0].Available==0&&ammo[0].Refund(100)==20&&ammo[0].Available==20,"finite bounded entitlement");'
assert drain in raw;raw=raw.replace(drain,'')
raw=raw.replace('static void Main(string[] args)', 'static int Reserve(SessionWeapons weapons,string id){int count;long version;if(!weapons.TryReadCustodyReserve(id,out count,out version))throw new Exception("missing actual reserve");return count;}static void Main(string[] args)')
raw=raw.replace('ammo[0].Available','Reserve(weapons,"one")').replace('ammo[1].Available','Reserve(weapons,"two")')
at='  var spent=coordinator.Records;'
extra='''
  var remapped=new Ashfall.RecoveryContracts.CheckpointBinding("fixture-build","fixture-catalog","s","b",2,2,2,2);
  var remappedIds=new[]{new LifetimeCheckpointIdentity("one","finite-one",remapped),new LifetimeCheckpointIdentity("two","finite-two",remapped)};
  byte[] exactWorldFlags=weapons.CaptureCustodyCheckpoint(remapped,remappedIds);
  var quarantined=SessionWeapons.RestoreQuarantinedCustody(exactWorldFlags,remapped,host,host);
  A(!quarantined.IsActive(items[0],"b",2)&&quarantined.Inspect("one").Loaded==2&&quarantined.Inspect("one").SpentChamber,"quarantine preserves world flag and spent mechanics");
  A(!quarantined.Capture(C(clock,"b",2),"one").Accepted,"world flag refuses legacy custody theft after decode");
  A(Convert.ToBase64String(exactWorldFlags)==Convert.ToBase64String(quarantined.CaptureCustodyCheckpoint(remapped,remappedIds)),"exact world/draw/sequence/mechanics roundtrip");
'''
assert at in raw;raw=raw.replace(at,extra+at)
p=S/'ColdCarriedWorldTests.cs';p.write_text(raw);cold=build('ColdCarriedWorldTests',[p],refs,True);execute([cold,'write',S/'carried']);execute([cold,'read',S/'carried']);legacy=build('UnchangedWorldTests',[world],refs,True);execute([legacy])
authority=copy(R/'campaign-staging/20261104-three-chats/unity/reviews/InventoryAuthorityReviewR4/pinned/retained-tests/204-AuthorityTests.cs')
exe=build('FinalEligibilityExpiryTests',[authority],refs,True);execute([exe])
(D/(label+'-inputs.json')).write_text(json.dumps(manifest,indent=2))
print('All results are local CPU quarantine evidence; world history adoption and crash cuts remain OPEN')


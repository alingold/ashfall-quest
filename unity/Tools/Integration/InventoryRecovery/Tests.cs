using System;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using Ashfall.Core;
using Ashfall.Modules.InventoryItems;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration.InventoryRecovery;
using Ashfall.RecoveryContracts;
using Handler=Ashfall.Modules.WeaponHandling.WeaponHandling;
class Tests
{
 static int assertions;
 static void Check(bool b,string s){if(!b)throw new Exception(s);assertions++;Console.WriteLine("PASS "+s);}
 sealed class Gate:IAuthorityGate,IWeaponLifetimeAuthority,IWeaponFireBinding {
 public RuleResult Validate(CommandContext c){return c.IsAuthority?RuleResult.Accept():RuleResult.Reject("host");}
 public RuleResult AuthorizeNew(CommandContext c,string id,WeaponMechanism m,int rounds){return Validate(c);}
 public RuleResult AuthorizeRestore(CommandContext c,string id,string actor,long epoch){return Validate(c);}
 public RuleResult Validate(CommandContext c,string id,long equip,long life,long conn,long frame){return Validate(c);}
 }
 sealed class Boundary:IInventoryTransactions {
 public RuleResult Validate(CommandContext c,InventoryOperation o,ItemInstance i,ItemInstance[] outgoing){return RuleResult.Accept();}
 public RuleResult Stage(CommandContext c,InventoryChange change,out IInventoryReceipt receipt){receipt=new Receipt();return RuleResult.Accept();}
 sealed class Receipt:IInventoryReceipt{public RuleResult Commit(){return RuleResult.Accept();}public void Abort(){}}
 }
 sealed class ExternalReserve:IRevisionedAmmoReserve{public int Available{get{return 1;}}public long Revision{get{return 0;}}public int Take(int n){return 0;}public int Refund(int n){return 0;}}
 static readonly Gate gate=new Gate();static readonly Boundary boundary=new Boundary();static readonly ItemCatalog catalog=SourceCatalog.Create();
 static CheckpointBinding Bind(string build,long sequence){return new CheckpointBinding(build,ColdInventoryCheckpoint.CatalogFingerprint(catalog),"session","owner",2,3,4,sequence);}
 static CommandContext C(string id,long tick){return new CommandContext("session","owner",id,tick,2,true);}
 static HandlingInput Input(long seq,bool trigger){return new HandlingInput{Sequence=seq,Enabled=true,PrimaryTracked=true,SupportTracked=true,Desktop=true,Trigger=trigger,DeltaSeconds=.25f};}
 static object Field(object o,string s){return o.GetType().GetField(s,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(o);}
 static void Equal(byte[] a,byte[] b,string s){Check(Convert.ToBase64String(a)==Convert.ToBase64String(b),s);}
 static void Refuse(byte[] b,CheckpointBinding binding,string label){QuarantinedInventory n;Check(!ColdInventoryCheckpoint.TryRestore(b,new TrustedCheckpointReceipt(binding,ColdInventoryCheckpoint.Digest(b),7),catalog,boundary,gate,out n)&&n==null,label);}
 static byte[] Patch(byte[] b,int offset,int value){byte[] n=(byte[])b.Clone();Array.Copy(BitConverter.GetBytes(value),0,n,offset,4);return n;}
 static void MalformedIdentity(byte[] bytes,CheckpointBinding binding){
 using(var q=new BinaryReader(new MemoryStream(bytes))){q.ReadInt32();q.ReadInt32();q.ReadBytes(q.ReadInt32());for(int z=0;z<4;z++)q.ReadBytes(q.ReadInt32());for(int z=0;z<4;z++)q.ReadInt64();int lengthOffset=(int)q.BaseStream.Position;int length=q.ReadInt32();q.ReadInt32();q.ReadInt32();q.ReadInt32();q.ReadBytes(q.ReadInt32());q.ReadBytes(2);q.ReadBytes(24);Check(q.ReadInt32()==2,"fixture two known identities");int start=(int)q.BaseStream.Position;byte[] utf8=(byte[])bytes.Clone();utf8[start+4]=255;Refuse(utf8,binding,"invalid UTF8 authenticated fixture");for(int k=0;k<2;k++)q.ReadBytes(q.ReadInt32());q.ReadInt32();int second=(int)q.BaseStream.Position;for(int k=0;k<2;k++)q.ReadBytes(q.ReadInt32());q.ReadInt32();int end=(int)q.BaseStream.Position;byte[] dup=new byte[bytes.Length-(end-second)+(second-start)];Array.Copy(bytes,0,dup,0,second);Array.Copy(bytes,start,dup,second,second-start);Array.Copy(bytes,end,dup,second+second-start,bytes.Length-end);Array.Copy(BitConverter.GetBytes(length-(end-second)+(second-start)),0,dup,lengthOffset,4);Refuse(dup,binding,"duplicate known identity authenticated fixture");}
 }
 static void Main(string[] args)
 {
  if(args.Length==3){Child(args[0],args[1],args[2]);return;}
  string folder=args[0];
  var unsupported=new Handler("session","owner",2,WeaponMechanism.ServiceRifle,new ExternalReserve(),gate);
  bool externalRefused=false;try{ColdInventoryCheckpoint.Capture(Bind("pinned-build",7),catalog,new Inventory(catalog,boundary,"session","owner",2,false),new[]{new WeaponCheckpointMember("external","external",unsupported)});}catch(InvalidDataException){externalRefused=true;}Check(externalRefused,"generic external reserve refused");
  foreach(WeaponMechanism mechanism in Enum.GetValues(typeof(WeaponMechanism))){
   string id=mechanism.ToString();var inv=new Inventory(catalog,boundary,"session","owner",2,false);
   string definition=mechanism==WeaponMechanism.PrecisionRifle?"longwatch":mechanism==WeaponMechanism.MagnumRevolver?"cairn6":"kestrel";
   Check(inv.Apply(C("admit",1),InventoryOperation.Admit,new ItemInstance(id,definition,1),null).Accepted,"actual admit "+id);
   for(int i=2;i<300;i++)Check(inv.Apply(C("holster"+i,i),InventoryOperation.Holster,null,null).Accepted,"retained inventory command "+i);
   Check(inv.Apply(C("knownadmit",300),InventoryOperation.Admit,new ItemInstance("known-only","banana",1),null).Accepted,"admit known identity");Check(inv.Apply(C("knownremove",301),InventoryOperation.Remove,null,"known-only").Accepted,"remove retains known identity");
   var reserve=new FiniteAmmoReserve(100,50);var ledger=new WeaponLifetimes("session",gate);Handler weapon;
   Check(ledger.CreateNew(C("grant",1),id,mechanism,3,reserve,gate,out weapon).Accepted,"actual trusted lifetime fixture");
   Check(weapon.Apply(C("fire",2),HandlingAction.Fire,Input(1,true)).Accepted,"actual initial fire");
   if(mechanism==WeaponMechanism.MagnumRevolver){
    weapon.Apply(C("open",3),HandlingAction.Eject,Input(2,false));
    for(int k=0;k<3;k++){var pose=Input(3+k,false);pose.Desktop=false;pose.DeltaSeconds=.04f;pose.HasPrimaryPose=true;pose.PrimaryForward=new Float3(0,0,1);pose.PrimaryUp=new Float3(0,1,0);weapon.Apply(C("pose"+k,4+k),HandlingAction.Sample,pose);}
    Check(((System.Collections.IList)Field(Field(weapon,"flick"),"history")).Count>0,"actual physical flick samples populated");
   }
   var binding=Bind("pinned-build",7);var members=new[]{new WeaponCheckpointMember(id,"entitlement/"+id,weapon)};
   byte[] bytes=ColdInventoryCheckpoint.Capture(binding,catalog,inv,members);string digest=ColdInventoryCheckpoint.Digest(bytes);MalformedIdentity(bytes,binding);
   Refuse(Patch(bytes,4,2),binding,"unknown schema authenticated fixture");
   Refuse(Patch(bytes,bytes.Length-12,Int32.MaxValue),binding,"reserve count exceeds entitlement authenticated fixture");
   Refuse(Patch(bytes,bytes.Length-16,-1),binding,"negative entitlement authenticated fixture");
   Refuse(Patch(bytes,bytes.Length-4,-1),binding,"negative reserve revision authenticated fixture");
   using(var q=new BinaryReader(new MemoryStream(bytes))){q.ReadInt32();q.ReadInt32();q.ReadBytes(q.ReadInt32());for(int z=0;z<4;z++)q.ReadBytes(q.ReadInt32());for(int z=0;z<4;z++)q.ReadInt64();q.ReadInt32();int inventoryOffset=(int)q.BaseStream.Position;Refuse(Patch(bytes,inventoryOffset+8,7),binding,"capacity overflow authenticated fixture");q.ReadInt32();q.ReadInt32();q.ReadInt32();q.ReadBytes(q.ReadInt32());byte[] malformed=(byte[])bytes.Clone();malformed[(int)q.BaseStream.Position]=2;Refuse(malformed,binding,"noncanonical bool authenticated fixture");}
   var receipt=new TrustedCheckpointReceipt(binding,digest,7);QuarantinedInventory cold;
   Check(ColdInventoryCheckpoint.TryRestore(bytes,receipt,catalog,boundary,gate,out cold),"cold factory "+id);
   Check(!Object.ReferenceEquals(inv,cold.Inventory)&&!Object.ReferenceEquals(weapon,cold.Weapon(id))&&!Object.ReferenceEquals(reserve,cold.Reserve(id)),"fresh actual objects");
   Equal(bytes,ColdInventoryCheckpoint.Capture(binding,catalog,cold.Inventory,new[]{new WeaponCheckpointMember(id,"entitlement/"+id,cold.Weapon(id))}),"every encoded private field exact");
   Check(!cold.Inventory.Apply(C("evicted",2),InventoryOperation.Holster,null,null).Accepted,"retired tick floor survives");
   Check(!cold.Inventory.Apply(C("holster299",400),InventoryOperation.Holster,null,null).Accepted,"inventory replay retained");
   Check(!cold.Weapon(id).Apply(C("fire",400),HandlingAction.Fire,Input(400,true)).Accepted,"weapon fire replay retained");
   Check(!cold.Weapon(id).Apply(C("grant",400),HandlingAction.ReloadStep,Input(400,false)).Accepted,"old grant cannot reload");
   Check(!ColdInventoryCheckpoint.TryRestore(bytes,new TrustedCheckpointReceipt(Bind("wrong-build",7),digest,7),catalog,boundary,gate,out cold)&&cold==null,"wrong build");
   Check(!ColdInventoryCheckpoint.TryRestore(bytes,new TrustedCheckpointReceipt(Bind("pinned-build",8),digest,7),catalog,boundary,gate,out cold),"wrong generation");
   var wrong=new CheckpointBinding("pinned-build","bad-catalog","session","owner",2,3,4,7);Check(!ColdInventoryCheckpoint.TryRestore(bytes,new TrustedCheckpointReceipt(wrong,digest,7),catalog,boundary,gate,out cold),"wrong catalog");
   for(int cut=0;cut<bytes.Length;cut+=Math.Max(1,bytes.Length/19)){byte[] torn=new byte[cut];Array.Copy(bytes,torn,cut);Check(!ColdInventoryCheckpoint.TryRestore(torn,new TrustedCheckpointReceipt(binding,ColdInventoryCheckpoint.Digest(torn),7),catalog,boundary,gate,out cold)&&cold==null,"truncation "+cut);}
   byte[] trailing=new byte[bytes.Length+1];Array.Copy(bytes,trailing,bytes.Length);Check(!ColdInventoryCheckpoint.TryRestore(trailing,new TrustedCheckpointReceipt(binding,ColdInventoryCheckpoint.Digest(trailing),7),catalog,boundary,gate,out cold),"trailing bytes");
   byte[] mixed=(byte[])bytes.Clone();mixed[mixed.Length-8]^=1;Check(!ColdInventoryCheckpoint.TryRestore(mixed,receipt,catalog,boundary,gate,out cold),"mixed reserve receipt mismatch");
   Equal(bytes,ColdInventoryCheckpoint.Capture(binding,catalog,inv,members),"all rejected restores preserve original inventory mechanics reserve");
   string path=Path.Combine(folder,id+".checkpoint");File.WriteAllBytes(path,bytes);
   var p=Process.Start(new ProcessStartInfo{FileName=Assembly.GetExecutingAssembly().Location,Arguments="\""+path+"\" "+digest+" "+id,UseShellExecute=false});p.WaitForExit();Check(p.ExitCode==0,"actual cold subprocess "+id);Equal(bytes,File.ReadAllBytes(path),"cold reader leaves physical checkpoint unchanged");
   // Prepared Inventory and fire reservation barriers on the actual owners.
   Inventory.PreparedWeaponHandoff handoff;Check(inv.PrepareWeaponHandoff(C("drop",400),new[]{id},new ItemInstance[0],out handoff),"actual prepared drop");
   bool rejected=false;try{ColdInventoryCheckpoint.Capture(binding,catalog,inv,members);}catch(InvalidDataException){rejected=true;}Check(rejected,"active inventory reservation refuses capture");handoff.Abort();handoff.Abort();
   weapon.Apply(C("sample",5),HandlingAction.Sample,Input(2,false));weapon.Apply(C("cycle",6),HandlingAction.ReloadStep,Input(3,false));weapon.Apply(C("sample2",7),HandlingAction.Sample,Input(4,false));
   IPreparedWeaponFire fire;var prepare=ledger.PrepareFire(C("preparedfire",8),id,Input(5,true),1,3,4,9,gate,out fire);
   if(prepare.Accepted){rejected=false;try{ColdInventoryCheckpoint.Capture(binding,catalog,inv,members);}catch(InvalidDataException){rejected=true;}Check(rejected,"active fire reservation refuses capture");fire.Abort();}
   IPreparedWeaponSuspension suspension;Check(weapon.PrepareSuspension(out suspension).Accepted,"actual suspension prepare");Check(suspension.Validate().Accepted,"actual suspension validate");suspension.CommitValidated();byte[] suspended=ColdInventoryCheckpoint.Capture(binding,catalog,inv,members);Check(ColdInventoryCheckpoint.TryRestore(suspended,new TrustedCheckpointReceipt(binding,ColdInventoryCheckpoint.Digest(suspended),7),catalog,boundary,gate,out cold),"suspended mechanics cold reconstruction");Check(cold.Weapon(id).Snapshot.TriggerReleaseRequired&&!cold.Weapon(id).Snapshot.Supported,"suspension disarmed state preserved");
   WeaponTransfer frozen;Check(ledger.Capture(C("retiremechanics",50),id,out frozen).Accepted,"actual ledger retires original mechanics");byte[] retiredMechanics=ColdInventoryCheckpoint.Capture(binding,catalog,inv,members);Check(ColdInventoryCheckpoint.TryRestore(retiredMechanics,new TrustedCheckpointReceipt(binding,ColdInventoryCheckpoint.Digest(retiredMechanics),7),catalog,boundary,gate,out cold),"retired mechanics cold reconstruction");Check(!cold.Weapon(id).Apply(C("retiredfire",600),HandlingAction.Fire,Input(600,true)).Accepted,"retired mechanics stays retired");
   // Retired disposition is preserved without retiring any original for compensation.
   inv.RetireForHandoff();byte[] retired=ColdInventoryCheckpoint.Capture(binding,catalog,inv,members);Check(ColdInventoryCheckpoint.TryRestore(retired,new TrustedCheckpointReceipt(binding,ColdInventoryCheckpoint.Digest(retired),7),catalog,boundary,gate,out cold),"retired cold factory");Check(!cold.Inventory.Apply(C("late",500),InventoryOperation.Holster,null,null).Accepted,"retired inventory stays retired");
  }
  Console.WriteLine("TOTAL parent "+assertions);
 }
 static void Child(string path,string digest,string id)
 {
  byte[] bytes=File.ReadAllBytes(path);QuarantinedInventory cold;var binding=Bind("pinned-build",7);
  Check(ColdInventoryCheckpoint.TryRestore(bytes,new TrustedCheckpointReceipt(binding,digest,7),catalog,boundary,gate,out cold),"child actual reconstruction");
  Handler w=cold.Weapon(id);var r=cold.Reserve(id);int total=w.Snapshot.Loaded+w.Snapshot.Seated+r.Available+(int)w.Snapshot.Shots;Check(total==53,"exact rounds plus fired conservation");
  Check(!cold.Inventory.Apply(C("holster299",500),InventoryOperation.Holster,null,null).Accepted,"child old inventory command refused");Check(!w.Apply(C("fire",500),HandlingAction.Fire,Input(500,true)).Accepted,"child old fire refused");Check(!w.Apply(C("fire",500),HandlingAction.ReloadStep,Input(500,false)).Accepted,"child old reload refused");
  for(int i=0;i<15;i++){w.Apply(C("resume"+i,501+i),i%3==2?HandlingAction.Fire:HandlingAction.ReloadStep,Input(501+i,i%3==2));Check(w.Snapshot.Loaded+w.Snapshot.Seated+r.Available+(int)w.Snapshot.Shots==total,"child reload fire conservation "+i);}
  Inventory.PreparedWeaponHandoff drop;Check(cold.Inventory.PrepareWeaponHandoff(C("newdrop",600),new[]{id},new ItemInstance[0],out drop),"child new actual drop prepare");Check(drop.Validate(),"child actual drop validate");drop.CommitValidated();Check(cold.Inventory.Items.Length==0,"child actual drop publication");Check(!cold.Inventory.PrepareWeaponHandoff(C("newdrop",600),new[]{id},new ItemInstance[0],out drop),"child old drop refused");Check(w.Snapshot.Loaded+w.Snapshot.Seated+r.Available+(int)w.Snapshot.Shots==total,"child drop conserves ammo");
 }
}



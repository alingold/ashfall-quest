using System;
using Ashfall.Core;
using Ashfall.RecoveryContracts;
using Ashfall.Modules.InventoryItems;
using Ashfall.Modules.WeaponHandling;
class Tests
{
 static int checks,serial;static long tick,frame;
 static void A(bool value,string name){checks++;if(!value)throw new Exception(name);}
 static CommandContext C(){return new CommandContext("s","a","c"+(++serial),++tick,1,true);}
 static HandlingInput I(bool trigger){return new HandlingInput{Sequence=++frame,Enabled=true,PrimaryTracked=true,SupportTracked=true,Trigger=trigger};}
 static SourceLaunchGeometry G(){return new SourceLaunchGeometry("fixture-geometry",new Float3(0,2,0),new Float3(0,0,1),new Float3(1,0,0),2,9.8f,20,1);}
 // Synthetic host ports, not authenticated Session/Health provisioning.
 sealed class Host:IAuthorityGate,ISourceWeaponLifetimeAuthority,ISourceLaunchBinding,IInventoryTransactions,IInventoryReceipt
 {
  public bool Allow=true;public long Life=1,Connection=1,Equip=1;public int Calls;
  public RuleResult Validate(CommandContext c){Calls++;return Allow?RuleResult.Accept():RuleResult.Reject("denied");}
  public RuleResult AuthorizeNew(CommandContext c,string id,WeaponMechanism m,int n){return Validate(c);}
  public RuleResult AuthorizeSourceNew(CommandContext c,string id,string p,int n,int r){return Validate(c);}
  public RuleResult AuthorizeRestore(CommandContext c,string id,string actor,long epoch){return Validate(c);}
  public RuleResult Validate(CommandContext c,string id,long e,long l,long n,long f){Calls++;return Allow&&e==Equip&&l==Life&&n==Connection?RuleResult.Accept():RuleResult.Reject("generation");}
  public RuleResult ValidateSource(CommandContext c,string id,HandlingInput i,double s,SourceLaunchGeometry g){return Validate(c);}
  public RuleResult Validate(CommandContext c,InventoryOperation op,ItemInstance incoming,ItemInstance[] outgoing){return Validate(c);}
  public RuleResult Stage(CommandContext c,InventoryChange change,out IInventoryReceipt receipt){receipt=this;return Validate(c);}
  public RuleResult Commit(){return RuleResult.Accept();}public void Abort(){}
 }
 sealed class Fake:IPreparedSourceLaunch
 {public SourceLaunchDescriptor Descriptor{get;set;}public bool Committed{get{return false;}}public RuleResult Validate(long t){return RuleResult.Accept();}public void CommitValidated(){}public void Abort(){}}
 sealed class Fixture
 {
  public Host h=new Host();public WeaponLifetimes l;public WeaponHandling w,next;public Inventory inv;public ItemInstance survivor;public string profile;
  public Fixture(string profile,bool stack,bool fallback)
  {
   this.profile=profile;l=new WeaponLifetimes("s",h);inv=new Inventory(SourceCatalog.Create(),h,"s","a",1,false);
   if(fallback){A(inv.Apply(C(),InventoryOperation.Admit,new ItemInstance("other","kestrel",1),null).Accepted,"admit earlier");A(l.CreateNew(C(),"other",WeaponMechanism.ServiceRifle,7,new FiniteAmmoReserve(20,20),h,out next).Accepted,"create earlier");}
   A(inv.Apply(C(),InventoryOperation.Admit,new ItemInstance("x",profile,1),null).Accepted,"admit source");A(l.CreateSourceExplosive(C(),"x",profile,h,out w).Accepted,"create source");
   if(stack){survivor=new ItemInstance("y",profile,1);A(inv.Apply(C(),InventoryOperation.Admit,survivor,null).Accepted,"admit survivor");A(l.CreateSourceExplosive(C(),"y",profile,h,out next).Accepted,"create survivor");A(next.SampleSourceExplosive(C(),I(false),0,G()).Accepted&&!next.Snapshot.TriggerReleaseRequired,"survivor actually armed before selection");}
   A(inv.Apply(C(),InventoryOperation.Equip,null,"x").Accepted,"equip source");
  }
  public IPreparedSourceLaunch Launch(double seconds)
  {A(w.SampleSourceExplosive(C(),I(false),seconds,G()).Accepted,"rearm source");IPreparedSourceLaunch p;var c=C();A(l.PrepareSourceThrow(c,"x",new SourceThrowInput{Frame=I(true),SimulationSeconds=seconds,Geometry=G()},1,1,1,c.Tick+10,h,out p).Accepted,"prepare real launch");return p;}
  public Inventory.PreparedSourceDisposableCommit Joint(IPreparedSourceLaunch p)
  {Inventory.PreparedSourceDisposableCommit j;A(inv.PrepareSourceDisposableDepletion(l,p,1,h,out j).Accepted,"prepare joint");return j;}
 }
 static int Loaded(WeaponHandling w){SourceExplosiveSnapshot s;A(w.TryReadSourceExplosive(out s),"read source");return s.Loaded;}
 static CheckpointBinding Binding(){return new CheckpointBinding("build","catalog","s","a",1,1,1,1);}
 static bool CaptureRefuses(Inventory inv){try{InventoryCheckpointCodec.Capture(inv,Binding());return false;}catch(System.IO.InvalidDataException){return true;}}
 static void First(Fixture f)
 {var p=f.Launch(0);Inventory.PreparedSourceDisposableCommit denied;A(!f.inv.PrepareSourceDisposableDepletion(f.l,p,1,f.h,out denied).Accepted,"no first throw depletion");A(f.inv.Items.Length>=1&&Loaded(f.w)==2,"prepare unchanged");A(p.Validate(tick).Accepted,"validate first");p.CommitValidated();A(Loaded(f.w)==1,"first actual debit only");}
 static void Main()
 {
  foreach(string profile in new[]{"frag","zone"})foreach(bool fallback in new[]{false,true})
  {
   bool stack=profile=="frag";var f=new Fixture(profile,stack,fallback);First(f);var p=f.Launch(1);int count=f.inv.Items.Length;
   WeaponLifetimes.SourceDispositionReservation bad;A(!f.l.PrepareSourceDisposableSelection(new Fake{Descriptor=p.Descriptor},"",1,f.h,out bad).Accepted,"fake descriptor rejected");
   A(!new WeaponLifetimes("s",f.h).PrepareSourceDisposableSelection(p,"",1,f.h,out bad).Accepted,"foreign registry rejected");
   var j=f.Joint(p);A(Loaded(f.w)==1&&f.inv.Items.Length==count,"joint prepare reversible");
   A(CaptureRefuses(f.inv),"pending joint schema1 refuses");
   bool sourceRefuses=false;try{WeaponCheckpointCodec.Capture(f.w,Binding(),"reserve");}catch(Exception e){sourceRefuses=e is NotSupportedException||e is System.IO.InvalidDataException;}A(sourceRefuses,"source schema1 refuses");
   IPreparedWeaponTransfer tr;IPreparedWeaponSuspension su;
   A(!f.l.PrepareTransfer(C(),"x","b",2,out tr).Accepted,"launch transfer excluded");A(!f.w.PrepareSuspension(out su).Accepted,"old suspension excluded");
   if(f.next!=null){A(!f.next.PrepareSuspension(out su).Accepted,"new suspension excluded");A(!f.l.PrepareTransfer(C(),stack?"y":"other","b",2,out tr).Accepted,"new transfer excluded");}
   A(j.Validate(tick),"joint validates");p.CommitValidated();A(!p.Committed&&Loaded(f.w)==1,"raw launch cannot split joint");
   int calls=f.h.Calls;j.CommitValidated();j.CommitValidated();j.Abort();A(j.Committed&&p.Committed&&f.h.Calls==calls,"assignment commit once");
   A(Loaded(f.w)==0&&f.inv.Items.Length==count-1,"one exact member and real exhausted debit");
   A(f.inv.SelectedInstanceId==(stack?"y":fallback?"other":""),"selection survivor then insertion order");A(f.inv.Holstered==(!stack&&!fallback),"holster empty only");
   if(stack){A(Object.ReferenceEquals(f.inv.Items[f.inv.Items.Length-1],f.survivor),"survivor identity");A(Loaded(f.next)==2&&f.next.Snapshot.TriggerReleaseRequired,"survivor ammo retained and rearm");}
   if(!stack&&fallback)A(f.next.Snapshot.Loaded==7&&f.next.Snapshot.TriggerReleaseRequired,"fallback ammo retained and rearm");
  }
  foreach(string failure in new[]{"life","connection","equip","eligibility","expiry","abort","rawabort","clock","owner-cancel"})
  {
   var f=new Fixture("frag",true,false);First(f);var p=f.Launch(1);var j=f.Joint(p);A(j.Validate(tick),"initial validation");
   if(failure=="life")f.h.Life++;if(failure=="connection")f.h.Connection++;if(failure=="equip")f.h.Equip++;if(failure=="eligibility")f.h.Allow=false;
   if(failure=="owner-cancel")A(f.l.CancelSourceDisposableSelection(C(),"x").Accepted,"actual owner cancels retained validated reservation");
   else if(failure=="expiry")A(!j.Validate(p.Descriptor.ExpiresTick+1),"expired validation");
   else if(failure=="abort")j.Abort();else if(failure=="rawabort")p.Abort();
   else if(failure=="clock")A(f.inv.AdvanceConsumableReservations(new CommandContext("s","a","clock",p.Descriptor.ExpiresTick+1,1,true)),"owner clock expiry");
   else A(!j.Validate(tick),"revoked final validate");
   j.CommitValidated();A(!j.Committed&&Loaded(f.w)==1&&f.inv.Items.Length==2,"failed final validation preserves all");j.Abort();
   f.h.Allow=true;f.h.Life=f.h.Connection=f.h.Equip=1;
   if(failure!="clock"){
    IPreparedSourceLaunch retry;var d=p.Descriptor;A(f.l.PrepareSourceThrow(d.Command,"x",new SourceThrowInput{Frame=new HandlingInput{Sequence=d.After.Frame,Enabled=true,PrimaryTracked=true,SupportTracked=true,Trigger=true},SimulationSeconds=1,Geometry=d.Geometry},1,1,1,d.ExpiresTick,f.h,out retry).Accepted,"same frame envelope retry");var r=f.Joint(retry);A(r.Validate(d.Command.Tick),"retry validate");r.CommitValidated();A(r.Committed,"retry committed");
   }
  }
  {
   var f=new Fixture("frag",true,false);First(f);IPreparedWeaponTransfer t;A(f.l.PrepareTransfer(C(),"y","b",2,out t).Accepted,"transfer reserves successor first");var p=f.Launch(1);Inventory.PreparedSourceDisposableCommit j;A(!f.inv.PrepareSourceDisposableDepletion(f.l,p,1,f.h,out j).Accepted,"joint refuses reserved successor");A(Loaded(f.w)==1&&f.inv.Items.Length==2,"order refusal preserves");t.Abort();p.Abort();
  }
  {
   var f=new Fixture("frag",true,false);First(f);var bytes=InventoryCheckpointCodec.Capture(f.inv,Binding());var p=f.Launch(1);var j=f.Joint(p);j.Abort();A(RecoveryWire.Hash(bytes)==RecoveryWire.Hash(InventoryCheckpointCodec.Capture(f.inv,Binding())),"abort schema1 bytes identical");
   var restored=InventoryCheckpointCodec.Restore(bytes,SourceCatalog.Create(),f.h,Binding());A(restored.Items.Length==2&&restored.SelectedInstanceId=="x","schema1 roundtrip");
   bytes[4]=2;bool refused=false;try{InventoryCheckpointCodec.Restore(bytes,SourceCatalog.Create(),f.h,Binding());}catch(System.IO.InvalidDataException){refused=true;}A(refused,"schema2 rejects");
  }
  Console.WriteLine("SourceDisposableCommit observations "+checks);
 }
}

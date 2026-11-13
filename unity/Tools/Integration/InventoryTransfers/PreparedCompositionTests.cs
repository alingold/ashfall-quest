using System;
using Ashfall.Core;
using Ashfall.Modules.InventoryItems;
using Ashfall.Modules.Pickups;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration.InventoryTransfers;
class Adversarial
{
 static int n,checks;static long tick;
 static void A(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static CommandContext C(){return new CommandContext("s","a","ind-"+(++n),tick,1,true);}
 sealed class Host:IAuthorityGate,ITransferEligibility,IPickupActors,IInteractionQuery,IWeaponLifetimeAuthority
 {
  public bool Eligible=true,Pose=true,Probe=false,Throw=false;public TransferBridge Bridge;public int Blocked;
  public RuleResult Validate(CommandContext c){if(Throw)throw new InvalidOperationException("violating callback");if(Probe){Probe=false;if(!Bridge.Equip(C(),"gun",false).Accepted)Blocked++;if(!Bridge.ApplyWeapon(C(),"gun",HandlingAction.Fire,I(999,true)).Accepted)Blocked++;}return Eligible?RuleResult.Accept():RuleResult.Reject("denied");}
  public bool TryRead(string a,out ActorFrame f){f=new ActorFrame{Allowed=Pose,HandEligible=true,TransferSafe=true,Desktop=true,Forward=new Float3(0,0,1),MaxStamina=6};return true;}
  public bool HasLineOfSight(string a,string s,Float3 o,Float3 t,float r){return true;}
  public RuleResult AuthorizeNew(CommandContext c,string id,WeaponMechanism m,int r){return RuleResult.Accept();}
  public RuleResult AuthorizeRestore(CommandContext c,string id,string a,long e){return RuleResult.Accept();}
 }
 sealed class Reserve:IAmmoReserve{public int Available{get{return 10;}}public int Take(int x){return x;}public int Refund(int x){return x;}}
 sealed class Planner:IDropPlanner{public bool Prepare(CommandContext c,ItemInstance[] m,out IDropBatch b){b=new Batch();return m.Length==0;}sealed class Batch:IDropBatch{public EvictedInstance[] Placements{get{return new EvictedInstance[0];}}public bool StillValid{get{return true;}}public bool Commit(){return true;}public void Abort(){}}}
 sealed class F
 {
  public Host H=new Host();public TransferBridge B;public SessionWeapons W;
  public F(){tick=0;B=new TransferBridge("s",1,100,H,H,H,H,new Planner());H.Bridge=B;A(B.AddActor("a"),"actor");W=new SessionWeapons("s",100,H,H);A(B.BindWeapons(W),"bind");var item=new ItemInstance("gun","longwatch",1);A(W.Grant(C(),item,WeaponMechanism.PrecisionRifle,3,new Reserve()).Accepted,"grant");A(B.AdmitExistingWeapon(C(),item,W).Accepted,"admit");Collect("food");A(B.Equip(C(),"gun",false).Accepted,"select");At(25);A(B.ApplyWeapon(C(),"gun",HandlingAction.Sample,I(1,false)).Accepted,"release");}
  public void At(long t){tick=t;B.Advance(t);}
  public void Collect(string id){A(B.Register(id,"banana","sem-"+id,new Float3(0,0,1)),"register");A(B.Execute(C(),PickupAction.Reserve,id).Accepted,"reserve");A(B.Execute(C(),PickupAction.Collect,id).Accepted,"collect");}
  public void Prep(){A(B.Execute(C(),PickupAction.Equip,"food").Accepted,"food");for(int j=0;j<4;j++){A(B.Execute(C(),PickupAction.DesktopRelease,"food").Accepted,"release peel");A(B.Execute(C(),PickupAction.DesktopPress,"food").Accepted,"peel");}A(B.ConsumablePhase("a")==Preparation.Consume,"prepared actual banana");}
 }
 static HandlingInput I(long s,bool trigger){return new HandlingInput{Sequence=s,Enabled=true,Desktop=true,PrimaryTracked=true,SupportTracked=true,Trigger=trigger};}
 static void Main()
 {
  var mag=new F();var item=new ItemInstance("magnum","cairn6",1);A(mag.W.Grant(C(),item,WeaponMechanism.MagnumRevolver,7,new Reserve()).Accepted,"real source Magnum profile grant");A(mag.B.AdmitExistingWeapon(C(),item,mag.W).Accepted,"actual inventory Magnum admission");A(mag.B.Equip(C(),"magnum",false).Accepted,"Magnum select");mag.B.ApplyWeapon(C(),"magnum",HandlingAction.Sample,I(100,false));mag.At(50);var support=I(101,false);support.Grip=true;support.ForegripDistance=.01f;A(mag.B.ApplyWeapon(C(),"magnum",HandlingAction.Sample,support).Accepted&&mag.W.Inspect("magnum").ClaimsSupport,"actual Magnum support");mag.Prep();A(!mag.W.Inspect("magnum").ClaimsSupport&&mag.W.Inspect("magnum").TriggerReleaseRequired,"weapon food suspends actual contacts");A(mag.B.Equip(C(),"magnum",false).Accepted&&mag.B.HeldConsumable("a")==null,"food weapon clears actual preparation");mag.B.ApplyWeapon(C(),"magnum",HandlingAction.Sample,I(102,false));mag.At(75);A(mag.B.ApplyWeapon(C(),"magnum",HandlingAction.Fire,I(103,true)).Accepted&&mag.W.Inspect("magnum").Loaded==6,"source Magnum draw and conservation");
  var f=new F();A(f.B.Equip(C(),"gun",false).Accepted,"advance inventory command clock");f.Prep();var snapshot=f.B.Inventory("a");A(snapshot.SelectedInstanceId=="gun"&&!snapshot.Holstered,"projection remains old weapon, not complete holster");
  var used=C();A(f.B.Execute(used,PickupAction.DesktopRelease,"food").Accepted,"consume pickup command id");A(!f.B.Equip(used,"gun",false).Accepted,"reverse pickup replay rejects before inventory publication");A(f.B.HeldConsumable("a")=="food"&&f.B.ConsumablePhase("a")==Preparation.Consume,"reverse replay preserves preparation");
  var stale=new CommandContext("s","a","stale-reverse",24,1,true);A(!f.B.Equip(stale,"gun",false).Accepted&&f.B.HeldConsumable("a")=="food","inventory stale reverse preserves prep");
  var epoch=new CommandContext("s","a","wrong-epoch",25,2,true);A(!f.B.Equip(epoch,"gun",false).Accepted,"epoch rejected");
  f.H.Eligible=false;A(!f.B.Equip(C(),"gun",false).Accepted&&f.B.ConsumablePhase("a")==Preparation.Consume,"pure denied eligibility preserves prep");f.H.Eligible=true;
  f.H.Pose=false;A(!f.B.Equip(C(),"gun",false).Accepted,"live invalid actor rejects reverse");A(f.B.HeldConsumable("a")==null&&f.B.ConsumablePhase("a")==Preparation.Idle,"actual pickups cancels prep on live actor rejection");A(!f.W.Apply(snapshot,C(),"gun",HandlingAction.Fire,I(2,true)).Accepted,"safety cancellation retains weapon suppression");f.H.Pose=true;A(f.B.Equip(C(),"gun",false).Accepted,"fresh reverse retry succeeds");
  for(int j=0;j<3;j++){A(f.B.Equip(C(),"gun",false).Accepted,"repeated same reselect");A(!f.B.ApplyWeapon(C(),"gun",HandlingAction.Fire,I(10+j*3,true)).Accepted,"repeat needs release");f.B.ApplyWeapon(C(),"gun",HandlingAction.Sample,I(11+j*3,false));f.At(tick+24);A(!f.B.ApplyWeapon(C(),"gun",HandlingAction.Fire,I(12+j*3,true)).Accepted,"repeat .24 fails");f.At(tick+1);}
  A(f.W.Inspect("gun").Loaded==3,"repeated failed actions conserve rounds");
  f=new F();f.Prep();f.Collect("food2");A(f.B.HeldConsumable("a")==null,"actual collect stows preparation");f.W.Selection(f.B.Inventory("a"),0,false,true);A(!f.W.Apply(f.B.Inventory("a"),C(),"gun",HandlingAction.Fire,I(2,true)).Accepted,"public selection misuse cannot bypass live suppression");A(!f.B.ApplyWeapon(C(),"gun",HandlingAction.Fire,I(3,true)).Accepted,"collect does not auto redraw");A(f.B.Equip(C(),"gun",false).Accepted,"explicit redraw after collect");f.B.ApplyWeapon(C(),"gun",HandlingAction.Sample,I(4,false));f.At(50);A(f.B.ApplyWeapon(C(),"gun",HandlingAction.Fire,I(5,true)).Accepted&&f.W.Inspect("gun").Loaded==2,"explicit redraw fires exactly one round");
  f=new F();f.H.Probe=true;A(f.B.ApplyWeapon(C(),"gun",HandlingAction.Fire,I(2,true)).Accepted,"outer weapon action succeeds");A(f.H.Blocked==2&&f.W.Inspect("gun").Loaded==2,"violating same-thread callback bridge reentry rejected");Console.WriteLine("CONDITIONAL reentrant fixture violates pure nonreentrant port contract; only bridge guard characterized");
  f=new F();f.Prep();f.H.Throw=true;bool threw=false;try{f.B.Equip(C(),"gun",false);}catch(InvalidOperationException){threw=true;}f.H.Throw=false;A(threw&&f.B.HeldConsumable("a")=="food","violating throwing eligibility propagates without stow");A(f.B.Equip(C(),"gun",false).Accepted,"active guard released after violating throw");Console.WriteLine("CONDITIONAL throwing fixture violates host contract, not rollback guarantee");
  f=new F();A(f.W.Capture(C(),"gun").Accepted,"host single-lane capture remains irreversible");A(!f.B.ApplyWeapon(C(),"gun",HandlingAction.Fire,I(2,true)).Accepted,"captured handler cannot fire");A(f.B.Inventory("a").SelectedInstanceId=="gun","lifecycle does not migrate inventory");
  Console.WriteLine("PASS "+checks+" independent adversarial checks; no Unity/runtime claim");
 }
}


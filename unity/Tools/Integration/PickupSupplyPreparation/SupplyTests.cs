using System;
using Ashfall.Core;
using Ashfall.Modules.Pickups;
using Ashfall.Modules.InventoryItems;
using Ashfall.RecoveryContracts;
using Ashfall.Integration.ConstructionRuntime;
using Ashfall.Integration.ConstructionTransactions;
public static class SupplyTests
{
 public sealed class Ports:IAuthorityGate,IPickupActors,IPickupTransactions,IInteractionQuery,IPickupSupplyLifetime,IConstructionHostState
 {
  public long Clock=10,Life=1,Connection=1,Root=1,Pose=1,Input=1;public bool Allowed=true,Wall,Authority=true,Active=true;public int Effects,Calls;
  public long Tick{get{return Clock;}}
  public RuleResult Validate(CommandContext c){Calls++;return Authority?RuleResult.Accept():RuleResult.Reject("revoked");}
  public bool TryRead(string a,out ActorFrame f){Calls++;f=new ActorFrame{Allowed=Allowed,HandEligible=true,TransferSafe=true,Head=new Float3(),Hand=new Float3(),Forward=new Float3(0,0,1),MaxStamina=6};return true;}
  public bool TryRead(string a,out PickupSupplyLife l){Calls++;l=new PickupSupplyLife("lease",Life,Connection,Root,Pose,Input);return Active;}
  public bool TryRead(string a,out HostBuilderStamp s){Calls++;s=new HostBuilderStamp("s",1,a,"lease",Life,Connection,Root,Pose,Active,Input);return Active;}
  public bool HasLineOfSight(string a,string b,Float3 f,Float3 t,float r){Calls++;return !Wall;}
  public bool TryCommit(Transfer t){Effects++;return true;}public bool TryStageStore(Transfer t,out IStagedStore s){s=null;return false;}
  public bool TryPlaceDrop(string a,string b,out Float3 p){p=new Float3();return false;}public ShieldProcessing TryAddShield(string a,float f){return ShieldProcessing.Applied;}
 }
 static int count;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;}
 static CommandContext C(string cmd,long tick=10){return new CommandContext("s","a",cmd,tick,1,true);}
 static PickupRules New(Ports p,int amount=6,bool bind=true){var r=new PickupRules("s",1,60,p,p,p,p,100,10,10);Check(r.Register("box","builds","body",new Float3(0,0,1),amount),"register");r.Advance(10);Check(r.Execute(C("reserve"),PickupAction.Reserve,"box").Accepted,"reserve");if(bind)Check(r.BindSupplyLifetime(p),"bind");return r;}
 static CheckpointBinding Binding(){return new CheckpointBinding("build","catalog","s","a",1,1,1,1);}
 public static int Main(){try{
  var p=new Ports();var r=New(p);PickupRules.PreparedSupplyRemoval t;
  long rev=r.Revision;Check(r.PrepareSupplyRemoval(C("collect"),"box",rev,11,out t).Accepted,"prepare");
  var x=t.Receipt;Check(x.Box=="box"&&x.Kind=="builds"&&x.Quantity==6&&x.SemanticBody=="body"&&x.Session=="s"&&x.Actor=="a"&&x.Epoch==1&&x.Tick==10&&x.Expiry==11&&x.Revision==rev&&x.Lifetime.Life==1,"exact receipt");
  Check(!r.Inspect("box").Removed&&!r.HasRetainedCommand("a","collect")&&r.PendingSupplyCount==1,"preparation no effects");
  bool refused=false;try{PickupCheckpointCodec.Capture(r,Binding());}catch{refused=true;}Check(refused,"capture pending refuses");
  Check(!r.Execute(C("race"),PickupAction.Collect,"box").Accepted&&p.Effects==0,"competing collect reserved");
  Check(!r.Register("other","builds","body2",new Float3()),"registration refuses reservation");
  Check(t.Validate(10)&&!t.Validate(9)&&!t.Validate(11),"actual owner clock");
  t.Abort();t.Abort();Check(!t.Validate(10)&&r.PendingSupplyCount==0&&!r.Inspect("box").Removed,"abort idempotent");
  Check(PickupCheckpointCodec.Capture(r,Binding()).Length>0,"capture after abort field disposition");
  Check(r.PrepareSupplyRemoval(C("collect"),"box",r.Revision,11,out t).Accepted&&t.Validate(10),"retry exact aborted command");
  int calls=p.Calls;t.CommitValidated();t.CommitValidated();t.Abort();Check(p.Calls==calls&&p.Effects==0,"assignment-only commit");
  Check(r.Inspect("box").Removed&&r.HasRetainedCommand("a","collect")&&r.PendingSupplyCount==0,"actual removal and replay");
  Check(!r.PrepareSupplyRemoval(C("collect"),"box",r.Revision,11,out t).Accepted,"second collection rejects");
  Check(PickupCheckpointCodec.Capture(r,Binding()).Length>0,"capture committed");
  for(int k=0;k<9;k++){
   p=new Ports();r=New(p);Check(r.PrepareSupplyRemoval(C("stale"),"box",r.Revision,11,out t).Accepted,"stale prepare "+k);
   if(k==0)p.Life++;if(k==1)p.Connection++;if(k==2)p.Root++;if(k==3)p.Pose++;if(k==4)p.Input++;if(k==5)p.Allowed=false;if(k==6)p.Wall=true;if(k==7)p.Authority=false;if(k==8)p.Active=false;
   Check(!t.Validate(10)&&!r.Inspect("box").Removed,"lifetime/reach/authority revoke "+k);t.Abort();Check(r.PendingSupplyCount==0,"release revoked "+k);
  }
  p=new Ports();r=New(p);Check(r.PrepareSupplyRemoval(C("clock"),"box",r.Revision,11,out t).Accepted,"clock prepare");r.Advance(12);Check(!t.Validate(12),"advance revision expiry");t.Abort();
  p=new Ports();r=New(p,6,false);var registry=new PreparedSupplyGrant(r,p);var accounts=new BuildChargeAccounts("s",1,p,registry);var ledger=accounts.ForActor("a");BuildChargeLedger.PreparedCharge g;
  Check(!ledger.PrepareGrant(C("forge"),"fabricated","builds",6,10,out g).Accepted,"forged receipt");
  Check(registry.Collect(accounts,C("grant"),"box").Accepted,"canonical grant");Check(ledger.Balance==6&&r.Inspect("box").Removed&&p.Effects==0,"real ledger real removal");
  Check(!registry.Collect(accounts,C("grant"),"box").Accepted&&ledger.Balance==6,"retry committed receipt");
  foreach(int amount in new[]{0,3,7}){p=new Ports();r=New(p,amount,false);registry=new PreparedSupplyGrant(r,p);accounts=new BuildChargeAccounts("s",1,p,registry);Check(!registry.Collect(accounts,C("partial"),"box").Accepted,"canonical six only");Check(accounts.ForActor("a").Balance==0&&!r.Inspect("box").Removed&&r.PendingSupplyCount==0,"unsupported amount no partial effects");}
  p=new Ports();r=New(p,6,false);registry=new PreparedSupplyGrant(r,p);accounts=new BuildChargeAccounts("wrong",1,p,registry);Check(!registry.Collect(accounts,C("mismatch"),"box").Accepted&&!r.Inspect("box").Removed&&r.PendingSupplyCount==0,"ledger refusal aborts source");
  Console.WriteLine("RESULT "+count+" PASS prepared supplies external DLL consumer");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}

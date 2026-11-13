using System;
using System.IO;
using Ashfall.Core;
using Ashfall.Modules.Pickups;
using Ashfall.Modules.Sessions;
using Ashfall.RecoveryContracts;
using Ashfall.Integration.InventoryRecovery;
using Ashfall.Integration.ConsumableTransactions;
// Actual owner DLL consumers. Authority/geometry/time/provenance are local fixtures.
sealed partial class TestsR5
{
 static CheckpointBinding Binding(long sequence=23){return new CheckpointBinding("fixture-build","fixture-catalog","s","authority",1,2,3,sequence);}
 void Capture(string path)
 {
  Init();Collect("drink","soda");Collect("survivor","soda");Action(PickupAction.Equip,"drink");Action(PickupAction.DesktopRelease,"drink");Action(PickupAction.DesktopPress,"drink");
  Check(health.ApplyLoss(C(),"a",health.Read("a").Revision,0,20,false).Accepted,"shield setup");Action(PickupAction.Consume,"drink");Check(pickups.HeldInstance("a")=="survivor","first original-slot survivor");Advance(60);Check(pickups.PendingSoda("a")==45,"soda at capture");
  Collect("shake","shaker");Action(PickupAction.Equip,"shake");Action(PickupAction.DesktopRelease,"shake");Action(PickupAction.DesktopPress,"shake");Advance(120);
  Check(pickups.Register("box","rifle","canonical-box",new Float3(.125f,.25f,1.5f),7),"source quantity and position");
  var replay=new CommandContext("s","a","retained-reserve",120,1,true);Check(bridge.Execute(replay,PickupAction.Reserve,"box").Accepted,"reservation/replay seed");
  byte[] image=bridge.CapturePickup(Binding());File.WriteAllBytes(path,image);File.WriteAllText(path+".digest",RecoveryWire.Hash(image));
  Check(!bridge.Execute(replay,PickupAction.Collect,"box").Accepted,"live retained replay rejects payload change");
  Init();Collect("peel","banana");Action(PickupAction.Equip,"peel");for(int j=0;j<2;j++){Action(PickupAction.DesktopRelease,"peel");Action(PickupAction.DesktopPress,"peel");}File.WriteAllBytes(path+".peel",bridge.CapturePickup(Binding()));
  Init();Collect("physical","shaker");xr=true;Action(PickupAction.Equip,"physical");Action(PickupAction.SampleShake,"physical");Advance(1);sample=new Float3(.05f,0,0);Action(PickupAction.SampleShake,"physical");File.WriteAllBytes(path+".physical",bridge.CapturePickup(Binding()));xr=false;
  Console.WriteLine("CAPTURE "+image.Length+" bytes "+RecoveryWire.Hash(image));
 }
 void Restore(string path)
 {
  Init();byte[] image=File.ReadAllBytes(path);var receipt=new TrustedCheckpointReceipt(Binding(),File.ReadAllText(path+".digest"),23);PickupRules p;
  Check(ConsumablePickupCheckpoint.TryRestore(image,receipt,this,this,bridge,this,out p),"actual fresh owner restore");
  Check(p!=pickups&&p.IsQuarantined,"fresh quarantine");Check(RecoveryWire.Hash(PickupCheckpointCodec.Capture(p,Binding()))==receipt.Digest,"all owner state roundtrip exact bytes");
  Check(p.PendingSoda("a")==40&&p.HeldInstance("a")=="shake"&&p.Phase("a")==Preparation.Shake,"soda/preparation/time survives");
  CanonicalPickupSource box,removed;Check(p.TryReadCanonicalSource("box",out box)&&box.Kind=="rifle"&&box.Amount==7&&box.SemanticOwnerId=="canonical-box"&&box.Position.X==.125f&&box.Position.Y==.25f&&box.Position.Z==1.5f&&box.ReservedBy=="a"&&box.ReservationUntil==180,"exact canonical source/reservation");
  Check(p.TryReadCanonicalSource("drink",out removed)&&removed.Removed&&removed.Owner==null,"consumed tombstone");Check(p.HasRetainedCommand("a","retained-reserve"),"actual retained replay restored");
  Check(!p.Execute(new CommandContext("s","a","retained-reserve",120,1,true),PickupAction.Collect,"box").Accepted&&!p.Register("new","soda","new",new Float3()),"no partial gameplay adoption");
  bool refused=false;try{p.Advance(121);}catch(InvalidOperationException){refused=true;}Check(refused,"quarantine clock cannot apply effect");
  Check(!ConsumablePickupCheckpoint.TryRestore(image,new TrustedCheckpointReceipt(Binding(),new string('0',64),23),this,this,bridge,this,out p),"external digest mismatch");
  for(int i=0;i<image.Length;i++){var cut=new byte[i];Array.Copy(image,cut,i);Check(!ConsumablePickupCheckpoint.TryRestore(cut,receipt,this,this,bridge,this,out p),"truncation digest refuses");Check(!ConsumablePickupCheckpoint.TryRestore(cut,new TrustedCheckpointReceipt(Binding(),RecoveryWire.Hash(cut),23),this,this,bridge,this,out p),"parser refuses each truncation with fixture digest");}
  var altered=(byte[])image.Clone();altered[0]^=1;Check(!ConsumablePickupCheckpoint.TryRestore(altered,receipt,this,this,bridge,this,out p),"byte mutation refuses");
  var wrong=new CheckpointBinding("wrong-build","fixture-catalog","s","authority",1,2,3,23);Check(!ConsumablePickupCheckpoint.TryRestore(image,new TrustedCheckpointReceipt(wrong,receipt.Digest,23),this,this,bridge,this,out p),"binding substitution");
  refused=false;try{new TrustedCheckpointReceipt(Binding(22),receipt.Digest,23);}catch(InvalidDataException){refused=true;}Check(refused,"trusted ACK sequence floor rejects stale envelope");
  foreach(string suffix in new[]{".peel",".physical"}){byte[] variant=File.ReadAllBytes(path+suffix);var expected=new TrustedCheckpointReceipt(Binding(),RecoveryWire.Hash(variant),23);Check(ConsumablePickupCheckpoint.TryRestore(variant,expected,this,this,bridge,this,out p),"nonzero peel/physical sample restore");Check(RecoveryWire.Hash(PickupCheckpointCodec.Capture(p,Binding()))==expected.Digest,"all nonzero peel/physical sample bytes preserved");}
  Console.WriteLine("RESTORE PASS "+checks+" observations, quarantine only; no whole-effect ACK recovery");
 }
 sealed class SessionFixture:ISessionClock,IReconnectTokens,ISpawnWorld,ISessionManagement
 {
  int next;public double Now {get{return 0;}}public string Issue(){return "token-"+(++next);}public bool CanManage(CommandContext c){return true;}
  public SpawnSite[] Sites(string map,bool warmup){return new[]{new SpawnSite("fixture",new Float3(),-1,false,false)};}public bool IsSafe(string map,string actor,Float3 feet){return true;}
 }
 public bool xr;public Float3 sample;
 void LifetimeChecks()
 {
  Init();var fixture=new SessionFixture();var rules=new SessionRules("map","respawn","map-hash",0,8,false,false,true,true,0);var session=new Session("s","content",rules,this,fixture,fixture,fixture,fixture);
  Check(session.Lifecycle(new CommandContext("s","a","sessions-lifecycle/0",0,0,true),LifecycleAction.BeginLoading,rules,null,false).Accepted&&session.Epoch==1,"actual session epoch");
  string reconnect;Check(session.Join(C(),new JoinRequest(Protocol.Name,Protocol.Schema,"content","a",null,false),out reconnect).Accepted,"actual admitted session");Check(bridge.BindSession(session),"bind actual opaque session");
  bridge.BindDrops(this);Collect("food","banana");PrepareBanana("food");object old=session.GetConnectionIdentity("a");
  Check(session.Disconnect(C(),false).Accepted,"actual disconnect");Check(session.Join(C(),new JoinRequest(Protocol.Name,Protocol.Schema,"content","a",reconnect,false),out reconnect).Accepted,"actual same actor reconnect");Check(!Object.ReferenceEquals(old,session.GetConnectionIdentity("a")),"server rotates identity");
  Check(!bridge.Execute(C(),PickupAction.Consume,"food").Accepted&&!pickups.Inspect("food").Removed&&inventory.Items.Length==1,"old preparation rejected without Health generation change");
  Action(PickupAction.Equip,"food");malformed=true;Check(!bridge.Execute(C(),PickupAction.Drop,"food").Accepted&&inventory.Items.Length==1&&!pickups.Inspect("food").Removed,"forged semantic refused without drop publication");malformed=false;Action(PickupAction.Drop,"food");
  Check(inventory.Items.Length==0&&pickups.Inspect("food").Owner==null,"canonical drop succeeds");
  Collect("next","banana");Action(PickupAction.Equip,"next");int published=drops;
  onValid=delegate{Check(session.Disconnect(C(),false).Accepted,"disconnect during final placement validation");Check(session.Join(C(),new JoinRequest(Protocol.Name,Protocol.Schema,"content","a",reconnect,false),out reconnect).Accepted,"replacement during validation");};
  Check(!bridge.Execute(C(),PickupAction.Drop,"next").Accepted&&inventory.Items.Length==1&&pickups.Inspect("next").Owner=="a"&&drops==published,"actual opaque identity rechecked before removal/placement commit");
  Check(!bridge.Execute(C(),PickupAction.Consume,"next").Accepted,"invalidated preparation resets");Action(PickupAction.Equip,"next");
  onValid=delegate{var h=health.Read("a");Check(health.SetConnected(C(),"a",h.Revision,false).Accepted,"health disconnect at final validation");h=health.Read("a");Check(health.SetConnected(C(),"a",h.Revision,true).Accepted,"health reconnect at final validation");};
  Check(!bridge.Execute(C(),PickupAction.Drop,"next").Accepted&&inventory.Items.Length==1&&drops==published,"actual health connection generation rechecked");
  Ashfall.Modules.Pickups.IStagedStore leaked=null;
  onActorRead=delegate{Check(bridge.TryStageStore(new Transfer(TransferKind.Store,"a","leak","soda",new Float3(),0,0,false,0),null,out leaked),"stage actual Inventory admission");bool refusal=false;try{PickupCheckpointCodec.Capture(pickups,Binding());}catch(InvalidDataException){refusal=true;}Check(refusal,"owner capture refuses executing callback");};
  // Reserve uses actor eligibility but performs no Inventory publication.
  Check(pickups.Register("reserve","soda","owner-reserve",new Float3()),"source for leaked preparation test");bridge.Execute(C(),PickupAction.Reserve,"reserve");
  Check(leaked!=null&&!leaked.TryCommit(),"retained bridge stage cannot commit outside active lane");bool busy=false;try{bridge.CapturePickup(Binding());}catch(InvalidOperationException){busy=true;}Check(busy,"bridge capture refuses outstanding actual stage");leaked.Abort();leaked.Abort();Check(bridge.CapturePickup(Binding()).Length>0,"abort releases exact stage and capture resumes");
  Ashfall.Modules.InventoryItems.Inventory.PreparedRemoval removal;Check(inventory.PrepareRemoval(C(),"next",out removal)&&removal.Validate(),"actual validated Inventory reservation");
  var ib=new CheckpointBinding("fixture",ColdInventoryCheckpoint.CatalogFingerprint(Ashfall.Modules.InventoryItems.SourceCatalog.Create()),"s","a",1,0,0,23);busy=false;try{ConsumableInventoryCheckpoint.Capture(ib,inventory);}catch(InvalidDataException){busy=true;}Check(busy,"public lower Inventory codec refuses outstanding validated reservation");removal.Abort();Check(ConsumableInventoryCheckpoint.Capture(ib,inventory).Length>0,"Inventory capture after exact abort");
 }
 public static void Main(string[] args){var t=new TestsR5();if(args[0]=="capture"){t.Run();t.LifetimeChecks();t.Capture(args[1]);}else t.Restore(args[1]);}
}

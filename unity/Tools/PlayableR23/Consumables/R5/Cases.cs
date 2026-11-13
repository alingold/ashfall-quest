using System;
using Ashfall.Core;
using Ashfall.Modules.InventoryItems;
using Ashfall.Modules.Pickups;
using Ashfall.Integration.Playable.Consumables;
using Ashfall.Integration.Playable.HandsWeapons;
class R5ItemCases
{
 static int count;static void Check(bool yes,string why){count++;if(!yes)throw new Exception(why);}
 sealed class Query:IInteractionQuery{public bool HasLineOfSight(string a,string b,Float3 c,Float3 d,float range){return true;}}
 sealed class BuildGate:IAuthorityGate,IBuildGrantAuthority
 {public RuleResult Validate(CommandContext c){return RuleResult.Accept();}public bool Validate(CommandContext c,string receipt,string kind,int amount){return kind=="builds"&&amount==6&&receipt.StartsWith("pickup089:",StringComparison.Ordinal);}}
 sealed class Effects:IR23ConsumableEffects
 {
  public readonly R23RoundState Round;public bool Refilled;
  public Effects(R23RoundState round){Round=round;}
  public R23ConsumableVitals ReadVitals(){return new R23ConsumableVitals{Alive=Round.Health>0,Health=Round.Health,Shield=Round.Shield,Stamina=Refilled?8:1,MaxStamina=8};}
  public bool TryApplyGain(float health,float shield,bool refill){bool gain=Round.ApplyPlayerGain(health,shield);if(refill&&!Refilled){Refilled=true;gain=true;}return gain;}
 }
 static ActorFrame Frame(){return new ActorFrame{Allowed=true,HandEligible=true,OtherHandEligible=true,TransferSafe=true,Head=new Float3(0,1.65f,0),Hand=new Float3(0,1,.2f),Forward=new Float3(0,0,1),LocalHand=new Float3(0,1,.2f),OtherLocal=new Float3(0,.1f,0),Anchor=new Float3(0,1.65f,.2f),HeadLocalAnchor=new Float3(0,0,.2f),ItemUpDot=0};}
 static void Take(R23ConsumableState c,string id){Check(c.Execute(PickupAction.Reserve,id).Accepted&&c.Execute(PickupAction.Collect,id).Accepted&&c.Execute(PickupAction.Equip,id).Accepted,"real source pickup "+id);}
 static void Main()
 {
  var round=new R23RoundState(new string[0],new Query(),true);var state=new R24SupplyState(round.ApplyReserveSupply);
  foreach(string family in R24SupplyState.Families)
  {
   if(family=="builds"||family=="backpack")continue;int before=round.ReadAmmoReserveFamily(family),amount=R24SupplyState.DefaultAmount(family);
   Check(state.Register(family,family)&&!state.Register(family,family),"single source world identity "+family);
   Check(state.Collect(family)==amount&&round.ReadAmmoReserveFamily(family)==before+amount,"actual shared native reserve receives source box "+family);
   Check(state.Collect(family)==0&&round.ApplyReserveSupply("pickup089:"+family,family,amount)==0,"duplicate box and native event do not grant "+family);
  }
  Check(!state.Register("bad","energy")&&!state.Register("bad","health"),"no invented energy or health bottles");
  int available=round.ReadAmmoReserveFamily("pistol");int debit=round.WithdrawReserveSupply("pistol",available-3);Check(debit==available-3&&round.ReadAmmoReserveFamily("pistol")==3,"source drop reads/debits actual pool");
  int dropped=round.WithdrawReserveSupply("pistol",16);Check(dropped==3&&round.ReadAmmoReserveFamily("pistol")==0,"last partial source box withdraws only actual quantity");
  Check(state.Register("dropped","pistol",dropped)&&state.Collect("dropped")==3&&round.ReadAmmoReserveFamily("pistol")==3,"same actual partial reserve returns through finite pickup");
  Check(state.Register("empty","pistol",0)&&state.Collect("empty")==0,"empty source box cannot invent one round");
  R24SupplyItem view;Check(state.TryRead("dropped",out view)&&view.Amount==3&&view.Collected&&view.Family=="pistol","immutable read snapshot exposes actual amount and collection");
  var gate=new BuildGate();var ledger=new BuildChargeAccounts("supplies",0,gate,gate).ForActor("player");long serial=0;
  var builds=new R24SupplyState(delegate(string id,string family,int amount){BuildChargeLedger.PreparedCharge plan;long n=++serial;var c=new CommandContext("supplies","player","pickup-"+n,n,0,true);if(!ledger.PrepareGrant(c,id,family,amount,n,out plan).Accepted||!plan.Validate(n))return 0;plan.CommitValidated();return amount;});
  Check(builds.Register("actual-builds","builds")&&builds.Collect("actual-builds")==6&&ledger.Balance==6,"actual canonical build ledger accepts source six panel box");
  Check(builds.Collect("actual-builds")==0&&ledger.Balance==6,"build box cannot duplicate ledger charge");builds.ResetRound();Check(builds.Collect("actual-builds")==0&&ledger.Balance==6,"box reset alone cannot replay actual owner's event");
  R24SupplyState reentrant=null;reentrant=new R24SupplyState(delegate(string id,string family,int amount){Check(reentrant.Collect("reentry")==0,"effect callback cannot collect again recursively");return amount;});reentrant.Register("reentry","rifle");Check(reentrant.Collect("reentry")==32,"accepted source box completes once");
  bool reject=true;var retry=new R24SupplyState(delegate(string id,string family,int amount){return reject?0:amount;});retry.Register("retry","shells");Check(retry.Collect("retry")==0&&retry.TryRead("retry",out view)&&!view.Collected,"unaccepted real effect keeps source box");reject=false;Check(retry.Collect("retry")==6,"fresh explicit grip may collect after real capacity allows");
  // These consumables use the SAME production HealthService as explosions and gun damage.
  round.Reset();round.Damage(180);var effects=new Effects(round);var cstate=new R23ConsumableState(effects,new Query());cstate.Register("soda","soda",new Float3(0,1,.3f));cstate.SetFrame(Frame());cstate.Advance(.02f);Take(cstate,"soda");Check(cstate.Execute(PickupAction.Open,"soda").Accepted&&cstate.Execute(PickupAction.Consume,"soda").Accepted,"source soda opens and drinks into actual player effect port");
  for(int i=0;i<100;i++){cstate.SetFrame(Frame());cstate.Advance(.1f);}Check(Math.Abs(round.Shield-50)<.001f&&Math.Abs(round.Health-20)<.001f,"actual health owner receives source fifty shield over ten seconds");
  cstate.Register("banana","banana",new Float3(0,1,.3f));cstate.SetFrame(Frame());Take(cstate,"banana");for(int i=0;i<4;i++){var f=Frame();f.OtherGrip=false;cstate.SetFrame(f);cstate.Execute(PickupAction.SamplePeel,"banana");f.OtherGrip=true;cstate.SetFrame(f);Check(cstate.Execute(PickupAction.BeginPeel,"banana").Accepted,"source opposite hand starts peel");f.OtherLocal=new Float3(0,-.01f,0);cstate.SetFrame(f);Check(cstate.Execute(PickupAction.SamplePeel,"banana").Accepted,"source ten centimetre pull exposes peel");}
  cstate.SetFrame(Frame());Check(cstate.Execute(PickupAction.Consume,"banana").Accepted&&round.Health==100&&round.Shield==50&&effects.Refilled,"banana heals real native health and requests actual stamina refill");
  cstate.Register("shaker","shaker",new Float3(0,1,.3f));cstate.SetFrame(Frame());Take(cstate,"shaker");for(int i=0;i<130;i++){var f=Frame();f.LocalHand=new Float3(0,1+(i%2==0?.04f:0),.2f);cstate.SetFrame(f);cstate.Advance(.02f);cstate.Execute(PickupAction.SampleShake,"shaker");}cstate.SetFrame(Frame());Check(cstate.Execute(PickupAction.Consume,"shaker").Accepted&&round.Shield==100,"charged shaker sends source instant fifty shield into actual health owner");
  var bagBudget=new R23ConsumableState(effects,new Query());var grenades=new Ashfall.Integration.Playable.Explosives.R23GrenadeState();var bags=new R24SupplyState(delegate(string id,string family,int amount){if(family!="backpack"||bagBudget.SlotCapacity>=6)return 0;bagBudget.SlotCapacity++;grenades.SlotCapacity=bagBudget.SlotCapacity;return 1;});
  Check(bagBudget.SlotCapacity==3&&grenades.SlotCapacity==3,"source initial shared capacity three");
  for(int i=0;i<4;i++)bags.Register("source-bag"+i,"backpack");for(int i=0;i<3;i++)Check(bags.Collect("source-bag"+i)==1&&bagBudget.SlotCapacity==4+i&&grenades.SlotCapacity==4+i,"source actual backpack expansion +1");
  Check(bags.Collect("source-bag0")==0&&bags.Collect("source-bag3")==0&&bags.TryRead("source-bag3",out view)&&!view.Collected,"no repeat bag grant or cap above six; full bag remains available");
  bagBudget.SetOtherSlotsUsed(2);for(int i=0;i<5;i++){string id="capacity-banana"+i;bagBudget.Register(id,"banana",new Float3(0,1,.3f));bagBudget.SetFrame(Frame());bool take=bagBudget.Execute(PickupAction.Reserve,id).Accepted&&bagBudget.Execute(PickupAction.Collect,id).Accepted;Check(take==(i<4),"native consumable admission honors expanded capacity and other owners");}
  grenades.SetOtherSlotsUsed(5);grenades.Register("one-zone","zone");grenades.Register("two-zone","zone");Check(grenades.Pickup("one-zone",1,true)&&!grenades.Pickup("two-zone",2,true),"expanded shared capacity allows only remaining real grenade slot");
  Console.WriteLine("PASS "+count+" actual native reserve/build-ledger/health/backpack and source item cases; no engine/device run.");
 }
}

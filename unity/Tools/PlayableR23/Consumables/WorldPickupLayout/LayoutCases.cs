using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Pickups;
using Ashfall.Integration.Playable.Consumables;
using Ashfall.Integration.Playable.Consumables.WorldPickupLayout;
using Ashfall.Integration.Playable.HandsWeapons;
static class LayoutCases
{
 static int count;static void Check(bool x,string why){count++;if(!x)throw new Exception(why);}static bool Near(float a,float b){return Math.Abs(a-b)<.000001f;}
 sealed class Query:IInteractionQuery{public bool HasLineOfSight(string a,string b,Float3 c,Float3 d,float range){return true;}}
 sealed class Effects:IR23ConsumableEffects
 {
  readonly R23RoundState round;public Effects(R23RoundState r){round=r;}
  public R23ConsumableVitals ReadVitals(){return new R23ConsumableVitals{Alive=round.Health>0,Health=round.Health,Shield=round.Shield,Stamina=1,MaxStamina=8};}
  public bool TryApplyGain(float h,float s,bool refill){return round.ApplyPlayerGain(h,s);}
 }
 static ActorFrame Frame(){return new ActorFrame{Allowed=true,HandEligible=true,TransferSafe=true,OtherHandEligible=true,Head=new Float3(0,1.65f,0),Hand=new Float3(0,1,.2f),Forward=new Float3(0,0,1),LocalHand=new Float3(0,1,.2f),OtherLocal=new Float3(0,.1f,0),Anchor=new Float3(0,1.65f,.2f),HeadLocalAnchor=new Float3(0,0,.2f),ItemUpDot=0};}
 static void Main()
 {
  var rows=R24WorldPickupLayout.FoodsAndBackpacks();var ids=new HashSet<string>();int foods=0,bags=0;var expected=new[]{"shaker","soda","banana"};var origin=new Float3(101,4,-5000);
  Check(rows.Length==12,"exact source12 food/bag props");
  for(int i=0;i<rows.Length;i++)
  {
   var row=rows[i];Check(ids.Add(row.Id),"independent stable source instance "+i);Check(row.SourceIndex==i&&row.Kind==(i<9?expected[i%3]:"backpack")&&row.IsSupply==(i>=9),"source KINDS/source index/real owner "+i);
   var p=row.At(origin);Check(Near(p.X,origin.X+(float)(-3.4+i%4*.65))&&Near(p.Y,4.75f)&&Near(p.Z,origin.Z-(float)(1.8+i/4*.65)),"checkpoint local position + singleZ reflection "+i);if(row.IsSupply)bags++;else foods++;
  }
  Check(foods==9&&bags==3&&rows[0].Id=="r23-starter/shaker"&&rows[1].Id=="r23-starter/soda"&&rows[2].Id=="r23-starter/banana","nine foods/three bags preserve existing3 exact identities");
  rows[0]=null;Check(R24WorldPickupLayout.FoodsAndBackpacks()[0]!=null,"immutable layout returned without mutable shared array");
  var ammo=R24WorldPickupLayout.Ammo();Check(ammo.Length==6,"source six ammo/build families; bags not invented seventh ammo row");var families=new[]{"rifle","pistol","shells","smg","sniper","builds"};float[] x={1.5f,2,2.5f,3.15f,3.7f,4.25f};
  for(int i=0;i<6;i++){Check(ammo[i].Kind==families[i]&&ids.Add(ammo[i].Id),"actual source family/identity "+i);Check(Near(ammo[i].UnityOffset.X,x[i])&&Near(ammo[i].UnityOffset.Y,.3f)&&ammo[i].UnityOffset.Z==2,"first3 + source fallback from FIRST position "+i);}
  float y;Check(R24WorldPickupLayout.TrySettleY(5,3,4.8f,out y)&&Near(y,3.6452f),"actual root shift by hit +hover -visible lowest");Check(!R24WorldPickupLayout.TrySettleY(5,Single.NaN,4,out y)&&y==5,"no finite hit preserves original placement");
  var min=new Float3(-1,-2,-3);var max=new Float3(1,2,3);
  Check(R24WorldPickupLayout.TryLowestCornerY(min,max,0,1,0,8,out y)&&y==6,"all8 visible local corners identity/translation");
  Check(R24WorldPickupLayout.TryLowestCornerY(min,max,1,0,0,8,out y)&&y==7,"rotated source bounds use actual transform");
  Check(R24WorldPickupLayout.TryLowestCornerY(min,max,0,-2,0,8,out y)&&y==4,"mirrored/scaled visible bounds");
  Check(R24WorldPickupLayout.TryLowestCornerY(min,max,.5f,.75f,-.25f,8,out y)&&Near(y,5.25f),"nested affine rotation/scale all8corners");
  Check(!R24WorldPickupLayout.TryLowestCornerY(min,max,Single.NaN,1,0,0,out y),"invalid transform does not invent floor");
  var query=new Query();var round=new R23RoundState(new string[0],query,true);round.Damage(180);var state=new R23ConsumableState(new Effects(round),query);var soda=R24WorldPickupLayout.FoodsAndBackpacks()[1];var at=new Float3(0,1,.3f);
  Check(state.Register(soda.Id,soda.Kind,at)&&!state.Register(soda.Id,soda.Kind,at),"actual food owner source registration deduplicates");state.SetFrame(Frame());state.Advance(.02f);
  Check(state.Execute(PickupAction.Reserve,soda.Id).Accepted&&state.Execute(PickupAction.Collect,soda.Id).Accepted&&state.Execute(PickupAction.Equip,soda.Id).Accepted,"actual food custody transfer");
  Check(!state.RepositionWorldItem(soda.Id,new Float3(99,99,99))&&state.OwnedItems.Length==1,"checkpoint cannot move or delete owned food");
  Check(state.Execute(PickupAction.Open,soda.Id).Accepted&&state.Execute(PickupAction.Consume,soda.Id).Accepted&&state.Inspect(soda.Id).Removed,"actual consumed source instance tombstone");
  Check(!state.Register(soda.Id,soda.Kind,at)&&!state.RepositionWorldItem(soda.Id,new Float3(99,99,99))&&state.OwnedItems.Length==0,"checkpoint does not revive consumed item");
  state.Reset();Check(!state.Inspect(soda.Id).Removed&&state.Inspect(soda.Id).Owner==null,"ONLY explicit round reset restores registered food");
  var supply=new R24SupplyState(round.ApplyReserveSupply);foreach(var row in ammo)if(row.Kind!="builds"){int before=round.ReadAmmoReserveFamily(row.Kind);Check(supply.Register(row.Id,row.Kind)&&supply.Collect(row.Id)==R24SupplyState.DefaultAmount(row.Kind),"actual finite family receives source pickup "+row.Kind);Check(!supply.Register(row.Id,row.Kind)&&supply.Collect(row.Id)==0&&round.ReadAmmoReserveFamily(row.Kind)==before+R24SupplyState.DefaultAmount(row.Kind),"reapply cannot mint same ammo "+row.Kind);}
  var bagsOwner=new R24SupplyState(delegate(string id,string family,int amount){if(family!="backpack"||state.SlotCapacity>=6)return 0;state.SlotCapacity++;return 1;});foreach(var row in R24WorldPickupLayout.FoodsAndBackpacks())if(row.IsSupply)Check(bagsOwner.Register(row.Id,row.Kind)&&bagsOwner.Collect(row.Id)==1&&bagsOwner.Collect(row.Id)==0,"three real one-time backpack effects "+row.SourceIndex);Check(state.SlotCapacity==6,"three genuine bags reach actual shared cap6");
  // Source permutations remain literal even when fewer validated loot sites repeat.
  foreach(int n in new[]{1,7,19,72,137})
  {
   for(int k=0;k<12;k++)Check(R24ArchivePickupLayout.FoodLootIndex(k,n)==(k*19+11)%n,"archive source12 food/bag index");
   int[] familyCounts=new int[6];var archiveIds=new HashSet<string>();
   for(int k=0;k<72;k++){Check(R24ArchivePickupLayout.AmmoLootIndex(k,n)==(k*13+5)%n&&R24ArchivePickupLayout.AmmoFamily(k)==families[k%6],"archive72 actual source index/family");familyCounts[k%6]++;Check(archiveIds.Add(R24ArchivePickupLayout.AmmoId("archive:alfa",k)),"archive independent stable slot identity");}
   for(int k=0;k<6;k++)Check(familyCounts[k]==12,"archive exactly12 finite boxes per family");
  }
  Check(R24ArchivePickupLayout.AmmoId("archive:alfa",0)!=R24ArchivePickupLayout.AmmoId("archive:classic-bigbox-vr",0),"archive map identities cannot collide");
  bool rejected=false;try{R24ArchivePickupLayout.FoodLootIndex(12,5);}catch(ArgumentOutOfRangeException){rejected=true;}Check(rejected,"reject nonexistent food13");
  rejected=false;try{R24ArchivePickupLayout.AmmoLootIndex(72,5);}catch(ArgumentOutOfRangeException){rejected=true;}Check(rejected,"reject nonexistent box73");
  rejected=false;try{R24ArchivePickupLayout.AmmoId("range",0);}catch(ArgumentException){rejected=true;}Check(rejected,"archive ownership cannot alias checkpoint Range");
  rejected=false;try{R24ArchivePickupLayout.AmmoLootIndex(0,0);}catch(ArgumentOutOfRangeException){rejected=true;}Check(rejected,"no validated sites means no invented positions");
  var archiveRound=new R23RoundState(new string[0],query,true);var archiveState=new R24SupplyState(archiveRound.ApplyReserveSupply);string collectedId=R24ArchivePickupLayout.AmmoId("archive:alfa",0),availableId=R24ArchivePickupLayout.AmmoId("archive:alfa",1);int reserveBefore=archiveRound.ReadAmmoReserveFamily("rifle");
  Check(archiveState.Register(collectedId,"rifle")&&archiveState.Register(availableId,"pistol"),"actual archive owner registers finite source boxes");
  Check(archiveState.Collect(collectedId)==32&&archiveRound.ReadAmmoReserveFamily("rifle")==reserveBefore+32,"actual archive collection grants finite reserve once");
  Check(!archiveState.RemoveUncollected(collectedId)&&!archiveState.Register(collectedId,"rifle")&&archiveState.Collect(collectedId)==0,"plan/map refresh preserves collected tombstone and cannot mint ammo");
  Check(archiveState.RemoveUncollected(availableId)&&!archiveState.RemoveUncollected(availableId)&&archiveState.Register(availableId,"pistol"),"only previous uncollected generation can retire/recreate");
  R24SupplyItem tombstone;Check(archiveState.TryRead(collectedId,out tombstone)&&tombstone.Collected&&archiveRound.ReadAmmoReserveFamily("rifle")==reserveBefore+32,"retiring world generation leaves authoritative grants unchanged");
  R24SupplyState reentrant=null;bool refusedDuringGrant=false;reentrant=new R24SupplyState(delegate(string eventId,string family,int amount){refusedDuringGrant=!reentrant.RemoveUncollected(availableId);return amount;});reentrant.Register(availableId,"pistol");
  Check(reentrant.Collect(availableId)==16&&refusedDuringGrant&&reentrant.TryRead(availableId,out tombstone)&&tombstone.Collected,"teardown cannot race the actual collection callback");
  archiveState.ResetRound();Check(archiveState.TryRead(collectedId,out tombstone)&&!tombstone.Collected,"only explicit source round reset restores archive collected box");
  Console.WriteLine("PASS "+count+" actual source layout/8-corner/finite owner/tombstone cases; native physics unrun");
 }
}

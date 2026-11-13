#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;using System.Collections;using System.Collections.Generic;
using Ashfall.Modules.Player;using Ashfall.Integration.Playable;using Ashfall.Integration.Playable.Consumables.WorldPickupLayout;
using UnityEngine;
namespace Ashfall.Editor.Playable
{
 public static partial class R24Feedback6237Smoke
 {
  static IEnumerator EitherHandTransfers98254(R23PlayableLoop loop,Action<string> note,bool remainingOnly=false)
  {
   note("PICKUP98254 actual reaching/receiving fixture entered");var original=loop.UI.Settings;int cell=0;var cells=new HashSet<Vector3>();
   try{
    loop.ReturnToLobby();loop.UI.OpenMaps();Need(loop.UI.ChooseMap("range"),"Actual Range");float end=Time.realtimeSinceStartup+60;while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;Need(loop.Ready&&loop.Map.MapId=="range","Range loaded");
    foreach(int primary in remainingOnly?new[]{1}:new[]{0,1})foreach(int reaching in new[]{0,1})foreach(string kind in new[]{"weapon","soda","frag","backpack","rifle"}){
     loop.RestartRound();loop.Gameplay.HolsterCurrent();loop.UI.SetSettings(original.With(dominantHand:primary,gripToggle:false));Call(loop,"ApplySettings",loop.UI.Settings);
     var at=new Vector3(10+(cell%5)*4,.2f,-620-(cell/5)*4);Need(cells.Add(at),"Unique finite fixture cell");cell++;loop.Rig.SetSpawn(at,at+Vector3.forward);loop.Calibration.NotifyRootPoseReset();loop.Rig.Left.localPosition=new Vector3(-.25f,1.15f,.35f);loop.Rig.Right.localPosition=new Vector3(.25f,1.15f,.35f);loop.Rig.Left.localRotation=loop.Rig.Right.localRotation=Quaternion.identity;
     loop.Rig.SmokeTrackedHands(new HandSample{Tracked=true},new HandSample{Tracked=true});loop.SmokeUiUpdate();Need(loop.Gameplay.PrimaryHand==primary&&loop.Gameplay.PreferredPickupHand==primary,"Configured receiving hand committed");
     var owned=loop.Gameplay.OwnedWeaponActionIds;Need(owned.Length>0&&loop.Gameplay.DropOwned(owned[owned.Length-1],at+Vector3.up+Vector3.forward*4,Quaternion.identity),"Real free inventory slot");Call(loop,"RefreshSlotBudget");
     var hand=reaching==0?loop.Rig.Left:loop.Rig.Right;string id="pickup98254/"+primary+"/"+reaching+"/"+kind;bool spawned;
     if(kind=="weapon")spawned=loop.Gameplay.SpawnWorldWeapon("px4",1,hand.position,Quaternion.identity,out id);
     else if(kind=="soda")spawned=loop.Consumables.Spawn(id,"soda",hand.position);
     else if(kind=="frag")spawned=loop.Explosives.SpawnWorldItem(id,"frag",1,hand.position,Quaternion.identity);
     else spawned=loop.Supplies.Spawn(id,kind,hand.position,kind=="backpack"?1:32);
     Need(spawned,"Actual finite prop "+id);WorldGripFrame(loop,reaching,0);var selected=WorldGripFrame(loop,reaching,1);bool taken=kind=="rifle"?WorldTaken(loop,"backpack",id):WorldTaken(loop,kind,id);note("Reach="+reaching+" configuredReceiver="+primary+" kind="+kind+" actualTarget="+selected.Kind+"/"+selected.Id+" physical="+selected.Hand+" distance="+selected.Distance+" actualReceiver="+loop.Gameplay.PrimaryHand+" taken="+taken);
     Need(selected.Id==id&&selected.Hand==reaching&&taken,"Actual reaching-hand selection collects exact item");
     if(kind=="weapon")Need(loop.Gameplay.HasWeapon&&loop.Gameplay.PrimaryHand==primary,"Weapon equips receiving hand, not reaching hand");
     if(kind=="soda")Need(loop.Consumables.HeldId==id&&loop.Consumables.HeldHand==primary+1,"Food equips receiving hand");
     if(kind=="frag")Need(loop.Explosives.HeldId==id&&loop.Explosives.HeldHand==primary+1,"Grenade equips receiving hand");
     WorldGripFrame(loop,reaching,0);yield return null;
    }
    note((remainingOnly?"10 remaining configured-right":"20")+" actual Root pickup cases, two configured handedness settings x two physical reaching hands x weapon/food/grenade/backpack/ammo. No controller/bore/physics changes or HMD claim.");
   }finally{loop.UI.SetSettings(original);Call(loop,"ApplySettings",original);loop.RestartRound();}
  }
  static IEnumerator SupplyNeighbors98254(R23PlayableLoop loop,Action<string> note)
  {
   note("PICKUP98254 original source supply cluster fixture entered");loop.RestartRound();loop.Gameplay.HolsterCurrent();for(int i=0;i<8;i++){loop.Rig.SmokeTrackedHands(new HandSample{Tracked=true},new HandSample{Tracked=true});loop.SmokeUiUpdate();yield return new WaitForFixedUpdate();}
   var boxes=(IDictionary)Read(loop.Supplies,"boxes");var sourceIds=new List<string>();foreach(var row in R24WorldPickupLayout.FoodsAndBackpacks())if(row.IsSupply)sourceIds.Add(row.Id);foreach(var row in R24WorldPickupLayout.Ammo())sourceIds.Add(row.Id);
   var views=new Dictionary<string,GameObject>();var positions=new Dictionary<string,Vector3>();foreach(string id in sourceIds){Need(boxes.Contains(id),"Actual source box "+id);var view=(GameObject)Read(boxes[id],"View");Need(view!=null&&view.activeInHierarchy,"Source box visible "+id);views.Add(id,view);positions.Add(id,view.transform.position);}
   string selectedId=sourceIds[0];Vector3 logical;Quaternion rotation;bool collected;Need(loop.Supplies.TryReadWorldPose(selectedId,out logical,out rotation,out collected)&&!collected,"Actual uncollected source backpack");Vector3 spot;Need(ClearBotPlayerSpot(loop,logical+Vector3.forward*.7f,out spot),"Actual supported approach to original source backpack cluster");loop.Rig.SetSpawn(spot,logical);int physical=1-loop.Gameplay.PrimaryHand;var reaching=physical==0?loop.Rig.Left:loop.Rig.Right;reaching.position=logical;Physics.SyncTransforms();
   Transform parent=(Transform)Read(loop.Supplies,"content");Vector3 parentBefore=parent.position;int capacity=loop.InventoryCapacity;WorldGripFrame(loop,physical,0);var selected=WorldGripFrame(loop,physical,1);Need(selected.Id==selectedId&&loop.InventoryCapacity==capacity+1,"One real source backpack adds exactly one capacity");
   Need(parent.position==parentBefore,"Moving sound must not translate all-supplies content parent");foreach(string id in sourceIds){bool isSelected=id==selectedId;Need(Vector3.Distance(positions[id],views[id].transform.position)<.001f,"Every source model position preserved after sound "+id);Need(views[id].activeInHierarchy!=isSelected,"Only selected model hidden "+id);Ashfall.Integration.Playable.Consumables.R24SupplyItem item;Need(loop.Supplies.TryReadItem(id,out item)&&item.Collected==isSelected,"Only selected owner collected "+id);}
   for(int i=0;i<60;i++)WorldGripFrame(loop,physical,1);Need(loop.InventoryCapacity==capacity+1,"60 held frames do not cascade");note("Actual source three-backpack/six-ammo cluster: one owner/capacity, sibling render world positions+visibility+state retained, parent stable during pickup audio. This directly exercises prior parent-transform fault, not isolated same-kind props only.");
  }
 }
}
#endif

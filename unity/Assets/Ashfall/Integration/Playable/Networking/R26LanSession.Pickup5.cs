#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable.Networking {
 public sealed partial class R24LanSession {
  sealed class HeldSwap {
   internal string Incoming,Local,Offered,Released;
   internal int Hand,Spawn;
   internal long Revision,Command;
   internal bool DropApplied;
   internal Vector3 At;
   internal Quaternion Rotation;
  }
  HeldSwap heldSwap;
  string confirmedSwapDrop;
  bool UseHeldWeaponSwap(){return root.Gameplay.HasWeapon&&root.Consumables.HeldId==null&&(root.Explosives==null||root.Explosives.HeldId==null)&&!root.LanWeaponFits();}
  bool StartHeldWeaponSwap(string local,R24LanLootState target,int hand){
   var lobby=peer.Lobby;var gun=root.Gameplay;string offered=gun.ActiveWeaponActionId,canonical=CanonicalLoot(offered);
   Bounds bounds;Vector3 at;Quaternion rotation;R24LanSpawn spawn;
   if(heldSwap!=null||!lobby.TryReadSpawn(lobby.OwnActor,out spawn)||!gun.TryGetWeaponBounds(offered,out bounds)||!R23WorldItemPlacement.TryDrop(root.Rig.Head.transform,root.Rig.transform,bounds,out at,out rotation))return false;
   R24LanLootState row;string released=lobby.TryReadLoot(canonical,out row)?canonical:lobby.StarterDropId(lobby.OwnActor,canonical);
   PublishRawTracking();
   if(!lobby.RequestWeaponSwap(target.Id,canonical,hand,V(at)))return false;
   heldSwap=new HeldSwap{Incoming=target.Id,Local=local,Offered=offered,Released=released,Hand=hand,Spawn=spawn.Revision,Revision=target.Revision,Command=lobby.CommandSequence,At=at,Rotation=rotation};
   if(lobby.State!=R24LanLobbyState.Hosting)return false;
   return FinishSwapDrop(heldSwap);
  }
  bool FinishSwapDrop(HeldSwap swap){
   // Both changes already committed on the host. Project the exact outgoing lifetime once.
   if(swap.DropApplied)return true;
   if(root.Gameplay.OwnsWeapon(swap.Offered)){
    confirmedSwapDrop=swap.Offered;
    try{if(!root.Gameplay.DropOwned(swap.Offered,swap.At,swap.Rotation))return false;}finally{confirmedSwapDrop=null;}
   }
   BindDroppedAlias(swap.Offered,swap.Released);swap.DropApplied=true;return true;
  }
  void SyncHeldWeaponSwap(R24LanLobby lobby){
   var swap=heldSwap;if(swap==null)return;
   R24LanSpawn spawn;R24LanLootState incoming,released;
   if(!lobby.TryReadSpawn(lobby.OwnActor,out spawn)||spawn.Revision!=swap.Spawn){heldSwap=null;return;}
   if(lobby.State==R24LanLobbyState.Connected&&lobby.LastRefusedWeaponSwap==swap.Command){heldSwap=null;return;}
   if(!lobby.TryReadLoot(swap.Incoming,out incoming)||incoming.Revision<=swap.Revision)return;
   if(incoming.Owner!=lobby.OwnActor){if(swap.DropApplied)heldSwap=null;return;}
   if(!lobby.TryReadLoot(swap.Released,out released)||released.Owner==lobby.OwnActor)return;
   if(!FinishSwapDrop(swap))return;
   if(released.Owner!="")root.Gameplay.SetWorldWeaponVisible(swap.Offered,false);
   if(root.Gameplay.OwnsWeapon(swap.Local)||(root.LanWeaponFits()&&root.CompleteLanCatalogPickup(swap.Local,"weapon",incoming.Profile,swap.Hand)))heldSwap=null;
  }
 }
}
#endif

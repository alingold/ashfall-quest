#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Integration.Playable.Networking;
using UnityEngine;
namespace Ashfall.Integration.Playable {
 public sealed partial class R25NetBot {
  static readonly string[] swapTargets={"spr1","m601","spr0","m600","px41"};
  string swapStep,swapOffered,swapReleased,swapLastGlobal,swapLastLocal;
  float swapPressed;int swapSlots;bool swapHadGun;
  void Pickup5Fixtures(R24LanLobby lobby){
   foreach(var member in lobby.Members){
    R24LanPoseSnapshot pose;if(!lobby.TryReadPresence(member.Actor,out pose))continue;
    for(int i=0;i<swapTargets.Length;i++){
     var p=V(pose.Pose.Root.Position)+new Vector3(i==4?0:i%2==0?-.65f:.65f,.65f,i==4?.95f:i<2?.65f:-.65f);
     string id="p5/"+member.Actor+"/"+swapTargets[i];
     Write("pickup5-fixture",id+" registered="+lobby.RegisterWorldWeapon(id,i==4?"px4":i%2==0?"spr":"m60",1,new Float3(p.x,p.y,p.z)));
    }
   }
  }
  void Pickup5Step(R24LanLobby lobby,string target,float t,ref Vector3 left,ref Vector3 right,ref Quaternion aim,ref float grip,ref bool leftGrip,out bool done,out bool ok,out string note){
   var game=root.Gameplay;int hand=target.EndsWith("0")?0:1;string global="p5/"+lobby.OwnActor+"/"+target;
   string profile=target.Substring(0,target.Length-1);
   string local=mode=="host"?global:R24LanWorldIdentity.LocalId(lobby.SessionId,lobby.MapManifestHash,"weapon",global,profile,1);
   if(swapStep!=target){
    swapStep=target;swapPressed=-1;swapSlots=root.Pickup4Used;swapOffered=swapLastLocal??game.ActiveWeaponActionId;
    swapReleased=swapLastGlobal??lobby.OwnActor+"/"+swapOffered;
    root.UI.SetSettings(root.UI.Settings.With(dominantHand:hand));root.Rig.ApplySettings(root.UI.Settings);game.PreferredPickupHand=hand;
    bool equipped=game.OwnsWeapon(swapOffered)&&game.EquipOwned(swapOffered,hand);swapHadGun=equipped;
    Write("pickup5-before",target+" wanted="+local+" offered="+swapOffered+" equipped="+equipped+" hand="+game.PrimaryHand+" slots="+root.Pickup4Used+"/"+root.InventoryCapacity);
   }
   Vector3 center,at;Quaternion rotation;string instance;
   if(game.TryReadWorldWeapon(local,out instance,out center,out at,out rotation)){
    Vector3 pos=center-root.Rig.transform.forward*.06f;if(hand==0)left=pos;else right=pos;
    aim=Quaternion.LookRotation(center-pos)*Quaternion.Inverse(R23TrackedGripPose.LocalRotation);
   }
   if(swapPressed<0&&t>=2.3f&&(!target.StartsWith("m60")||swapSlots==root.InventoryCapacity&&swapHadGun&&game.HasWeapon&&game.PrimaryHand==hand&&game.ActiveWeaponId=="spr"&&game.ActiveWeaponActionId==swapOffered)){
    string aimed;float score;if(game.TryReadAimedWorldWeapon(hand,out aimed,out center,out score)&&aimed==local){
     swapPressed=Time.realtimeSinceStartup;Write("pickup5-grip",target+" "+root.Rig.NetBotDiagnostics+" origin="+root.Rig.TrackingOrigin.localPosition.y);
    }
   }
   if(swapPressed>=0){grip=.9f;leftGrip=hand==0;}
   var owned=new List<R26OwnedWeaponReceipt>();lobby.CopyOwnWeaponReceipts(owned);
   R24LanLootState outgoing,incoming;
   bool rows=lobby.TryReadLoot(global,out incoming)&&incoming.Owner==lobby.OwnActor&&(swapSlots<root.InventoryCapacity||lobby.TryReadLoot(swapReleased,out outgoing)&&outgoing.Owner=="");
   bool dropped=game.TryReadWorldWeapon(swapOffered,out instance,out center,out at,out rotation)&&!game.OwnsWeapon(swapOffered);
   ok=swapPressed>=0&&Time.realtimeSinceStartup-swapPressed>=1.25f&&game.HasWeapon&&game.ActiveWeaponActionId==local&&game.PrimaryHand==hand&&(swapSlots<root.InventoryCapacity||dropped)&&rows&&root.Pickup4Used==Mathf.Min(swapSlots+1,root.InventoryCapacity)&&owned.Count==game.OwnedWeaponActionIds.Length&&owned.Exists(w=>w.Id==global);
   done=ok||t>=12;note="oneGrip="+(swapPressed>=0)+" outgoing="+swapReleased+" incoming="+global+" rows="+rows+" ground="+dropped+" ledger="+owned.Count+" held="+game.ActiveWeaponActionId+" hand="+game.PrimaryHand;
   if(done){swapLastLocal=local;swapLastGlobal=global;}
  }
 }
}
#endif

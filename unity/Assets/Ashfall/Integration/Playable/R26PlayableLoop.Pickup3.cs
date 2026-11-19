#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable {
 public sealed partial class R23PlayableLoop {
  internal string Pickup3State {get{return "round="+roundStarted+" mapSwitch="+switchingMap+" page="+UI.Page+" blocked="+PageBlocksGameplay+" backpack="+BackpackOpen+" hand="+Gameplay.PrimaryHand+" held="+Gameplay.HasWeapon+":"+Gameplay.ActiveWeaponActionId+" owned="+System.String.Join(",",Gameplay.OwnedWeaponActionIds)+" eligible="+Gameplay.WorldPickupHandEligible(0)+","+Gameplay.WorldPickupHandEligible(1)+" rawGrip="+Rig.LeftInput.Grip+","+Rig.RightInput.Grip+" rawPrimary="+Rig.RightInput.Primary+" motorReady="+Rig.LeftInputReady+","+Rig.RightInputReady+" queuedDrop="+(queuedDrop??"none")+" wheel="+(WeaponWheel==null?"none":WeaponWheel.Pickup3State)+" net="+LanSession.Pickup3State;}}
  internal string Pickup3TargetState(string id){return "worldGate="+(WorldLoot==null||WorldLoot.AllowsWorldClaim(id))+" rackGate="+(MountedRacks==null||MountedRacks.AllowsWorldClaim(id))+" localRoom="+LanWeaponFits();}
  internal void Pickup3LoseLocalCustody(){if(R25NetBot.Arg("-ashfallNetBotPickup3Fault")!="true"||UnityEngine.XR.XRSettings.isDeviceActive)return;foreach(string id in Gameplay.OwnedWeaponActionIds)Gameplay.RevokeNetworkOwned(id);}
  internal void Pickup3QueueDrop(string id){if(R25NetBot.Arg("-ashfallNetBotPickup3")=="true"&&!UnityEngine.XR.XRSettings.isDeviceActive)QueueDrop(id);}
 }
}
#endif

#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;using Ashfall.Core;using Ashfall.Modules.HealthStates;using Ashfall.Integration.Playable.HandsWeapons;using UnityEngine;
namespace Ashfall.Integration.Playable.Networking {
 public sealed partial class R24LanSession {
  float CombatTrace(Float3 origin,Float3 direction,float range){float nearest=range;foreach(var hit in Physics.RaycastAll(new Vector3(origin.X,origin.Y,origin.Z),new Vector3(direction.X,direction.Y,direction.Z),range,(1<<0)|(1<<8),QueryTriggerInteraction.Ignore)){Ashfall.Modules.Construction.PlacedPanel panel;if(root!=null&&root.Activities!=null&&root.Activities.TryReadPanel(hit.collider,out panel))continue;nearest=Mathf.Min(nearest,hit.distance);}return nearest;} // Shared panels are intersected immediately by the host domain, including before native projection.
  void SendVolley(string profile,int side,Float3 origin,Float3[] rays){if(peer!=null){PublishRawTracking();peer.Lobby.SubmitShot(profile,side,origin,rays,CanonicalLoot(root.Gameplay.ActiveWeaponActionId));}}
  void SendReload(string profile,int side){if(peer!=null)peer.Lobby.SubmitReload(CanonicalLoot(root.Gameplay.ActiveWeaponActionId),side);}
  public R24ReviveTarget[] ReviveTargets(){var result=new List<R24ReviveTarget>();if(peer==null)return result.ToArray();var lobby=peer.Lobby;R24LanHealthState own;if(!lobby.TryReadHealth(lobby.OwnActor,out own)||own.Team<0)return result.ToArray();
   foreach(var member in lobby.Members){R24LanHealthState h;R24LanPoseSnapshot display;if(member.Actor==lobby.OwnActor||!lobby.TryReadHealth(member.Actor,out h)||h.Team!=own.Team||(h.Life!=LifeState.Downed&&!pendingRevives.Contains(member.Actor))||!lobby.TryReadPresence(member.Actor,out display)||display.Pose.Scene!="map:"+root.Map.ManifestSha256)continue;
    var q=display.Pose.Root.Rotation;var rotation=new Quaternion(q.X,q.Y,q.Z,q.W);var center=display.Pose.Head.Position;center=new Float3(center.X,center.Y-.6f,center.Z);var right=rotation*Vector3.right;var forward=rotation*Vector3.forward;var up=rotation*Vector3.up;
    var basis=new R23ShotPose(center,new Float3(right.x,right.y,right.z),new Float3(up.x,up.y,up.z),new Float3(forward.x,forward.y,forward.z));result.Add(new R24ReviveTarget(member.Actor,basis,new Float3(center.X-right.x*.12f,center.Y,center.Z-right.z*.12f),new Float3(center.X+right.x*.12f,center.Y,center.Z+right.z*.12f)));}
   return result.ToArray();}
  readonly HashSet<string> pendingRevives=new HashSet<string>();
  // A queued request is not a completed revive. Consume local charge only after
  // the host's replicated health reports the ally alive.
  public bool CompleteSharedRevive(string actor){if(peer==null)return false;R24LanHealthState h;if(pendingRevives.Contains(actor)&&peer.Lobby.TryReadHealth(actor,out h)&&h.Life==LifeState.Alive){pendingRevives.Remove(actor);return true;}double now=Time.realtimeSinceStartupAsDouble,last;string key="revive/"+actor;if(!lootRequests.TryGetValue(key,out last)||now-last>=.5){lootRequests[key]=now;PublishRawTracking();if(peer.Lobby.RequestRevive(actor))pendingRevives.Add(actor);}return false;}

 }
}
#endif

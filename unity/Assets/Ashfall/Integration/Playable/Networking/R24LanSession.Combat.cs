#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;using Ashfall.Core;using Ashfall.Modules.HealthStates;using Ashfall.Integration.Playable.HandsWeapons;using UnityEngine;
namespace Ashfall.Integration.Playable.Networking {
 public sealed partial class R24LanSession {
  float CombatTrace(Float3 origin,Float3 direction,float range){float nearest=range;foreach(var hit in Physics.RaycastAll(new Vector3(origin.X,origin.Y,origin.Z),new Vector3(direction.X,direction.Y,direction.Z),range,(1<<0)|(1<<8),QueryTriggerInteraction.Ignore)){if(root!=null&&root.Activities!=null&&root.Activities.IsBuildCollider(hit.collider))continue;nearest=Mathf.Min(nearest,hit.distance);}return nearest;} // Shared panels are intersected immediately by the host domain, including before native projection.
  static Vector3 BulletPoint(Float3 a){return new Vector3(a.X,a.Y,a.Z);}
  readonly RaycastHit[] firearmHits=new RaycastHit[32];
  R26BulletSurface FirearmSurface(Float3 origin,Float3 direction,float range,double t0,double t1){
   var a=BulletPoint(origin);var d=BulletPoint(direction);int mask=(1<<0)|(1<<8)|(1<<World.R26RangeLane.MoverLayer);
   var hits=firearmHits;int n=Physics.RaycastNonAlloc(a,d,hits,range,mask,QueryTriggerInteraction.Ignore);
   if(n==hits.Length){hits=Physics.RaycastAll(a,d,range,mask,QueryTriggerInteraction.Ignore);n=hits.Length;}
   var result=new R26BulletSurface{Distance=range+1};
   for(int i=0;i<n;i++){var hit=hits[i];if(hit.distance>=result.Distance)continue;
    if(root.Activities!=null&&root.Activities.IsBuildCollider(hit.collider))continue;
    byte tag=root.RangeLane==null?(byte)0:root.RangeLane.ContactId(hit.collider);if(hit.collider.gameObject.layer==World.R26RangeLane.MoverLayer&&tag!=255)continue;
    result.Distance=hit.distance;result.Normal=V(hit.normal);result.RangeTarget=tag;}
   if(root.RangeLane!=null)root.RangeLane.SweepMovers(a,d,range,t0,t1,ref result);return result;
  }
  void PresentMountedBullet(string mount,Vector3 origin,Vector3 direction){
   R24LanBallisticProfile gun;if(!R24LanBallisticProfile.TryRead("m60",out gun))return;
   var lobby=peer==null?null:peer.Lobby;root.Gameplay.ShowMountedFlight(origin,direction.normalized,gun,lobby==null?null:lobby.OwnActor,lobby==null?0:lobby.LastLocalGameCommand,lobby==null?(System.Action<Collider,float>)MountedContact:null);
  }
  void MountedContact(Collider collider,float damage){if(root.Activities!=null&&root.Activities.IsBuildCollider(collider))root.Activities.TryDamageCollider(collider,damage);}
  void SendVolley(string profile,int side,Float3 origin,Float3[] rays){if(peer!=null){lastLocalVolley=Time.realtimeSinceStartupAsDouble;PublishRawTracking();bool submitted=peer.Lobby.SubmitShot(profile,side,origin,rays,CanonicalLoot(root.Gameplay.ActiveWeaponActionId));root.Gameplay.PredictedVolleyId=submitted?peer.Lobby.AmmoPredictionSequence:0;root.Gameplay.PredictedActor=peer.Lobby.OwnActor;}}
  void SendReload(string profile,int side){if(peer!=null)peer.Lobby.SubmitReload(CanonicalLoot(root.Gameplay.ActiveWeaponActionId),side);}
  public R24ReviveTarget[] ReviveTargets(){var result=new List<R24ReviveTarget>();if(peer==null)return result.ToArray();var lobby=peer.Lobby;R24LanHealthState own;if(!lobby.TryReadHealth(lobby.OwnActor,out own)||own.Team<0)return result.ToArray();
   foreach(var member in lobby.Members){R24LanHealthState h;R24LanPoseSnapshot display;if(member.Actor==lobby.OwnActor||!lobby.TryReadHealth(member.Actor,out h)||(h.Team!=own.Team&&(lobby.SelectedMap==null||lobby.SelectedMap.Rules==null||!lobby.SelectedMap.Rules.EnemyRevive))||h.Life!=LifeState.Downed||!lobby.TryReadPresence(member.Actor,out display)||display.Pose.Scene!="map:"+root.Map.ManifestSha256)continue;
    var q=display.Pose.Root.Rotation;var rotation=new Quaternion(q.X,q.Y,q.Z,q.W);var center=display.Pose.Head.Position;center=new Float3(center.X,center.Y-.6f,center.Z);var right=rotation*Vector3.right;var forward=rotation*Vector3.forward;var up=rotation*Vector3.up;
    var basis=new R23ShotPose(center,new Float3(right.x,right.y,right.z),new Float3(up.x,up.y,up.z),new Float3(forward.x,forward.y,forward.z));result.Add(new R24ReviveTarget(member.Actor,basis,new Float3(center.X-right.x*.12f,center.Y,center.Z-right.z*.12f),new Float3(center.X+right.x*.12f,center.Y,center.Z+right.z*.12f)));}
   return result.ToArray();}
  // Publish the contact sample immediately, including between periodic presence frames.
  public bool CompleteSharedRevive(string actor){if(peer==null)return false;double now=Time.realtimeSinceStartupAsDouble,last;string key="revive/"+actor;if(!lootRequests.TryGetValue(key,out last)||now-last>=.15){lootRequests[key]=now;PublishRawTracking();peer.Lobby.RequestRevive(actor);}return false;}

 }
}
#endif

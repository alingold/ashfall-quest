#if UNITY_6000_0_OR_NEWER
using System.Globalization;using Ashfall.Integration.Playable.Networking;using Ashfall.Integration.Playable.UI;using UnityEngine;
namespace Ashfall.Integration.Playable
{
 public sealed partial class R25NetBot
 {
  bool GrapplePvpEnabled{get{return Arg("-ashfallNetBotGrapple").Length>0;}}
  bool GrappleRoomEnabled{get{return Arg("-ashfallNetBotGrappleRoom")=="true";}}
  bool grappleObserved,grappleReleased,grappleVisible,grappleWas;float grappleTravel,nextGrappleCatalog,nextGrappleLog,nextGrappleState;Vector3 grappleStart;
  void AimGrapplePvp(R24LanPoseSnapshot other,ref Vector2 move,ref float height,ref Vector3 left,ref Vector3 right,ref Quaternion aim,ref float trigger,ref float grip)
  {
   if(other==null||!GrappleRoomEnabled&&playBegan<0)return;bool shooter=Arg("-ashfallNetBotGrapple")=="shooter";
   float elapsed=Time.realtimeSinceStartup-playBegan;Vector3 toward=Vector3.ProjectOnPlane(V(other.Pose.Root.Position)-root.Rig.transform.position,Vector3.up);
   Vector3 walk=root.Rig.transform.InverseTransformDirection(toward);move=Vector2.zero;
   if(toward.magnitude>9)move=Vector2.ClampMagnitude(new Vector2(walk.x,walk.z)*.3f,.6f);
   if(!shooter&&toward.magnitude<6&&!root.Rig.BeingGrappled)move=Vector2.ClampMagnitude(new Vector2(-walk.x,-walk.z),.3f);
   if(elapsed<1||elapsed%12<.25f)move=Vector2.zero;
   height=1.7f;left=root.Rig.Head.transform.position+root.Rig.transform.rotation*new Vector3(-.32f,-.35f,.35f);right=root.Rig.Head.transform.position+root.Rig.transform.rotation*new Vector3(.32f,-.35f,.35f);
   Vector3 point=V(other.Pose.Head.Position)-Vector3.up*.45f;aim=Quaternion.LookRotation(point-right);
   if(!GrappleRoomEnabled)aim*=Quaternion.Inverse(HandsWeapons.R23TrackedGripPose.LocalRotation);
   grip=GrappleRoomEnabled&&shooter&&elapsed>2?1:0;trigger=shooter&&elapsed>4&&elapsed%8>=2&&elapsed%8<3?1:0;
   if(Time.realtimeSinceStartup>=nextGrappleState){nextGrappleState=Time.realtimeSinceStartup+2;Write("grapple-input","distance="+toward.magnitude+" weapon="+root.Gameplay.ActiveWeaponId+" ready="+root.Rig.LeftInputReady+","+root.Rig.RightInputReady+" traversal="+root.Rig.TraversalInputAllowed+" trigger="+trigger+" grip="+grip);}
   if(!GrappleRoomEnabled&&shooter&&!root.Gameplay.IsGrapple&&Time.realtimeSinceStartup>=nextGrappleCatalog){
    nextGrappleCatalog=Time.realtimeSinceStartup+2;root.Gameplay.DropCurrent(right,aim);
    bool queued=root.LanSession.QueueCatalog(new R24CreativeRequest("weapon","grapple",1,2,false),1,right);Write("grapple-catalog","queued="+queued);
   }
  }
  void ObserveGrapplePvp()
  {
   if(!GrapplePvpEnabled||root.LanSession.Lobby==null)return;var lobby=root.LanSession.Lobby;bool active=false;
   foreach(var row in lobby.GrapplePulls){active=true;grappleObserved=true;if(!grappleWas){grappleStart=root.Rig.transform.position;Capture(100);}
    if(row.Victim==lobby.OwnActor)grappleTravel=Mathf.Max(grappleTravel,Vector3.Distance(root.Rig.transform.position,grappleStart));
   }
   if(active){foreach(var line in root.LanSession.GetComponentsInChildren<LineRenderer>())if(line.name=="Grapple player tether"&&line.enabled){grappleVisible=true;break;}}
   if(grappleWas&&!active){grappleReleased=true;Capture(102);Write("grapple-ended","travel="+grappleTravel.ToString("F3",CultureInfo.InvariantCulture)+" rejected="+lobby.RejectedRawTracking+":"+lobby.LastRawTrackingRejection);}
   if(active&&Time.realtimeSinceStartup>=nextGrappleLog){nextGrappleLog=Time.realtimeSinceStartup+.2f;Capture(101);Write("grapple-active","being="+root.Rig.BeingGrappled+" velocity="+root.Rig.MotionVelocity+" travel="+grappleTravel.ToString("F3",CultureInfo.InvariantCulture)+" tether="+grappleVisible+" observed="+grappleObserved+" released="+grappleReleased);}
   grappleWas=active;
  }
 }
}
#endif

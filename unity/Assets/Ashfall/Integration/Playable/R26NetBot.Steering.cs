#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using Ashfall.Core;
using Ashfall.Integration.Playable.Networking;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R23PlayableLoop
    {
        internal bool NetBotSafeWaypoint(Vector3 here,out Vector3 goal)
        {
            goal=here;
            if(!MatchActive||!ActualMatchRules.ZoneEnabled||zoneFrame==null||MatchZoneOwner==null||MatchZoneOwner.State==null)return false;
            var square=MatchZoneOwner.State.Current(MatchClock);
            Vector3 local=zoneFrame.InverseTransformPoint(here);double x,z;
            if(!R26NetBotSteeringRules.SafeWaypoint(local.x,-local.z,square.X,square.Z,square.Size,out x,out z))return false;
            // Continue into the interior instead of hovering on the moving boundary.
            goal=zoneFrame.TransformPoint(new Vector3((float)((x+square.X+square.Size*.5)*.5),local.y,(float)(-(z+square.Z+square.Size*.5)*.5)));return true;
        }
    }
    public sealed partial class R25NetBot
    {
        R24LanLobby heardLobby;long heardEpoch;double heardTime;Vector3 heardAt;
        void HearGunshot(R26RemoteVolley shot)
        {
            var lobby=root.LanSession.Lobby;if(lobby==null)return;
            Vector3 at=V(shot.Origin);
            if(!R26NetBotSteeringRules.Heard((at-root.Rig.transform.position).sqrMagnitude,lobby.EstimatedHostTime-shot.Time))return;
            heardAt=at;heardTime=shot.Time;heardLobby=lobby;heardEpoch=lobby.Epoch;
        }
        float scanSupplyAt;R24LanSupplyState supply;
        bool EmptyReserveGoal(R24LanLobby lobby,float now,Vector3 here,out Vector3 goal)
        {
            var gun=root.Gameplay;goal=here;
            if(!gun.HasWeapon||gun.Ammo!=0||gun.Reserve!=0||gun.ReloadStage!=Ashfall.Modules.WeaponHandling.ReloadPhase.NeedsMagazine){supply=null;return false;}
            if(now>=scanSupplyAt){
                scanSupplyAt=now+1;supply=null;
                foreach(string id in gun.OwnedWeaponActionIds){
                    if(id==gun.ActiveWeaponActionId||AiRange(gun.ResolveWeaponProfile(id))<=0)continue;
                    if((gun.ReadWeaponState(id).Loaded>0||gun.ReadReserve(id)!=0)&&root.NetBotEquipOwned(id)){Write("equip-owned",id);return false;}
                }
                string family=Ashfall.Integration.Playable.HandsWeapons.R23HandsWeapons.WeaponAmmoFamily(gun.ActiveWeaponId);
                float nearest=float.MaxValue;
                foreach(var box in lobby.SharedSupplies){
                    if(box.Family!=family||box.Collector!="")continue;
                    Vector3 at=V(box.Position),safe;float distance=(at-here).sqrMagnitude;
                    if(distance>=nearest||root.NetBotSafeWaypoint(at,out safe))continue;
                    nearest=distance;supply=box;
                }
            }
            R24LanSupplyState current;
            if(supply==null||!lobby.TryReadSharedSupply(supply.Id,out current)||current.Collector!="")return false;
            goal=V(current.Position);return true;
        }
        void SteerMatch(R24LanLobby lobby,float now,bool visible,ref Vector2 move,ref Vector3 left,ref float leftGrip,ref string step)
        {
            if(!root.MatchActive)return;
            Vector3 here=root.Rig.transform.position,safe;Float3 mark;
            bool retreat=root.NetBotSafeWaypoint(here,out safe);
            bool ping=lobby.TryReadTeamPing(out mark);
            bool heard=heardLobby==lobby&&heardEpoch==lobby.Epoch&&R26NetBotSteeringRules.Heard((here-heardAt).sqrMagnitude,lobby.EstimatedHostTime-heardTime);
            var intent=R26NetBotSteeringRules.Choose(retreat,step=="help-downed-ally",ping,visible,heard);
            Vector3 goal;
            if(intent==R26NetBotSteeringRules.Intent.Existing){
                if(step=="help-downed-ally"||!EmptyReserveGoal(lobby,now,here,out goal))return;
                step="collect-ammunition";
                if((here-goal).sqrMagnitude<1.5f*1.5f){left=goal;leftGrip=(int)(now*2)%2==0?.85f:0;}
            }
            else if(intent==R26NetBotSteeringRules.Intent.Zone){goal=safe;step="return-to-zone";}
            else if(intent==R26NetBotSteeringRules.Intent.Ping){goal=V(mark);step="follow-team-ping";}
            else{goal=heardAt;step="investigate-gunshot";}
            Vector3 toward=Vector3.ProjectOnPlane(goal-here,Vector3.up);
            if(toward.sqrMagnitude<(step=="collect-ammunition"?.55f*.55f:1.5f*1.5f)||now-playBegan<1||(now-playBegan)%12<.25f){move=Vector2.zero;return;}
            Vector3 origin=here+Vector3.up*.6f,direction=toward.normalized;
            for(int i=0;i<5;i++){
                int turn=i==0?0:(i%2==0?-1:1)*((i+1)/2)*45;
                Vector3 walk=Quaternion.AngleAxis(turn,Vector3.up)*direction;
                if(!AiClear(origin,origin+walk*1.4f))continue;
                Vector3 local=root.Rig.transform.InverseTransformDirection(walk);move=new Vector2(local.x,local.z)*.85f;return;
            }
            move=Vector2.zero;
        }
    }
}
#endif

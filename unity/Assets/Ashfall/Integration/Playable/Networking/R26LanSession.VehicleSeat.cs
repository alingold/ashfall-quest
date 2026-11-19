#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Integration.Playable.Vehicles;
using UnityEngine;
namespace Ashfall.Integration.Playable.Networking
{
    // r19 VEHICLES19: network side of the eject handle and the helicopter collective.
    // Own rider: the rig draws its own handle; this sends the pull and collective (vehicle.fire/1 mode 4) and routes a handle exit through the host's check.
    // Remote riders: draws their handle from the host's replicated pull (vehicle.state/1).
    public sealed partial class R24LanSession
    {
        sealed class SeatHandleView{internal R26VehicleEjectHandleView View;internal float Pull,Last;}
        readonly Dictionary<string,SeatHandleView> seatViews=new Dictionary<string,SeatHandleView>();
        string sentSeatVehicle;float sentSeatPull=-1,sentSeatCollective=9,nextSeatSend;
        void BindVehicleSeat()
        {
            var inner=root.Rig.DismountVehicle;
            root.Rig.DismountVehicle=delegate(string id,Float3 rest,float yaw){
                if(peer!=null&&root.Rig.DismountedByHandle){
                    float pull=root.Rig.EjectHandlePull;
                    peer.Lobby.RequestVehicleSeat(id,pull,0);peer.Lobby.RequestVehicleDismountHandle(id,rest,yaw,pull);sentSeatVehicle=null;return;
                }
                if(inner!=null)inner(id,rest,yaw);sentSeatVehicle=null;
            };
        }
        void ClearVehicleSeats()
        {
            foreach(var v in seatViews.Values)if(v.View!=null)Destroy(v.View.gameObject);seatViews.Clear();sentSeatVehicle=null;
        }
        void SyncVehicleSeats(bool enabled,bool tanks,bool helicopters)
        {
            var rig=root==null?null:root.Rig;if(rig==null)return;
            string own=peer==null?"local":peer.Lobby.OwnActor,ownId=rig.RidingVehicleId;
            if(peer!=null&&ownId!=null&&rig.EjectHandleVisible){
                float pull=rig.EjectHandlePull,collective=rig.RidingKind=="helicopter"?rig.VehicleCollective:0;
                bool changed=ownId!=sentSeatVehicle||Mathf.Abs(pull-sentSeatPull)>.004f||Mathf.Abs(collective-sentSeatCollective)>.03f;
                if(changed&&Time.unscaledTime>=nextSeatSend&&peer.Lobby.RequestVehicleSeat(ownId,pull,collective)){sentSeatVehicle=ownId;sentSeatPull=pull;sentSeatCollective=collective;nextSeatSend=Time.unscaledTime+.1f;}
            }else sentSeatVehicle=null;
            var live=new HashSet<string>();
            if(enabled)foreach(var row in bikeRows){
                if(row.Rider==""||row.Rider==own||row.Destroyed||row.Kind=="tank"&&!tanks||row.Kind=="helicopter"&&!helicopters)continue;
                R24LanPoseSnapshot pose;
                if(peer==null||!peer.Lobby.TryReadPresence(row.Rider,out pose)||!pose.Pose.Riding||pose.Pose.Scene!=PresenceScene||Time.realtimeSinceStartupAsDouble-pose.ReceivedAt>1)continue;
                SeatHandleView seat;if(!seatViews.TryGetValue(row.Id,out seat)){seat=new SeatHandleView{View=R26VehicleEjectHandleView.Create(null,"R26 remote eject handle / "+row.Id)};seatViews.Add(row.Id,seat);}
                live.Add(row.Id);
                var q=pose.Pose.Root.Rotation;var forward=Quaternion.Euler(0,R25BikeRider.Yaw(q.X,q.Y,q.Z,q.W),0)*Vector3.forward;var head=pose.Pose.Head.Position;
                Vector3 rest=new Vector3(head.X,head.Y+R26VehicleSeat.HandleAboveHead,head.Z)+forward*R26VehicleSeat.HandleForward;
                seat.Pull=Mathf.MoveTowards(seat.Pull,row.HandlePull,Time.unscaledDeltaTime*1.2f);
                if(!R26VehicleSeat.PullEjects(seat.Last)&&R26VehicleSeat.PullEjects(row.HandlePull))seat.View.Click();
                seat.Last=row.HandlePull;
                seat.View.Show(rest,forward,seat.Pull,row.HandlePull>.002f,true);
            }
            List<string> gone=null;foreach(var id in seatViews.Keys)if(!live.Contains(id)){if(gone==null)gone=new List<string>();gone.Add(id);}
            if(gone!=null)foreach(var id in gone){if(seatViews[id].View!=null)Destroy(seatViews[id].View.gameObject);seatViews.Remove(id);}
        }
    }
}
#endif

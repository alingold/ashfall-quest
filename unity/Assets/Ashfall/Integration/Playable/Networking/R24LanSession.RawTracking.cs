#if UNITY_6000_0_OR_NEWER
using Ashfall.Core;using UnityEngine;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanSession
    {
        bool RawTrackingWorldClear(Float3 a,Float3 b)
        {
            if(root==null||!root.Ready||!root.Map.IsLoaded)return false;
            Vector3 from=new Vector3(a.X,a.Y,a.Z),delta=new Vector3(b.X-a.X,b.Y-a.Y,b.Z-a.Z);
            // Source net_bridge1000._clear uses world1|8, excluding the local
            // player. Unity's map/build layers are0|8; rig, art and corpses absent.
            return delta.sqrMagnitude<.0001f||!Physics.Raycast(from,delta.normalized,delta.magnitude,(1<<0)|(1<<8),QueryTriggerInteraction.Ignore);
        }
        void PublishRawTracking()
        {
            // Carrier motion needs the source server-owned pod path and spawn
            // revision; do not accept its client path as combat evidence.
            if(peer==null||root==null||!root.Ready||!root.Map.IsLoaded||root.Lobby!=null&&root.Lobby.Visible||root.Rig.CarrierActive||!root.Rig.Tracking||!root.Rig.SessionFocused)return;
            R24LanSpawn spawn;if(!peer.Lobby.TryReadSpawn(peer.Lobby.OwnActor,out spawn)||!root.ApplyLanSpawn(spawn))return;
            bool left=root.Rig.LeftInput.Tracked,right=root.Rig.RightInput.Tracked;
            PublishEquipment();PublishFoodEquipment();PublishGrenadeEquipment();peer.Lobby.PublishRawTracking(new R24LanRawTracking(root.Map.ManifestSha256,(byte)(1|(left?2:0)|(right?4:0)|(root.Paddles!=null&&root.Paddles.State.Active?8:0)),T(root.Rig.transform),T(root.Rig.Head.transform),T(root.Rig.Left),T(root.Rig.Right),left&&!root.Climbing.LeftAnchored?Mathf.Clamp01(root.Rig.LeftInput.Grip):0,right&&!root.Climbing.RightAnchored?Mathf.Clamp01(root.Rig.RightInput.Grip):0,spawn.Revision));
        }
    }
}
#endif

#if UNITY_6000_0_OR_NEWER
using Ashfall.Integration.Playable.Networking;using UnityEngine;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R23PlayableLoop
    {
        string appliedLanSpawnSession;long appliedLanSpawnEpoch=-1;int appliedLanSpawnRevision;
        public bool ApplyLanSpawn(R24LanSpawn state)
        {
            if(state==null||!Ready||!Map.IsLoaded||Map.ManifestSha256!=state.MapHash||LanSession==null||LanSession.Lobby==null||LanSession.Lobby.OwnActor!=state.Actor||LanSession.Lobby.Epoch!=state.Epoch||!LanSession.Lobby.OwnsSpawn(state))return false;
            string session=LanSession.Lobby.SessionId;if(appliedLanSpawnSession==session&&appliedLanSpawnEpoch==state.Epoch&&appliedLanSpawnRevision==state.Revision)return true;
            activeSpawn=new Vector3(state.Feet.X,state.Feet.Y,state.Feet.Z);Rig.SetSpawn(activeSpawn,Map.Bounds.center);Calibration.NotifyRootPoseReset();appliedLanSpawnSession=session;appliedLanSpawnEpoch=state.Epoch;appliedLanSpawnRevision=state.Revision;return true;
        }
    }
}
#endif

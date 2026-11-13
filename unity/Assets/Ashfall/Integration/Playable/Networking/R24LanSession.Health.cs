#if UNITY_6000_0_OR_NEWER
using Ashfall.Core;using Ashfall.Integration.Playable.MatchZone;using UnityEngine;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanSession
    {
        void SyncHealth(){if(root!=null&&root.Gameplay!=null&&root.Gameplay.PlayerHealthViewBound)root.RefreshLanPlayerHealth();}
        // Host-only zone owner port for remote members. Root.Update (order -100)
        // has already stepped the SAME R24MatchZoneHost this frame; the lobby
        // supplies only authenticated current-revision raw roots.
        bool RemoteZoneDamage(string actor,Float3 worldRoot,double protectionUntil,out float amount)
        {
            amount=0;if(peer==null||root==null||!root.Ready||!root.LanRoundBound||!root.MatchActive||root.MatchZone==null||root.MatchZoneOwner==null||!ReferenceEquals(root.MatchZone.Host,root.MatchZoneOwner))return false;
            R24ZoneDamage request;if(!root.MatchZone.TryRequestActorDamage(actor,new Vector3(worldRoot.X,worldRoot.Y,worldRoot.Z),protectionUntil,out request))return false;
            amount=request.Amount;return true;
        }
    }
}
#endif

#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public sealed partial class R23HandsWeapons
    {
        // MP-HITMARKER: the same source hit cue a local practice-target hit plays, driven
        // only by a host-confirmed hit on a remote player (never by a local trace).
        public bool PlayConfirmedHitCue(){if(!Ready||audioCues==null)return false;audioCues.Play(ActiveWeaponId,"hit",false);return true;}
        // MP-RETICLE: remote avatar heads the LAN session publishes each frame. Fixed
        // capacity, no allocation; entries are ignored once the session stops refreshing.
        readonly Transform[] remoteAimHeads=new Transform[R26RemoteAimVolumes.Capacity];int remoteAimCount,remoteAimFrame=-1000;
        public int RemoteAimCount{get{return Time.frameCount-remoteAimFrame>2?0:remoteAimCount;}}
        public void BeginRemoteAim(){for(int i=0;i<remoteAimCount;i++)remoteAimHeads[i]=null;remoteAimCount=0;remoteAimFrame=Time.frameCount;}
        public bool AddRemoteAim(Transform head){if(head==null||remoteAimCount>=remoteAimHeads.Length)return false;remoteAimHeads[remoteAimCount++]=head;return true;}
        // Shortens an unblocked physics aim point to the nearest remote avatar volume on the ray.
        Vector3 RemoteAimPoint(Vector3 origin,Vector3 direction,Vector3 point)
        {
            int count=RemoteAimCount;if(count==0)return point;float length=direction.magnitude;if(!(length>1e-6f))return point;
            Vector3 unit=direction/length;float limit=Vector3.Dot(point-origin,unit),best=limit;
            for(int i=0;i<count;i++){var head=remoteAimHeads[i];if(head==null)continue;Vector3 h=head.position;float t=R26RemoteAimVolumes.Nearest(origin.x,origin.y,origin.z,unit.x,unit.y,unit.z,h.x,h.y,h.z);if(t<best)best=t;}
            return best<limit?origin+unit*best:point;
        }
    }
}
#endif

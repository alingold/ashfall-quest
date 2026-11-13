#if UNITY_6000_0_OR_NEWER
using Ashfall.Core;
using Ashfall.Integration.Playable.Match;
using Ashfall.Integration.Playable.MatchZone;
using Ashfall.Integration.Playable.TeamLaunch;
using UnityEngine;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R23PlayableLoop
    {
        long ghostRecoveryEpoch=-1;double ghostRecoveryCheck;
        public int GhostRecoveryCount{get;private set;}
        public Vector3 LastGhostRecoveryFrom{get;private set;}
        public Vector3 LastGhostRecoveryTo{get;private set;}
        public bool GhostRecoveryUsedSourceFallback{get;private set;}
        public bool GhostRecoverySupported{get{return Map!=null&&(R24GhostRecoveryMath.Supported(Map.MapId)||ArchiveGhostRecoveryReady);}}
        public string GhostRecoveryLimit{get{return GhostRecoverySupported?null:ArchiveGhostRecoveryFailure??"Preparing actual source archive recovery sites";}}
        void TickGhostRecovery()
        {
            if(!MatchActive||LanRoundBound)return;PrepareArchiveGhostRecovery(); // LAN relocations require the actual host revision owner.
            if(ghostRecoveryEpoch!=MatchEpoch){ghostRecoveryEpoch=MatchEpoch;ghostRecoveryCheck=0;GhostRecoveryCount=0;}
            if(MatchClock-ghostRecoveryCheck<.10)return;ghostRecoveryCheck=MatchClock;
            if(MatchLocalGhost&&Map.MapId.StartsWith("archive:",System.StringComparison.Ordinal)){TryRecoverArchiveGhost();return;}
            if(!MatchLocalGhost||!GhostRecoverySupported)return;
            R24LaunchMapInfo info=null;float removedY=LaunchMapCatalog!=null&&LaunchMapCatalog.TryRead(Map.MapId,out info)?info.RemovedOriginGodot.Y:0;
            if(Map.MapId=="cyber_bird"&&info==null)return;
            var local=Map.transform.InverseTransformPoint(Rig.transform.position);
            if(!R24GhostRecoveryMath.Outside(Map.MapId,local.y+removedY))return;
            Vector3 origin=info==null?Map.transform.position:Map.transform.TransformPoint(MatchV(info.RepresentedOriginUnity));
            R24ZoneSquare? next=MatchZoneOwner!=null&&MatchZoneOwner.State!=null?(R24ZoneSquare?)MatchZoneOwner.State.Next:null;
            var candidates=R24GhostRecoveryMath.Candidates(Map.MapId,new Float3(Map.GameSpawn.x,Map.GameSpawn.y,Map.GameSpawn.z),new Float3(origin.x,origin.y,origin.z),next);
            int offset=Random.Range(0,candidates.Length);Vector3 selected=Map.GameSpawn;bool accepted=false;
            // Source listen-host id1; adding a uniform random offset gives the
            // same candidate order with only this actual local actor present.
            for(int i=0;i<candidates.Length;i++)
            {
                Vector3 candidate=MatchV(candidates[R24GhostRecoveryMath.SourceOrder(i,candidates.Length,offset,1)]),point;
                if(!GhostRecoveryFloor(candidate,out point)||!GhostRecoveryClear(point))continue;
                selected=point;accepted=true;break;
            }
            Vector3 from=Rig.transform.position;
            if(!Rig.RelocateSourceGhost(selected))return;
            Calibration.NotifyRootPoseReset();LastGhostRecoveryFrom=from;LastGhostRecoveryTo=selected;GhostRecoveryUsedSourceFallback=!accepted;GhostRecoveryCount++;
        }
        bool GhostRecoveryTerrain(Collider c)
        {
            if(c==null||!c.enabled||c.isTrigger||!c.gameObject.activeInHierarchy||c.gameObject.layer==19||c.gameObject.layer==22||Gameplay.IsSourceActorLayer4(c))return false;
            return c.transform.IsChildOf(Map.transform)||Activities.StructureRoot!=null&&c.transform.IsChildOf(Activities.StructureRoot)||TeamLaunch!=null&&c.transform.IsChildOf(TeamLaunch.transform);
        }
        bool GhostRecoveryFloor(Vector3 candidate,out Vector3 point)
        {
            point=Vector3.zero;RaycastHit best=new RaycastHit();float nearest=float.PositiveInfinity;
            foreach(var hit in Physics.RaycastAll(candidate+Vector3.up*20,Vector3.down,50,Ashfall.Integration.Playable.Bots.R24RagdollRules.GameplayQueryMask,QueryTriggerInteraction.Ignore))
                if(GhostRecoveryTerrain(hit.collider)&&hit.distance<nearest){best=hit;nearest=hit.distance;}
            if(float.IsPositiveInfinity(nearest)||best.normal.y<.65f)return false;point=best.point+Vector3.up*.2f;return true;
        }
        bool GhostRecoveryClear(Vector3 point)
        {
            float top=Mathf.Max(1.8288f,Calibration.Calibrated?Calibration.Receipt.TargetHeadTop:1.8288f),height=top+.10f;
            foreach(var c in Physics.OverlapCapsule(point+Vector3.up*(.02f+.27f),point+Vector3.up*(height+.02f-.27f),.27f,Ashfall.Integration.Playable.Bots.R24RagdollRules.GameplayQueryMask,QueryTriggerInteraction.Ignore))if(GhostRecoveryTerrain(c))return false;
            return true;
        }
    }
}
#endif

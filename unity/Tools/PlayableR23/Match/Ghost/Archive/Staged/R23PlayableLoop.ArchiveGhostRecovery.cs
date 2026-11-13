#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Integration.Playable.Match;
using Ashfall.Integration.Playable.TeamLaunch;
using Ashfall.Integration.Playable.World;
using UnityEngine;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R23PlayableLoop
    {
        readonly R24ArchiveRecoveryCatalog archiveRecoveryCatalog=new R24ArchiveRecoveryCatalog();
        R24ArchiveRecoveryInfo archiveRecoveryInfo;R24LaunchMapInfo archiveRecoveryFrame;R24ArchiveSpawnPlan archiveSpawnPlan;
        Coroutine archiveSpawnRoutine;long archiveSpawnEpoch=-1;string archiveRecoveryFailure;
        public bool ArchiveGhostRecoveryReady{get{return archiveSpawnEpoch==MatchEpoch&&archiveRecoveryInfo!=null&&Map.MapId==archiveRecoveryInfo.Id&&archiveSpawnPlan!=null&&archiveSpawnPlan.Ready;}}
        public string ArchiveGhostRecoveryFailure{get{return archiveRecoveryFailure??archiveRecoveryCatalog.Failure;}}
        public R24ArchiveSpawnPlan ArchiveGhostSpawnPlan{get{return ArchiveGhostRecoveryReady?archiveSpawnPlan:null;}}
        public R24ArchiveRecoveryInfo ArchiveGhostSourceInfo{get{return ArchiveGhostRecoveryReady?archiveRecoveryInfo:null;}}
        void PrepareArchiveGhostRecovery()
        {
            if(!Map.MapId.StartsWith("archive:",StringComparison.Ordinal))return;
            if(!archiveRecoveryCatalog.Ready){if(!archiveRecoveryCatalog.Loading&&archiveRecoveryCatalog.Failure==null)StartCoroutine(archiveRecoveryCatalog.Load());return;}
            if(archiveSpawnEpoch==MatchEpoch&&archiveRecoveryInfo!=null&&archiveRecoveryInfo.Id==Map.MapId)return;
            R24ArchiveRecoveryInfo info;R24LaunchMapInfo frame;
            if(LaunchMapCatalog==null||!LaunchMapCatalog.Ready||!LaunchMapCatalog.TryRead(Map.MapId,out frame)||!archiveRecoveryCatalog.TryRead(Map.MapId,out info))return;
            if(info.SourceSha256!=frame.SourceSha256||info.ManifestSha256!=frame.ManifestSha256||info.CacheFingerprint!=frame.CacheFingerprint||info.AdapterSha256!=frame.AdapterSha256){archiveRecoveryFailure="Actual archive resource/catalog binding mismatch";return;}
            if(archiveSpawnRoutine!=null)StopCoroutine(archiveSpawnRoutine);
            archiveRecoveryInfo=info;archiveRecoveryFrame=frame;archiveSpawnEpoch=MatchEpoch;archiveRecoveryFailure=null;
            archiveSpawnPlan=new R24ArchiveSpawnPlan(info,new ArchiveRecoveryWorld(this,info,frame));
            archiveSpawnRoutine=StartCoroutine(BuildArchiveGhostPlan(archiveSpawnPlan,MatchEpoch,Map.MapId));
        }
        IEnumerator BuildArchiveGhostPlan(R24ArchiveSpawnPlan plan,long epoch,string mapId)
        {
            var work=plan.Build();
            try
            {
                while(MatchActive&&MatchEpoch==epoch&&Map.MapId==mapId)
                {
                    bool moved;object current=null;try{moved=work.MoveNext();if(moved)current=work.Current;}catch(Exception error){archiveRecoveryFailure=error.ToString();yield break;}
                    if(!moved){archiveRecoveryFailure=plan.Failure;yield break;}yield return current;
                }
            }
            finally{var done=work as IDisposable;if(done!=null)done.Dispose();archiveSpawnRoutine=null;}
        }
        Vector3 ArchiveToWorld(Float3 p,R24LaunchMapInfo frame){return Map.transform.TransformPoint(MatchV(frame.RepresentedOriginUnity)+new Vector3(p.X,p.Y,-p.Z));}
        Float3 ArchiveFromWorld(Vector3 world,R24LaunchMapInfo frame){var p=Map.transform.InverseTransformPoint(world)-MatchV(frame.RepresentedOriginUnity);return new Float3(p.x,p.y,-p.z);}
        bool ArchiveOutside(Float3 point,R24ArchiveRecoveryInfo info,R24LaunchMapInfo frame)
        {
            var world=ArchiveToWorld(point,frame);bool launch=TeamLaunch!=null&&TeamLaunch.Eligible;
            bool exempt=launch&&(TeamLaunch.Staging(world)||TeamLaunch.AuthorizedFlight(world,MatchClock));
            float ceiling=launch?TeamLaunch.Ceiling-Map.transform.TransformPoint(MatchV(frame.RepresentedOriginUnity)).y:float.NegativeInfinity;
            return info.Outside(point,ceiling,exempt);
        }
        bool ArchiveTerrain(Collider c){return GhostRecoveryTerrain(c)&&c.gameObject.layer!=R23PlayableMap.PlayerOnlyLayer;}
        bool ArchiveFloor(Float3 from,float depth,R24LaunchMapInfo frame,out Float3 floor)
        {
            floor=new Float3();RaycastHit best=new RaycastHit();float nearest=float.PositiveInfinity;
            foreach(var hit in Physics.RaycastAll(ArchiveToWorld(from,frame),Vector3.down,depth,Physics.AllLayers,QueryTriggerInteraction.Ignore))if(ArchiveTerrain(hit.collider)&&hit.distance<nearest){best=hit;nearest=hit.distance;}
            if(float.IsPositiveInfinity(nearest)||best.normal.y<=.7f)return false;floor=ArchiveFromWorld(best.point,frame);return true;
        }
        float ArchiveStandingClearance{get{return Mathf.Max(1.8288f,Calibration.Calibrated?Calibration.Receipt.TargetHeadTop:1.8288f)+.10f;}}
        bool ArchiveCapsuleClear(Float3 point,float radius,R24LaunchMapInfo frame)
        {
            var world=ArchiveToWorld(point,frame);float height=ArchiveStandingClearance;
            foreach(var c in Physics.OverlapCapsule(world+Vector3.up*(.02f+radius),world+Vector3.up*(height+.02f-radius),radius,Physics.AllLayers,QueryTriggerInteraction.Ignore))if(ArchiveTerrain(c))return false;return true;
        }
        bool ArchiveHazard(Float3 point,R24ArchiveRecoveryInfo info,R24LaunchMapInfo frame)
        {var at=ArchiveToWorld(point,frame);if(TeamLaunch!=null&&(TeamLaunch.Staging(at)||TeamLaunch.AuthorizedFlight(at,MatchClock)))return false;return info.Hazard(point);}
        bool ArchiveRoom(Float3 floor,bool initial,R24ArchiveRecoveryInfo info,R24LaunchMapInfo frame)
        {
            if(initial&&info.PanelBlocks(floor,ArchiveStandingClearance))return false;
            Float3 at;foreach(var offset in new[]{new Float3(.4f,0,0),new Float3(-.4f,0,0),new Float3(0,0,.4f),new Float3(0,0,-.4f)})
                if(!ArchiveFloor(new Float3(floor.X+offset.X,floor.Y+.6f,floor.Z+offset.Z),1.2f,frame,out at)||Math.Abs(at.Y-floor.Y)>.3f)return false;
            if(!ArchiveCapsuleClear(floor,.3f,frame))return false;
            return !ArchiveHazard(new Float3(floor.X,floor.Y+.1f,floor.Z),info,frame)&&!ArchiveHazard(new Float3(floor.X,floor.Y+.9f,floor.Z),info,frame)&&!ArchiveHazard(new Float3(floor.X,floor.Y+1.7f,floor.Z),info,frame);
        }
        sealed class ArchiveRecoveryWorld:IR24ArchiveRecoveryWorld
        {
            readonly R23PlayableLoop owner;readonly R24ArchiveRecoveryInfo info;readonly R24LaunchMapInfo frame;
            public ArchiveRecoveryWorld(R23PlayableLoop host,R24ArchiveRecoveryInfo data,R24LaunchMapInfo mapFrame){owner=host;info=data;frame=mapFrame;}
            public bool Floor(Float3 from,float depth,out Float3 point){return owner.ArchiveFloor(from,depth,frame,out point);}
            public bool Outside(Float3 point){return owner.ArchiveOutside(point,info,frame);}
            public bool Room(Float3 floor,bool initial){return owner.ArchiveRoom(floor,initial,info,frame);}
        }
        void TryRecoverArchiveGhost()
        {
            if(!ArchiveGhostRecoveryReady)return;var info=archiveRecoveryInfo;var frame=archiveRecoveryFrame;var plan=archiveSpawnPlan;var actual=ArchiveFromWorld(Rig.transform.position,frame);
            if(R24ArchiveMapPolicy.MagnumOnly(info.Id)&&actual.Y<plan.MagnumFallLevel){CommitArchiveGhostRecovery(plan.MagnumCenter,false);return;}
            if(!ArchiveOutside(actual,info,frame))return;
            var candidates=new List<Float3>(plan.Spawns);
            if(Gameplay.LocalMatchHealth.Deaths==0){bool separated=false;foreach(var p in candidates)if(DistanceArchive(p,plan.SourceWarmup)>=144){separated=true;break;}if(!separated)foreach(var p in plan.Loot)candidates.Add(new Float3(p.X,p.Y-.48f,p.Z));}
            bool zone=MatchZoneOwner!=null&&MatchZoneOwner.State!=null;
            if(zone)
            {
                var rect=MatchZoneOwner.State.Next;candidates.RemoveAll(delegate(Float3 p){return p.X<rect.X||p.X>=rect.MaxX||p.Z<rect.Z||p.Z>=rect.MaxZ;});
                if(candidates.Count==0)for(int z=0;z<7;z++)for(int x=0;x<7;x++)candidates.Add(new Float3((float)(rect.X+1+(rect.Size-2)*(x+.5)/7),100,(float)(rect.Z+1+(rect.Size-2)*(z+.5)/7)));
            }
            int random=UnityEngine.Random.Range(0,Math.Max(1,candidates.Count));Float3 selected=plan.SourceSpawn;bool accepted=false;
            for(int i=0;i<candidates.Count;i++)
            {
                var p=candidates[R24GhostRecoveryMath.SourceOrder(i,candidates.Count,random,1)];Float3 floor;
                if(zone&&Math.Abs(p.Y-100)<.001f){float top=info.SolidMax.Y+5;if(!ArchiveFloor(new Float3(p.X,top,p.Z),top-info.SolidMin.Y+10,frame,out floor))continue;p=new Float3(floor.X,floor.Y+.12f,floor.Z);}
                if(!plan.Safe(p,false)||!ArchiveCapsuleClear(p,.27f,frame))continue;selected=p;accepted=true;break;
            }
            CommitArchiveGhostRecovery(selected,!accepted);
        }
        static double DistanceArchive(Float3 a,Float3 b){return(double)(a.X-b.X)*(a.X-b.X)+(double)(a.Y-b.Y)*(a.Y-b.Y)+(double)(a.Z-b.Z)*(a.Z-b.Z);}
        void CommitArchiveGhostRecovery(Float3 sourcePosition,bool fallback)
        {
            var from=Rig.transform.position;var selected=ArchiveToWorld(sourcePosition,archiveRecoveryFrame);if(!Rig.RelocateSourceGhost(selected))return;
            Calibration.NotifyRootPoseReset();LastGhostRecoveryFrom=from;LastGhostRecoveryTo=selected;GhostRecoveryUsedSourceFallback=fallback;GhostRecoveryCount++;
        }
    }
}
#endif

#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Integration.Playable.Match;
using Ashfall.Integration.Playable.Networking;
using Ashfall.Integration.Playable.TeamLaunch;
using UnityEngine;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R23PlayableLoop
    {
        Coroutine sourceArchiveEntry;long sourceArchiveEntryTicket;bool sourceArchiveCommitting,sourceArchiveRoundStartValid;
        R24ArchiveSpawnPlan entrySpawnPlan;Transform entrySpawnGeometry;Matrix4x4 entrySpawnMatrix;
        Vector3 sourceArchiveRoundStart;string sourceArchiveRoundMap,sourceArchiveRoundManifest;
        public bool SourceArchiveEntryPending{get{return sourceArchiveEntry!=null;}}
        public string SourceArchiveEntryFailure{get;private set;}
        void CancelSourceArchiveEntry()
        {sourceArchiveEntryTicket++;entrySpawnPlan=null;entrySpawnGeometry=null;if(sourceArchiveEntry!=null)StopCoroutine(sourceArchiveEntry);sourceArchiveEntry=null;sourceArchiveCommitting=false;sourceArchiveRoundStartValid=false;}
        bool BeginSourceArchiveEntry()
        {
            if(!Ready||Map==null||!Map.IsLoaded||!Map.MapId.StartsWith("archive:",StringComparison.Ordinal)||ActualMatchRules!=null||sourceArchiveCommitting||LanSession!=null&&LanSession.Lobby!=null)return false;
            if(sourceArchiveEntry!=null)return true;
            SourceArchiveEntryFailure=null;sourceArchiveRoundStartValid=false;roundStarted=false;Rig.DisarmMotion();Gameplay.SetGameplayPaused(true);
            if(Gallery!=null)Gallery.Hide();if(Preview!=null)Preview.Hide();Lobby.Hide();Map.SetWorldVisible(true);UI.BeginLoading("VALIDATING SOURCE SPAWN");
            long ticket=++sourceArchiveEntryTicket;sourceArchiveEntry=StartCoroutine(PrepareSourceArchiveEntry(ticket,Map.MapId,Map.ManifestSha256));return true;
        }
        bool CurrentSourceEntry(long ticket,string map,string hash)
        {return ticket==sourceArchiveEntryTicket&&Ready&&!switchingMap&&Map!=null&&Map.IsLoaded&&Map.MapId==map&&Map.ManifestSha256==hash&&Lobby!=null&&!Lobby.Visible;}
        IEnumerator PrepareSourceArchiveEntry(long ticket,string map,string hash)
        {
            // Ensure the owned coroutine handle and opaque loading gate are live
            // before any asynchronous map-owned planner can finish.
            yield return null;float deadline=Time.realtimeSinceStartup+60;
            try{
                while(CurrentSourceEntry(ticket,map,hash)&&!ArchiveWorldReady){
                    PrepareArchiveWorld(true);string failure=ArchiveWorldFailure;
                    if(!String.IsNullOrEmpty(failure)||Time.realtimeSinceStartup>=deadline){RefuseSourceArchiveEntry(ticket,failure??"Source floor/headroom validation timed out");yield break;}
                    UI.SetLoadingProgress("VALIDATING SOURCE SPAWN",.98f);yield return null;
                }
                if(!CurrentSourceEntry(ticket,map,hash))yield break;
                var info=ArchiveWorldInfo;var plan=ArchiveWorldPlan;R24LaunchMapInfo frame;
                if(info==null||plan==null||!LaunchMapCatalog.TryRead(map,out frame)){RefuseSourceArchiveEntry(ticket,"Actual source spawn inputs unavailable");yield break;}
                Float3 selected=new Float3();bool found=false;
                if(pendingMatch==null){
                    var candidates=new List<Float3>(plan.WarmupSites);candidates.Add(plan.SourceWarmup);
                    foreach(var p in candidates)if(plan.Safe(p,true)&&SourcePlayerVolumeClear(p,frame)){selected=p;found=true;break;}
                }else{
                    int team=pendingMatch.Teams>0?0:-1;
                    var choice=R24LanSourceSpawnSelection.Archive(info,plan,new ArchiveRecoveryWorld(this,info,frame),"player",0,team,pendingMatch.Teams,true,new R24LanSpawnNeighbour[0],null,delegate(int n){return UnityEngine.Random.Range(0,n);},delegate(Float3 p){return SourcePlayerVolumeClear(p,frame);},1);
                    selected=choice.Position;found=choice.Validated;
                }
                Vector3 world;if(!found||!TryArchiveSourceToWorld(selected,out world)){RefuseSourceArchiveEntry(ticket,"No validated authored source spawn is clear");yield break;}
                activeSpawn=world;sourceArchiveRoundStart=world;sourceArchiveRoundStartValid=pendingMatch!=null;sourceArchiveRoundMap=map;sourceArchiveRoundManifest=hash;
                entrySpawnPlan=plan;entrySpawnGeometry=CurrentArchiveGeometry();entrySpawnMatrix=Map.transform.localToWorldMatrix;
                sourceArchiveCommitting=true;StartPlayingCore();sourceArchiveCommitting=false;
            }
            finally{if(ticket==sourceArchiveEntryTicket)sourceArchiveEntry=null;sourceArchiveCommitting=false;}
        }
        void RefuseSourceArchiveEntry(long ticket,string failure)
        {
            if(ticket!=sourceArchiveEntryTicket)return;SourceArchiveEntryFailure=failure;sourceArchiveEntry=null;
            ReturnToLobbyCore();UI.OpenMatchRules();SetMatchStatus("MAP NOT ENTERED | "+failure);
        }
        bool SourcePlayerVolumeClear(Float3 sourceFeet,R24LaunchMapInfo frame)
        {
            Vector3 feet=ArchiveToWorld(sourceFeet,frame);float height=ArchiveStandingClearance;const float radius=.3f;
            return !Physics.CheckCapsule(feet+Vector3.up*(radius+.02f),feet+Vector3.up*(height-radius+.02f),radius,1<<World.R23PlayableMap.PlayerOnlyLayer,QueryTriggerInteraction.Ignore);
        }
        bool TrySourceRoundStart(int round,out Vector3 world)
        {
            world=Map.GameSpawn;
            if(LanRoundBound||R26MapPodPolicy.UsesPods(Map.MapId))return true;
            if(R24ArchiveRoundPolicy.FiveLevels(Map.MapId)&&(round<1||round>5))return false;
            if(round==1&&sourceArchiveCommitting&&sourceArchiveRoundStartValid&&sourceArchiveRoundMap==Map.MapId&&sourceArchiveRoundManifest==Map.ManifestSha256){world=sourceArchiveRoundStart;sourceArchiveRoundStartValid=false;return true;}
            R24ArchiveRecoveryInfo info;R24LaunchMapInfo frame;
            if(!archiveRecoveryCatalog.TryRead(Map.MapId,out info)||!LaunchMapCatalog.TryRead(Map.MapId,out frame)||info.SourceSha256!=Map.SourceSha256||frame.SourceSha256!=info.SourceSha256)return false;
            if(R24ArchiveRoundPolicy.Forcefield(Map.MapId)||R26MapPodPolicy.HasExplicitOrAutoRow(Map.MapId)){
                // Restart changes the clock epoch before the map planner has a frame to rebuild.
                // Reuse its same-geometry candidate list. Archive() repeats live floor, volume and round checks.
                var plan=ArchiveWorldPlan;
                if(plan==null&&sourceArchiveRoundMap==Map.MapId&&sourceArchiveRoundManifest==Map.ManifestSha256&&entrySpawnGeometry!=null&&entrySpawnGeometry==CurrentArchiveGeometry()&&entrySpawnMatrix.Equals(Map.transform.localToWorldMatrix))plan=entrySpawnPlan;
                if(plan==null||!plan.Ready)return false;int team=ActualMatchRules.Teams>0?LocalMatchTeam:-1;
                var choice=R24LanSourceSpawnSelection.Archive(info,plan,new ArchiveRecoveryWorld(this,info,frame),"player",0,team,ActualMatchRules.Teams,true,new R24LanSpawnNeighbour[0],null,delegate(int n){return UnityEngine.Random.Range(0,n);},delegate(Float3 p){return SourcePlayerVolumeClear(p,frame);},round);
                if(!choice.Validated)return false;world=ArchiveToWorld(choice.Position,frame);return true;
            }
            IEnumerable<Float3> hints=R24ArchiveRoundPolicy.FiveLevels(Map.MapId)?(IEnumerable<Float3>)info.Deathmatch:info.Recorded;
            var candidates=R24ArchiveRoundPolicy.RoundSites(info,hints,round);Float3 best=new Float3();double score=Double.NegativeInfinity;bool found=false;
            float sourceY=R24ArchiveRoundPolicy.FiveLevels(Map.MapId)?R24ArchiveRoundPolicy.LevelHeights(info)[round-1]:info.Center.Y;
            var target=ActualMatchRules.Teams>0?R24ArchiveRoundPolicy.TeamTarget(info,sourceY,LocalMatchTeam,ActualMatchRules.Teams,round):info.Center;
            foreach(var hint in candidates){
                Float3 floor;if(!ArchiveFloor(new Float3(hint.X,hint.Y+1.5f,hint.Z),8,frame,out floor)||floor.Y<hint.Y-6||floor.Y>hint.Y+1||ArchiveOutside(floor,info,frame)||!ArchiveRoom(floor,false,info,frame))continue;
                var point=new Float3(floor.X,floor.Y+.12f,floor.Z);if(!R24ArchiveRoundPolicy.OnRoundLevel(info,point,round)||!SourcePlayerVolumeClear(point,frame))continue;
                double x=point.X-target.X,y=point.Y-target.Y,z=point.Z-target.Z,value=-(x*x+y*y+z*z);if(value>score){score=value;best=point;found=true;}
            }
            if(!found)return false;world=ArchiveToWorld(best,frame);return true;
        }
        void RefuseSourceRoundStart()
        {
            Gameplay.EndLocalMatchHealth();localMatchClock.End();ActualMatchRules=null;roundStarted=false;Rig.DisarmMotion();
            UI.SetPauseRoundActive(false);UI.OpenMatchRules();SetMatchStatus("ROUND NOT STARTED | No clear authored source spawn for this floor");
        }
    }
}
#endif

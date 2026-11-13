#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Player;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.Match;
using Ashfall.Integration.Playable.TeamLaunch;
using Ashfall.Integration.Playable.World;
using UnityEngine;
namespace Ashfall.Editor.Playable
{
    // Genuine loaded Alpha and both published Magnum payloads. No floor/actor,
    // Health, inventory, physics motor or source recovery behavior is mocked.
    public sealed class R24ArchiveGhostRecoverySmoke
    {
        [Serializable]sealed class Group{public string map="",error="",notes="";public bool passed;}
        [Serializable]sealed class Result{public bool nativeExecuted,success,headsetVerified=false;public string error="";public List<Group> groups=new List<Group>();}
        R23PlayableLoop loop;Result result;Saved saved;float deadline;
        public bool AutoFinish=true;public bool Passed{get;private set;}public string Json{get;private set;}
        static Float3 F(Vector3 p){return new Float3(p.x,p.y,p.z);}static Vector3 V(Float3 p){return new Vector3(p.X,p.Y,p.Z);}
        static void Need(bool v,string why){if(!v)throw new InvalidOperationException(why);}
        void Bound(){Need(Time.realtimeSinceStartup<deadline,"Archive recovery fixture exceeded300s active bound");}
        public IEnumerator Run()
        {
            result=new Result();float end=Time.realtimeSinceStartup+120;
            while(Time.realtimeSinceStartup<end){loop=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(loop!=null&&(loop.Ready||loop.Failure!=null))break;yield return null;}
            if(loop==null||!loop.Ready||UnityEngine.XR.XRSettings.isDeviceActive){result.error="Actual ready XR-off Root required";Finish();yield break;}
            saved=new Saved(loop);deadline=Time.realtimeSinceStartup+300;result.nativeExecuted=true;
            try
            {
                loop.ManualSmoke=true;loop.ExternalProbePump=true;loop.Rig.enabled=false;loop.Gameplay.ExternalInputPump=true;loop.Calibration.SuppressPersistence=true;
                loop.Rig.TrackingOrigin.localPosition=Vector3.zero;loop.Rig.Head.transform.localPosition=new Vector3(0,1.7088f,0);loop.Rig.Left.localPosition=new Vector3(-.24f,1.16f,.24f);loop.Rig.Right.localPosition=new Vector3(.24f,1.16f,.24f);
                foreach(string id in new[]{"archive:alfa-aglae-souza","archive:magnum-chaosvr","archive:magnum-3-chaosvr"})
                {
                    var group=new Group{map=id};result.groups.Add(group);var drive=Drive(group,Measure(group));while(drive.MoveNext())yield return drive.Current;
                }
                var cleanup=new Group{map="OriginalSceneCleanup"};result.groups.Add(cleanup);var restore=Drive(cleanup,Cleanup());while(restore.MoveNext())yield return restore.Current;
            }
            finally{try{if(loop.Ready)loop.ReturnToLobby();}catch(Exception e){result.error+=" Cleanup: "+e;}saved.Restore(loop);Finish();}
        }
        IEnumerator Drive(Group g,IEnumerator work)
        {
            var stack=new Stack<IEnumerator>();stack.Push(work);
            try{while(stack.Count>0){bool moved=false;object current=null;try{Bound();moved=stack.Peek().MoveNext();if(moved)current=stack.Peek().Current;}catch(Exception e){g.error=e.ToString();Debug.LogError("R24 ARCHIVE GHOST "+g.map+": "+e);break;}if(!moved){var done=stack.Pop()as IDisposable;if(done!=null)done.Dispose();continue;}var nested=current as IEnumerator;if(nested!=null)stack.Push(nested);else yield return current;}g.passed=g.error.Length==0;}
            finally{while(stack.Count>0){var done=stack.Pop()as IDisposable;if(done!=null)done.Dispose();}}
        }
        IEnumerator Measure(Group group)
        {
            Need(R23MapCatalog.Find(group.map)!=null,"Actual published map payload is unavailable; do not create a stand-in");
            loop.ReturnToLobby();Need(loop.UI.SetSelectedMap(group.map),"Actual map selector unavailable");loop.UI.OpenMatchRules();loop.UI.SelectMatchPreset("team_revive");
            Need(loop.UI.SetMatchRule("zone_enabled",false)&&loop.UI.SetMatchRule("protection",0f)&&loop.UI.SetMatchRule("time_limit",0),"Actual supported source match controls refused");
            Need(loop.UI.RequestMatchStart(),"Real UI START MATCH refused");float end=Mathf.Min(deadline,Time.realtimeSinceStartup+75);
            while(Time.realtimeSinceStartup<end&&loop.Failure==null)
            {
                if(loop.Ready)Pump();if(loop.Ready&&loop.MatchActive&&loop.Map.MapId==group.map&&loop.ArchiveGhostRecoveryReady)break;Bound();yield return null;
            }
            Need(loop.Ready&&loop.MatchActive&&loop.Map.MapId==group.map&&loop.ArchiveGhostRecoveryReady,"Actual archive source plan unavailable: "+loop.ArchiveGhostRecoveryFailure+" / "+loop.Failure);
            var plan=loop.ArchiveGhostSpawnPlan;var info=loop.ArchiveGhostSourceInfo;R24LaunchMapInfo frame;Need(loop.LaunchMapCatalog.TryRead(group.map,out frame),"Actual map origin/catalog binding unavailable");
            Need(plan.Ready&&plan.Spawns.Length>0&&plan.Loot.Length>0,"No source floor/headroom-validated native sites");
            Need(loop.Gameplay.ApplyDirectPlayerDamage(1000),"Real canonical lethal callback refused");loop.TickMatch(.001f,true);Pump();
            Need(loop.Gameplay.LocalMatchHealth.Life==LifeState.Downed&&loop.MatchLocalGhost&&loop.Rig.SourceGhostMotion,"Real source downed life not entered");
            var before=loop.Gameplay.LocalMatchHealth;string items=WeaponState();int balance=loop.Activities.Balance,panels=loop.Activities.PanelCount,broken=loop.Map.BrokenDestructibleCount;
            Vector3 head=loop.Rig.Head.transform.localPosition,left=loop.Rig.Left.localPosition,right=loop.Rig.Right.localPosition,origin=loop.Rig.TrackingOrigin.localPosition;Quaternion heading=loop.Rig.transform.rotation;
            bool magnum=R24ArchiveMapPolicy.MagnumOnly(group.map);var point=magnum?new Float3(plan.MagnumCenter.X,plan.MagnumFallLevel-.1f,plan.MagnumCenter.Z):new Float3(plan.SourceSpawn.X,info.SolidMin.Y-25.1f,plan.SourceSpawn.Z);
            var world=World(point,frame);int count=loop.GhostRecoveryCount;Need(loop.Rig.RelocateSourceGhost(world),"Actual sole-Rig source pose staging refused");Physics.SyncTransforms();
            for(int i=0;i<3&&loop.GhostRecoveryCount==count;i++)loop.TickMatch(.1f,true);
            Need(loop.GhostRecoveryCount==count+1&&!loop.GhostRecoveryUsedSourceFallback,"Actual source native candidate/center relocation did not commit once");
            var after=loop.Gameplay.LocalMatchHealth;
            Need(after.Revision==before.Revision&&after.Life==before.Life&&after.Health==before.Health&&after.Shield==before.Shield&&after.Deaths==before.Deaths&&after.DownedUntil==before.DownedUntil&&after.RespawnAt==before.RespawnAt&&after.ProtectionUntil==before.ProtectionUntil&&after.LifeGeneration==before.LifeGeneration,"Archive recovery changed canonical Health/counters/timers");
            Need(items==WeaponState()&&balance==loop.Activities.Balance&&panels==loop.Activities.PanelCount&&broken==loop.Map.BrokenDestructibleCount,"Archive relocation altered actual inventory/ammo/builds/world destruction");
            Need(loop.Rig.Head.transform.localPosition==head&&loop.Rig.Left.localPosition==left&&loop.Rig.Right.localPosition==right&&loop.Rig.TrackingOrigin.localPosition==origin&&loop.Rig.transform.rotation==heading,"Archive relocation rewrote tracked poses/origin/heading");
            if(magnum)Need(Vector3.Distance(loop.Rig.transform.position,World(plan.MagnumCenter,frame))<.0001f,"Magnum did not use exact mean-spawn center floor+2.5 source position");
            RaycastHit support;Need(Floor(loop.Rig.transform.position,magnum?4:1,out support),"Real relocated source site lacks native map support");
            if(magnum)Need(loop.Rig.transform.position.y-support.point.y>2&&loop.Rig.transform.position.y-support.point.y<3,"Magnum source2.5m recovery height changed");
            Need(!loop.Rig.LeftInputReady&&!loop.Rig.RightInputReady,"Actual source recovery failed to require neutral");
            group.notes="Actual "+group.map+" bound adapter="+info.AdapterSha256+" nativeSpawns="+plan.Spawns.Length+" loot="+plan.Loot.Length+" from="+world+" to="+loop.LastGhostRecoveryTo+" floor="+support.collider.name+" sourceMagnum="+magnum+" fallLevel="+plan.MagnumFallLevel+" same canonicalHealth/inventory/timers/rawtracking/worldstate";Debug.Log("R24 ARCHIVE GHOST PASS "+group.notes);
        }
        Vector3 World(Float3 p,R24LaunchMapInfo frame){return loop.Map.transform.TransformPoint(V(frame.RepresentedOriginUnity)+new Vector3(p.X,p.Y,-p.Z));}
        void Pump(){loop.Rig.SmokeTrackedHands(new HandSample{Tracked=true,Position=F(loop.Rig.transform.InverseTransformPoint(loop.Rig.Left.position))},new HandSample{Tracked=true,Position=F(loop.Rig.transform.InverseTransformPoint(loop.Rig.Right.position))});loop.SmokeUiUpdate();Physics.SyncTransforms();}
        bool Floor(Vector3 foot,float depth,out RaycastHit best){best=new RaycastHit();float distance=float.PositiveInfinity;foreach(var hit in Physics.RaycastAll(foot+Vector3.up*.3f,Vector3.down,depth,Physics.AllLayers,QueryTriggerInteraction.Ignore))if(hit.collider.transform.IsChildOf(loop.Map.transform)&&hit.collider.gameObject.layer!=19&&hit.collider.gameObject.layer!=20&&hit.collider.gameObject.layer!=22&&hit.normal.y>.7f&&hit.distance<distance){best=hit;distance=hit.distance;}return !float.IsPositiveInfinity(distance);}
        string WeaponState(){var r=new List<string>();foreach(var id in loop.Gameplay.OwnedWeaponActionIds){var s=loop.Gameplay.ReadWeaponState(id);r.Add(id+":"+s.Loaded+":"+s.Seated+":"+s.Shots+":"+s.Phase);}return string.Join("|",r.ToArray());}
        IEnumerator Cleanup(){loop.ReturnToLobby();if(loop.Map.MapId!=saved.Map){loop.SelectMap(saved.Map,false);float end=Mathf.Min(deadline,Time.realtimeSinceStartup+60);while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;Need(loop.Ready&&loop.Map.MapId==saved.Map,"Original actual map restore failed");loop.ReturnToLobby();}Need(loop.ActualMatchRules==null&&!loop.Rig.SourceGhostMotion,"Cleanup left actual ghost/match policy active");}
        void Finish(){result.success=result.nativeExecuted&&result.error.Length==0&&result.groups.Count==4;foreach(var g in result.groups)result.success&=g.passed;Passed=result.success;Json=JsonUtility.ToJson(result,true);Debug.Log("R24 ARCHIVE GHOST TERMINAL "+Json);if(AutoFinish)R23SmokeControl.Finish(Passed,Json);}
        sealed class Saved
        {
            public readonly string Map;readonly string selected;readonly bool manual,external,pump,rigEnabled,persist,blocked;readonly Vector3 root,origin,head,left,right;readonly Quaternion rootQ,headQ,leftQ,rightQ;
            public Saved(R23PlayableLoop l){var r=l.Rig;Map=l.Map.MapId;selected=l.UI.SelectedMapId;manual=l.ManualSmoke;external=l.ExternalProbePump;pump=l.Gameplay.ExternalInputPump;rigEnabled=r.enabled;persist=l.Calibration.SuppressPersistence;blocked=r.GameplayBlocked;root=r.transform.position;rootQ=r.transform.rotation;origin=r.TrackingOrigin.localPosition;head=r.Head.transform.localPosition;left=r.Left.localPosition;right=r.Right.localPosition;headQ=r.Head.transform.localRotation;leftQ=r.Left.localRotation;rightQ=r.Right.localRotation;}
            public void Restore(R23PlayableLoop l){var r=l.Rig;l.UI.ClearMatchSelection();if(selected!=null)l.UI.SetSelectedMap(selected);r.transform.SetPositionAndRotation(root,rootQ);r.TrackingOrigin.localPosition=origin;r.Head.transform.SetLocalPositionAndRotation(head,headQ);r.Left.SetLocalPositionAndRotation(left,leftQ);r.Right.SetLocalPositionAndRotation(right,rightQ);r.DisarmMotion();l.Calibration.SuppressPersistence=persist;l.Gameplay.ExternalInputPump=pump;r.GameplayBlocked=blocked;r.enabled=rigEnabled;l.ExternalProbePump=external;l.ManualSmoke=manual;Physics.SyncTransforms();}
        }
    }
}
#endif

#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Ashfall.Core;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.Bots;
using UnityEngine;
using UnityEditor;

namespace Ashfall.Editor.Playable
{
    public static partial class R24Feedback6237Smoke
    {
        static IEnumerator ValleyRouteWorld(R23PlayableLoop loop,Action<string> note)
        {
            GameObject blocker=null;bool originalBackfaces=Physics.queriesHitBackfaces;
            try
            {
                yield return LoadValleyRouteScene(loop);var routes=(R24GuardRoutes)Read(loop.ValleyBots,"routes");var start=new Float3(-29,0,26.5f);var goal=new Float3(-18,8,43);
                // Same actual-world cases as Ashfall tests/guard_routes.gd and
                // guard_route_cost.gd. No fabricated support/loaded-result flags.
                var route=routes.BeginRoute(start,goal,2.2f,1);note("Actual ground-roof points="+route.PointCount+" aborted="+route.Aborted+" reason="+route.AbortReason+" budget="+route.Budget);Need(!route.Aborted&&route.PointCount>0,"Actual source ground-to-roof corridor");
                Need(routes.Route(start,new Float3(-18,3,43)).Length==0,"Source unsupported interior destination refused");var muzzle=routes.Route(start,new Float3(-18,9.5f,43));Need(muzzle.Length>0&&Math.Abs(muzzle[muzzle.Length-1].Y-8)<.1f,"Source muzzle report resolves to nearby actual floor");Need(routes.Route(start,new Float3(-18,13,43)).Length==0,"Source airborne report cannot snap across storey");
                for(int i=0;i<routes.AnchorCount;i++){var at=routes.AnchorAt(i);bool supported=routes.WalkableSegment(at,at);note("Actual source anchor="+i+" local="+V3(at).ToString("F4")+" support="+supported);}
                var owners=new List<object>();var requests=new List<R24GuardRoutes.Traversal>();for(int i=0;i<8;i++){owners.Add(new object());requests.Add(routes.RequestRoute(owners[i],new Float3(-29,0,26.5f+i*.05f),goal,2.2f,1));}Need(routes.PendingCount==8,"Eight real physics route requests queued");
                for(int i=0;i<8;i++){int before=routes.CompletedQueries;var watch=System.Diagnostics.Stopwatch.StartNew();routes.ProcessOne();watch.Stop();note("Actual queued query="+i+" ms="+watch.Elapsed.TotalMilliseconds.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" completedDelta="+(routes.CompletedQueries-before));Need(routes.CompletedQueries-before==1,"Source one computed route per supplied physics step");yield return null;}
                Need(routes.PendingCount==0,"Eight requests drained");foreach(var request in requests)Need(!request.Pending&&!request.Aborted&&request.PointCount>0,"Queued actual-world result supported");
                blocker=new GameObject("DECLARED source guard-route endpoint blocker");blocker.layer=0;blocker.transform.SetParent(loop.Map.transform,false);blocker.transform.localPosition=V3(goal)+Vector3.up;blocker.AddComponent<BoxCollider>().size=Vector3.one*2;Physics.SyncTransforms();
                foreach(bool flag in new[]{false,true}){Physics.queriesHitBackfaces=flag;var blocked=routes.Route(start,goal);note("Actual blocked endpoint points="+blocked.Length+" callerBackfaces="+flag+" after="+Physics.queriesHitBackfaces);Need(blocked.Length==0&&Physics.queriesHitBackfaces==flag,"Source inside blocker refuses cached endpoint and restores caller query flag");}
                UnityEngine.Object.DestroyImmediate(blocker);blocker=null;Physics.SyncTransforms();Need(routes.Route(start,goal).Length>0,"Fresh endpoint reopens after actual blocker removal");
                foreach(var renderer in loop.ValleyBots.EncounterTransform.GetComponentsInChildren<Renderer>(true))foreach(var material in renderer.sharedMaterials)if(material!=null)foreach(var message in ShaderUtil.GetShaderMessages(material.shader))Need(message.severity.ToString()!="Error",message.message);
                note("Actual imported Valley query/queue/inside-blocker controls only; no physical actor/HMD/performance acceptance from these query results.");GC.KeepAlive(owners);
            }
            finally{if(blocker!=null)UnityEngine.Object.Destroy(blocker);Physics.queriesHitBackfaces=originalBackfaces;loop.ReturnToLobby();}
        }
        static IEnumerator LoadValleyRouteScene(R23PlayableLoop loop)
        {
            loop.ReturnToLobby();loop.UI.OpenMaps();Need(loop.UI.ChooseMap("valley"),"Actual Valley map card");float until=Time.realtimeSinceStartup+90;while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<until)yield return null;Need(loop.Ready&&loop.Map.IsLoaded&&loop.Map.MapId=="valley","Actual Valley loaded for source route query");loop.StartPlaying();loop.StepValleyBots(0,true);until=Time.realtimeSinceStartup+40;while(!loop.ValleyBots.Ready&&loop.ValleyBots.Failure==null&&Time.realtimeSinceStartup<until){loop.StepValleyBots(0,true);yield return null;}Need(loop.ValleyBots.Ready,"Actual source actor bridge ready "+loop.ValleyBots.Failure);
        }
        static IEnumerator ValleyRouteTraversal(R23PlayableLoop loop,Action<string> note,bool autonomous,int firstCase=0,int finalCase=3)
        {
            const string key="ashfall.unity.relay-valley.checkpoint.v1";bool had=PlayerPrefs.HasKey(key);string saved=PlayerPrefs.GetString(key,"");var mission=loop.ValleyBots.Mission;int chapter=mission.Chapter,kills=mission.Kills;double seconds=mission.PlaySeconds;float difficulty=loop.ValleyBots.Difficulty;bool backfaces=Physics.queriesHitBackfaces;
            try
            {
                yield return LoadValleyRouteScene(loop);
                var starts=new[]{new Float3(-29,0,26.5f),new Float3(-18,8,43),new Float3(-18,8,43)};var goals=new[]{new Float3(-18,8,43),new Float3(15,16,69),new Float3(-29,0,26.5f)};
                for(int test=firstCase;test<finalCase;test++)
                {
                    // Exact source tests/guard_routes.gd setup: fresh source actor
                    // placed start+.1m; actual controller supplies all later motion.
                    loop.StartNewValleyCampaign();loop.StepValleyBots(0,true);float until=Time.realtimeSinceStartup+40;while(!loop.ValleyBots.Ready&&loop.ValleyBots.Failure==null&&Time.realtimeSinceStartup<until){loop.StepValleyBots(0,true);yield return null;}var squad=loop.ValleyBots;Need(squad.Ready&&squad.Count==4,"Fresh actual encounter for source route case "+test);squad.SetDifficulty(autonomous?1:.5f);squad.ResetRouteDiagnostics();squad.CaptureRouteDiagnostics=autonomous;
                    for(int other=1;other<4;other++){R24KestrelBrain peer;Vector3 at;bool floor;Need(squad.TryRead(other,out peer,out at,out floor),"Actual companion source actor");var body=FindValleyBody(squad,peer.Variant,other);Need(body!=null&&loop.ApplyTargetDamage(squad.TargetId(body),20000,new Float3(0,1,-8)),"Retire companion through actual Root damage router");}Need(squad.LivingCount==1,"One actual source route actor remains");
                    var start=starts[test];var goal=goals[test];Need(squad.FixturePlaceRouteActor(0,V3(start)+Vector3.up*.1f),"Declared source route actor setup position");var worldGoal=loop.Map.transform.TransformPoint(V3(goal));squad.NotifyGunshot(worldGoal);Need(squad.EncounterActive,"Actual source remembered gunshot activates encounter");
                    var graph=(R24GuardRoutes)Read(squad,"routes");var follower=autonomous?null:graph.BeginRoute(start,goal,2.2f,1);if(!autonomous)Need(!follower.Aborted,"Actual physical source route precondition "+follower.AbortReason);
                    if(autonomous){var hidden=loop.Map.transform.TransformPoint(new Vector3(1000,500,-1000));loop.Rig.SetSpawn(hidden,hidden+Vector3.forward);note("Source autonomous control: target moved to "+hidden+" after single remembered report "+worldGoal+"; no continuing target information supplied.");}
                    bool reached=false,returned=false;float elapsed=0,arrival=0,maximumStep=0,maximumHeight=start.Y;int steps=0;string reason="";float health=loop.Gameplay.Health,shield=loop.Gameplay.Shield;
                    for(;steps<4500;steps++)
                    {
                        R24KestrelBrain brain;Vector3 position;bool grounded;Need(squad.TryRead(0,out brain,out position,out grounded),"Actual route actor readable");var before=position;
                        if(autonomous){int queries=squad.CompletedRouteQueries;loop.StepValleyBots(.02f,true);Need(squad.CompletedRouteQueries-queries<=1,"Production shared routing stays one query per actual supplied tick");follower=squad.FixtureActorRoute(0);Need(Vector3.Distance(V3(brain.LastFeet),worldGoal)<.0001f,"Unseen target relocation cannot overwrite remembered route goal");}
                        else{var direction=follower.Direction(F3(loop.Map.transform.InverseTransformPoint(position)),.02f);if(follower.Finished||follower.Aborted){reached=follower.Finished;reason=follower.AbortReason;break;}Need(squad.FixtureMoveRouteActor(0,direction,.02f),"Existing source actor safety/controller movement executes");}
                        elapsed+=.02f;squad.TryRead(0,out brain,out position,out grounded);maximumStep=Mathf.Max(maximumStep,Vector3.Distance(before,position));maximumHeight=Mathf.Max(maximumHeight,loop.Map.transform.InverseTransformPoint(position).y);
                        if(follower!=null){reason=follower.AbortReason;if(follower.Finished&&!reached){reached=true;arrival=elapsed;}if(follower.Aborted)break;}if(autonomous&&brain.State==R24KestrelState.Patrol){returned=true;break;}if(steps%25==0)yield return null;
                    }
                    R24KestrelBrain final;Vector3 finalPosition;bool finalGrounded;squad.TryRead(0,out final,out finalPosition,out finalGrounded);note("Actual "+(autonomous?"autonomous":"controller")+" route="+test+" sourceStart="+V3(start)+" sourceGoal="+V3(goal)+" reached="+reached+" returnedPatrol="+returned+" abort="+reason+" seconds="+elapsed+" arrival="+arrival+" steps="+steps+" maxStep="+maximumStep+" maxHeight="+maximumHeight+" final="+finalPosition+" grounded="+finalGrounded);
                    if(autonomous)note("Actual route return accepted="+squad.FixtureRouteReturns+" extraQueries="+squad.FixtureRouteReturnQueries);if(autonomous)note("Actual bounded route trace generation="+squad.Generation+" phase="+final.Phase+"\n"+squad.RouteDiagnostics);Need(reached,"Actual source route physically reaches goal "+test+" abort="+reason);Need(maximumStep<.41f,"Source no-teleport movement bound "+maximumStep);if(autonomous&&test==1)Need(squad.FixtureRouteReturns>0,"Actual failed-corner inward return exercised through real wall/floor probes");if(autonomous)Need(returned&&elapsed-arrival<12,"Arrived source investigation returns Patrol within bounded local search");Need(loop.Gameplay.Health==health&&loop.Gameplay.Shield==shield,"Route-only source case causes no player damage");Need(Physics.queriesHitBackfaces==backfaces,"All source route queries restore caller backface flag");
                }
                foreach(var renderer in loop.ValleyBots.EncounterTransform.GetComponentsInChildren<Renderer>(true))foreach(var material in renderer.sharedMaterials)if(material!=null)foreach(var message in ShaderUtil.GetShaderMessages(material.shader))Need(message.severity.ToString()!="Error",message.message);
                note("Three exact source route pairs only; declared source-test starting poses and one remembered report. No all-map/HMD/performance/network authority claim.");
            }
            finally{loop.ValleyBots.CaptureRouteDiagnostics=false;Physics.queriesHitBackfaces=backfaces;loop.ReturnToLobby();mission.Restore(chapter,kills,seconds);loop.ValleyBots.SetDifficulty(difficulty);if(had)PlayerPrefs.SetString(key,saved);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();}
        }
    }
}
#endif

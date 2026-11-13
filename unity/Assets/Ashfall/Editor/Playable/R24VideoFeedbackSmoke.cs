#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using Ashfall.Core;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.Match;
using Ashfall.Integration.Playable.World;
using UnityEditor;
using UnityEngine;
namespace Ashfall.Editor.Playable
{
    public sealed class R24VideoFeedbackSmoke
    {
        [Serializable]sealed class Group{public string name="",error="";public bool passed;public List<string> notes=new List<string>();}
        [Serializable]sealed class Result{public bool success,nativeExecuted,headsetVerified=false;public string error="";public List<Group> groups=new List<Group>();}
        R23PlayableLoop root;Result result=new Result();float deadline;
        static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        static void Need(bool value,string why){if(!value)throw new InvalidOperationException(why);}
        static object Read(object owner,string field){var f=owner.GetType().GetField(field,Flags);Need(f!=null,"Inspected field "+field);return f.GetValue(owner);}
        void Note(Group g,string text){g.notes.Add(text);Debug.Log("VIDEO FEEDBACK "+g.name+": "+text);}
        void Pump(){root.SmokeUiUpdate();Need(Time.realtimeSinceStartup<deadline,"Finite video-feedback fixture deadline");}
        IEnumerator Load(string id){root.ReturnToLobby();root.SelectMap(id,false);float end=Mathf.Min(deadline,Time.realtimeSinceStartup+60);while(!root.Ready&&root.Failure==null&&Time.realtimeSinceStartup<end)yield return null;Need(root.Ready&&root.Map.MapId==id,"Actual source map load: "+id+" / "+root.Failure);}
        IEnumerator Enter(){root.StartPlaying();float end=Mathf.Min(deadline,Time.realtimeSinceStartup+65);while(root.SourceArchiveEntryPending&&Time.realtimeSinceStartup<end){Pump();yield return null;}Need(!root.SourceArchiveEntryPending&&String.IsNullOrEmpty(root.SourceArchiveEntryFailure)&&root.UI.Page==Ashfall.Integration.Playable.UI.R23UiPage.Playing,"Actual source entry: "+root.SourceArchiveEntryFailure);}
        IEnumerator WorldReady(){float end=Mathf.Min(deadline,Time.realtimeSinceStartup+60);while(!root.ArchiveWorldReady&&root.ArchiveWorldFailure==null&&Time.realtimeSinceStartup<end){root.PrepareArchiveWorld(true);yield return null;}Need(root.ArchiveWorldReady,"Actual map-owned source planner: "+root.ArchiveWorldFailure);}
        void Shaders(){var shaders=new HashSet<Shader>();foreach(var r in root.Map.GetComponentsInChildren<Renderer>(true))foreach(var m in r.sharedMaterials)if(m!=null&&m.shader!=null&&shaders.Add(m.shader))foreach(var message in ShaderUtil.GetShaderMessages(m.shader))Need(message.severity.ToString()!="Error",message.message);}
        IEnumerator Solids(Group g)
        {
            yield return Load("archive:afterlife-gods-alamo-hank");root.Map.SetWorldVisible(true);Physics.SyncTransforms();
            var list=new List<MeshCollider>();foreach(var c in root.Map.GetComponentsInChildren<MeshCollider>(true))if(c.name.StartsWith("Afterlife source solid ",StringComparison.Ordinal))list.Add(c);
            Need(root.Map.SourceSolidVolumeCount==66&&list.Count==66,"All sixty-six exact closed source pieces cooked once");
            int renderers=root.Map.GetComponentsInChildren<Renderer>(true).Length,deep=0,outside=0;bool flag=Physics.queriesHitBackfaces;
            var centers=new Vector3[list.Count];var baseline=new bool[list.Count];var probes=new List<Vector3>();
            try{
                for(int i=0;i<list.Count;i++){var c=list[i];Need(c.convex&&c.gameObject.layer==R23PlayableMap.PlayerOnlyLayer&&!c.isTrigger&&c.GetComponents<Renderer>().Length==0,"Player-only solid with no renderer");Vector3 center=Vector3.zero;var vertices=c.sharedMesh.vertices;foreach(var v in vertices)center+=v;centers[i]=c.transform.TransformPoint(center/vertices.Length);c.enabled=false;}
                Physics.SyncTransforms();for(int i=0;i<list.Count;i++)baseline[i]=root.Rig.HeadCollision.Clear(centers[i],Vector3.zero);
                foreach(var c in list){var b=c.sharedMesh.bounds;foreach(var axis in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back}){Vector3 p=b.center+Vector3.Scale(axis,b.extents+Vector3.one*.4f);p=c.transform.TransformPoint(p);bool outsideAll=true;foreach(var other in list){var bounds=other.sharedMesh.bounds;bounds.Expand(.3f);if(bounds.Contains(other.transform.InverseTransformPoint(p))){outsideAll=false;break;}}if(outsideAll&&root.Rig.HeadCollision.Clear(p,Vector3.zero))probes.Add(p);}}
                foreach(var c in list)c.enabled=true;Physics.SyncTransforms();
                for(int i=0;i<list.Count;i++){bool clear=root.Rig.HeadCollision.Clear(centers[i],Vector3.zero);Note(g,"solid="+list[i].name+" center="+centers[i].ToString("F6")+" originalClear="+baseline[i]+" solidClear="+clear);Need(!clear,"Actual source solid interior must reject head: "+list[i].name);if(baseline[i])deep++;}
                foreach(var p in probes){Need(root.Rig.HeadCollision.Clear(p,Vector3.zero),"Source-exterior clear point was filled: "+p);outside++;}
                Need(deep>0&&outside>0,"Actual shell-interior witness and external-clear controls required");Need(root.Map.GetComponentsInChildren<Renderer>(true).Length==renderers,"No visible renderer additions");Need(Physics.queriesHitBackfaces==flag,"Caller query flag retained");Shaders();Note(g,"exactConvexPieces=66 originalSurfaceTriangles=728 newlyBlockedDeepInteriors="+deep+" retainedExternalClearPoints="+outside+" renderers="+renderers+". CPU source conservation remains separately pinned; no exact human entry/HMD/all-map proof.");
            }finally{foreach(var c in list)if(c!=null)c.enabled=true;Physics.SyncTransforms();}
            var owned=list.ToArray();root.ReturnToLobby();root.SelectMap("range",false);while(!root.Ready&&root.Failure==null&&Time.realtimeSinceStartup<deadline)yield return null;yield return null;foreach(var c in owned)Need(c==null,"Source volume survived map disposal");Need(Physics.queriesHitBackfaces==flag,"Scene cleanup changed caller flag");
        }
        IEnumerator Entries(Group g)
        {
            foreach(string id in new[]{"archive:afterlife-gods-alamo-hank","archive:10000-days-gods-crazytrain","archive:aethelgard-solstis-kiki-80-bbb"}){
                yield return Load(id);string ids=String.Join(",",root.Gameplay.OwnedWeaponActionIds);int ammo=root.Gameplay.ReadStoredAmmoReserveFamily("rifle");yield return Enter();yield return WorldReady();
                Float3 source;Need(root.TryArchiveWorldToSource(root.Rig.transform.position,out source),"Actual entered source coordinates");Need(root.ArchiveWorldSafeSpawn(source)&&!root.ArchiveWorldOutside(source)&&!root.ArchiveWorldHazard(source),"Entered actual source floor/headroom/hazard gates");
                Note(g,"map="+id+" world="+root.Rig.transform.position.ToString("F6")+" source="+source+" health="+root.Gameplay.Health+" shield="+root.Gameplay.Shield+" generation="+root.ArchiveWorldGeneration);
                Need(root.Gameplay.Health>0&&String.Join(",",root.Gameplay.OwnedWeaponActionIds)==ids&&root.Gameplay.ReadStoredAmmoReserveFamily("rifle")==ammo,"Entry keeps actual owners/ammo and live Health");
                if(id!="archive:aethelgard-solstis-kiki-80-bbb"){
                    root.UI.OpenMatchRules();root.UI.SelectMatchPreset("team_revive");Need(root.UI.RequestMatchStart(),"Public source match request");float end=Mathf.Min(deadline,Time.realtimeSinceStartup+65);while((root.SourceArchiveEntryPending||!root.MatchActive)&&Time.realtimeSinceStartup<end){Pump();yield return null;}Need(root.MatchActive&&!root.SourceArchiveEntryPending,"Actual source grounded team match admitted");Need(root.TeamLaunch==null||!root.TeamLaunch.Eligible,"Reviewed enclosed map must not create sky deployment");yield return WorldReady();Need(root.TryArchiveWorldToSource(root.Rig.transform.position,out source)&&root.ArchiveWorldSafeSpawn(source),"Actual team start has source floor/headroom");Note(g,"ground-team="+id+" source="+source+" onLaunchDeck="+root.OnLaunchDeck);Need(!root.OnLaunchDeck,"No platform deck state");
                }
                Shaders();root.ReturnToLobby();Need(!root.SourceArchiveEntryPending,"HOME cancels source entry owner");
            }
        }
        IEnumerator Rounds(Group g)
        {
            yield return Load(R24ArchiveRoundPolicy.FiveLevelsId);yield return Enter();root.UI.OpenMatchRules();root.UI.SelectMatchPreset("team_respawn");Need(root.UI.SelectedMatchRules.Rounds==5&&root.UI.SelectedMatchRules.TimeLimit==300,"Actual Five Levels defaults five rounds/five minutes");Need(!root.UI.IsMatchRuleAvailable("rounds"),"Five Levels count is fixed to authored levels");Need(root.UI.RequestMatchStart(),"Public Five Levels start");
            float end=Mathf.Min(deadline,Time.realtimeSinceStartup+65);while((!root.MatchActive||root.SourceArchiveEntryPending)&&Time.realtimeSinceStartup<end){Pump();yield return null;}Need(root.MatchActive&&!root.SourceArchiveEntryPending,"Actual five-floor match starts");
            for(int round=1;round<=5;round++){
                yield return WorldReady();Float3 p=new Float3();Need(root.MatchRound==round&&root.TryArchiveWorldToSource(root.Rig.transform.position,out p)&&R24ArchiveRoundPolicy.OnRoundLevel(root.ArchiveWorldInfo,p,round),"Actual Root starts on authored round floor "+round);Note(g,"round="+round+" source="+p+" timeLimit="+root.ActualMatchRules.TimeLimit);
                if(round==1){
                    Pump();var clock=(TextMesh)Read(root.UI,"compactClockText");var personal=(TextMesh)Read(root.UI,"compactPersonalText");var backdrop=root.UI.CompactMatchHudRoot.Find("Source score backdrop").GetComponent<Renderer>();
                    Need(root.UI.CompactMatchHudVisible&&clock.text.Contains("K")&&clock.text.Contains("D")&&!personal.gameObject.activeInHierarchy,"Actual timer and K/D share one visible line");Need(clock.GetComponent<Renderer>().bounds.min.y>backdrop.bounds.max.y,"Actual timer glyphs are above scoreboard backing");
                    string image=Path.Combine(Path.GetDirectoryName(R23PlayableBuild.Argument("-ashfallVRSmoke")),"UNMATCHED-five-levels-single-line-hud.png");Need(!File.Exists(image),"Fresh Head image");var camera=root.Rig.Head;var old=camera.targetTexture;var active=RenderTexture.active;var target=RenderTexture.GetTemporary(1200,800,24,RenderTextureFormat.ARGB32);Texture2D pixels=null;
                    try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels=new Texture2D(1200,800,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1200,800),0,0);pixels.Apply();File.WriteAllBytes(image,pixels.EncodeToPNG());Note(g,"actualHeadImage="+image+" clock="+clock.text);}
                    finally{camera.targetTexture=old;RenderTexture.active=active;if(pixels!=null)UnityEngine.Object.Destroy(pixels);RenderTexture.ReleaseTemporary(target);}
                    Need(root.Gameplay.ApplyLocalMatchFall(),"Real Health death control");for(int i=0;i<600&&root.Gameplay.LocalMatchHealth.Life!=Ashfall.Modules.HealthStates.LifeState.Alive;i++)root.TickMatch(.02f,true);Need(root.MatchRound==1&&root.Gameplay.LocalMatchHealth.Life==Ashfall.Modules.HealthStates.LifeState.Alive,"A death respawns without advancing floor");Need(root.TryArchiveWorldToSource(root.Rig.transform.position,out p)&&R24ArchiveRoundPolicy.OnRoundLevel(root.ArchiveWorldInfo,p,1),"Actual death respawn remains first floor");}
                for(int i=0;i<1201&&root.MatchActive&&root.MatchRound==round;i++)root.TickMatch(.25f,true);
                Need(round==5?!root.MatchActive:root.MatchActive&&root.MatchRound==round+1,"Actual existing timer advances only at round end");
            }
            root.ReturnToMapLobby();root.UI.ToggleMenu();Action start=null;bool restart=false;foreach(object b in (IEnumerable)Read(root.UI,"buttons")){var caption=(TextMesh)Read(b,"Caption");if(caption!=null){if(caption.text=="START ROUND")start=(Action)Read(b,"Action");restart|=caption.text=="RESTART ROUND";}}Need(start!=null&&!restart,"Actual lobby pause action reads START ROUND");start();end=Mathf.Min(deadline,Time.realtimeSinceStartup+65);while((!root.MatchActive||root.SourceArchiveEntryPending)&&Time.realtimeSinceStartup<end){Pump();yield return null;}Need(root.MatchActive&&root.MatchRound==1&&!root.SourceArchiveEntryPending,"Actual lobby START ROUND callback starts first floor");Shaders();Note(g,"Five actual source floors / real Health death / five 300s managed-clock rounds / lobby START ROUND callback. No human fight or network authority proof.");
        }
        IEnumerator LobbyHud(Group g)
        {
            yield return Load(R24ArchiveRoundPolicy.FiveLevelsId);yield return Enter();root.UI.OpenMatchRules();root.UI.SelectMatchPreset("team_respawn");Need(root.UI.RequestMatchStart(),"Actual one-round HUD setup");
            float end=Mathf.Min(deadline,Time.realtimeSinceStartup+65);while((!root.MatchActive||root.SourceArchiveEntryPending)&&Time.realtimeSinceStartup<end){Pump();yield return null;}Need(root.MatchActive&&!root.SourceArchiveEntryPending,"Actual initial match setup");Pump();root.UI.RefreshTextLayout();
            var camera=root.Rig.Head;float fov=camera.fieldOfView;camera.fieldOfView=90;
            try{
                var clock=(TextMesh)Read(root.UI,"compactClockText");var personal=(TextMesh)Read(root.UI,"compactPersonalText");var backing=root.UI.CompactMatchHudRoot.Find("Source score backdrop").GetComponent<Renderer>();
                Need(clock.text.Contains("K")&&clock.text.Contains("D")&&!personal.gameObject.activeInHierarchy&&clock.GetComponent<Renderer>().bounds.min.y>backing.bounds.max.y,"Unchanged actual single-line HUD placement predicates");
                var planes=GeometryUtility.CalculateFrustumPlanes(camera);int visible=0;foreach(var renderer in root.UI.CompactMatchHudRoot.GetComponentsInChildren<Renderer>())if(renderer.enabled&&GeometryUtility.TestPlanesAABB(planes,renderer.bounds))visible++;Need(visible>5,"Actual HUD frustum eligibility");
                string image=Path.Combine(Path.GetDirectoryName(R23PlayableBuild.Argument("-ashfallVRSmoke")),"UNMATCHED-video-feedback-hud-desktop90.png");Need(!File.Exists(image),"Fresh HUD image");var old=camera.targetTexture;var active=RenderTexture.active;var target=RenderTexture.GetTemporary(1200,800,24,RenderTextureFormat.ARGB32);Texture2D pixels=null;
                try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels=new Texture2D(1200,800,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1200,800),0,0);pixels.Apply();File.WriteAllBytes(image,pixels.EncodeToPNG());Note(g,"image="+image+" actualHeadFOV90 originalFOV="+fov+" mask="+camera.cullingMask+" eligibleHUDRenderers="+visible+" clock="+clock.text);}
                finally{camera.targetTexture=old;RenderTexture.active=active;if(pixels!=null)UnityEngine.Object.Destroy(pixels);RenderTexture.ReleaseTemporary(target);}
            }finally{camera.fieldOfView=fov;}
            root.ReturnToMapLobby();Note(g,"Immediately after ReturnToMapLobby: page="+root.UI.Page+" sourceEntryPending="+root.SourceArchiveEntryPending);end=Mathf.Min(deadline,Time.realtimeSinceStartup+65);
            while(root.SourceArchiveEntryPending&&Time.realtimeSinceStartup<end){Pump();yield return null;}Need(!root.SourceArchiveEntryPending&&root.ActualMatchRules==null&&root.UI.Page==Ashfall.Integration.Playable.UI.R23UiPage.Playing,"Actual source lobby entry completes before opening pause");
            root.UI.ToggleMenu();Note(g,"Lobby pause page="+root.UI.Page+" roundActive="+Read(root.UI,"pauseRoundActive"));Action start=null;bool restart=false;
            foreach(object b in (IEnumerable)Read(root.UI,"buttons")){var caption=(TextMesh)Read(b,"Caption");if(caption==null)continue;Note(g,"button="+caption.text+" enabled="+Read(b,"Enabled"));if(caption.text=="START ROUND"){Need((bool)Read(b,"Enabled"),"Actual lobby start enabled");start=(Action)Read(b,"Action");}restart|=caption.text=="RESTART ROUND";}
            Need(start!=null&&!restart,"Unchanged actual lobby START ROUND predicate");start();end=Mathf.Min(deadline,Time.realtimeSinceStartup+65);while((!root.MatchActive||root.SourceArchiveEntryPending)&&Time.realtimeSinceStartup<end){Pump();yield return null;}Need(root.MatchActive&&root.MatchRound==1&&!root.SourceArchiveEntryPending,"Actual public lobby START callback starts first floor");Shaders();
        }
        public IEnumerator Start()
        {
            string scope=R23PlayableBuild.Argument("-ashfallVideoFeedback");Debug.Log("VIDEO FEEDBACK ENTRY "+scope);float end=Time.realtimeSinceStartup+90;while(Time.realtimeSinceStartup<end){root=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(root!=null&&(root.Ready||root.Failure!=null))break;yield return null;}
            if(root==null||!root.Ready||UnityEngine.XR.XRSettings.isDeviceActive){result.error="Actual ready XR-off Root required";R23SmokeControl.Finish(false,JsonUtility.ToJson(result,true));yield break;}
            result.nativeExecuted=true;deadline=Time.realtimeSinceStartup+(scope=="solids"?240:scope=="lobby-hud"?180:360);bool manual=root.ManualSmoke,external=root.ExternalProbePump,rig=root.Rig.enabled,pump=root.Gameplay.ExternalInputPump,persistence=root.Calibration.SuppressPersistence;
            try{
                root.ManualSmoke=true;root.ExternalProbePump=true;root.Rig.enabled=false;root.Gameplay.ExternalInputPump=true;root.Calibration.SuppressPersistence=true;
                string[] names=scope=="solids"?new[]{"AfterlifeExactSolids"}:scope=="entry"?new[]{"AuthoredMapEntry","FiveLevelsRounds"}:scope=="lobby-hud"?new[]{"LobbyStartAndHudCapture"}:new string[0];Need(names.Length>0,"Unknown video-feedback scope refuses");
                foreach(string name in names){var g=new Group{name=name};result.groups.Add(g);var work=name=="AfterlifeExactSolids"?Solids(g):name=="AuthoredMapEntry"?Entries(g):name=="LobbyStartAndHudCapture"?LobbyHud(g):Rounds(g);var stack=new Stack<IEnumerator>();stack.Push(work);
                    try{while(stack.Count>0){bool moved=false;object current=null;try{moved=stack.Peek().MoveNext();if(moved)current=stack.Peek().Current;}catch(Exception e){g.error=e.ToString();break;}if(!moved){var done=stack.Pop()as IDisposable;if(done!=null)done.Dispose();continue;}var nested=current as IEnumerator;if(nested!=null){stack.Push(nested);continue;}yield return current;}g.passed=g.error.Length==0;}
                    finally{while(stack.Count>0){var done=stack.Pop()as IDisposable;if(done!=null)done.Dispose();}root.ReturnToLobby();}
                }
                result.success=true;foreach(var g in result.groups)result.success&=g.passed;
            }finally{root.ReturnToLobby();root.ManualSmoke=manual;root.ExternalProbePump=external;root.Rig.enabled=rig;root.Gameplay.ExternalInputPump=pump;root.Calibration.SuppressPersistence=persistence;}
            R23SmokeControl.Finish(result.success,JsonUtility.ToJson(result,true));
        }
    }
}
#endif

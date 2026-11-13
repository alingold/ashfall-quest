#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Ashfall.Core;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.UI;
using Ashfall.Integration.Playable.Input;
using Ashfall.Integration.Playable.Appearance;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Modules.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ashfall.Editor.Playable
{
    // Finite checks of the latest human feedback, using the actual loaded Root.
    public static partial class R24Feedback6237Smoke
    {
        const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        static object Read(object value,string name){var f=value.GetType().GetField(name,Fields);if(f==null)throw new InvalidOperationException("Missing inspected field "+name);return f.GetValue(value);}
        static object Call(object value,string name,params object[] args){var m=value.GetType().GetMethod(name,Fields);if(m==null)throw new InvalidOperationException("Missing real callback "+name);return m.Invoke(value,args);}
        static void Need(bool yes,string message){if(!yes)throw new InvalidOperationException(message);}
        static object Button(R23VrUI ui,string caption,string id=null)
        {foreach(var b in (IEnumerable)Read(ui,"buttons")){var text=(TextMesh)Read(b,"Caption");if(id!=null?(string)Read(b,"OwnedId")==id:text!=null&&text.text==caption)return b;}throw new InvalidOperationException("Actual button missing "+caption+" / "+id);}
        static void Pointer(R23VrUI ui,object button,bool grip)
        {
            var surface=(Transform)Read(button,"Surface");var origin=surface.TransformPoint(new Vector3(0,0,-.4f));var direction=surface.forward;
            ui.UpdateLoadoutPointer(1,origin,direction,false,false,true);
            ui.UpdateLoadoutPointer(1,origin,direction,!grip,grip,true);
            if(!grip)ui.UpdateLoadoutPointer(1,origin,direction,false,false,true);
        }
        public static IEnumerator Check(R23PlayableLoop loop,string group,Action<string> note)
        {
            loop.EndMatch();loop.UI.ClearMatchSelection();loop.RestartRound();loop.UI.SetPlaying();
            if(group=="6237BuildEmpty257")return BuildEmpty257(loop,note);
            if(group=="6237DtGrip257")return DtGrip257(loop,note);
            if(group=="6237PaddleReady257")return PaddleReady257(loop,note);
            if(group=="6237UiClutter257")return UiClutter257(loop,note);
            if(group=="6237SourcePadCue")return SourcePadCue(loop,note);
            if(group=="6237ZoneHumRemaining")return ZoneDangerHum(loop,note,true);
            if(group=="6237ZoneDangerHum")return ZoneDangerHum(loop,note);
            if(group=="6237ZoneAcceptedHurtVoices")return ZoneAcceptedHurtVoices(loop,note);
            if(group=="6237BackpackReleaseOwned")return BackpackReleaseOwned(loop,note);
            if(group=="6237BackpackDragCatalog")return BackpackDragCatalog(loop,note);
            if(group=="6237SourceBallisticRoles")return SourceBallisticRoles(loop,note);
            if(group=="6237SourceBuildImpacts")return SourceBuildImpactRoles(loop,note);
            if(group=="6237AwpCurrentGallery")return GalleryCurrentAwpRay(loop,note);
            if(group=="6237AwpCurrentCarried")return CarriedCurrentAwpRay(loop,note);
            if(group=="6237Dt11RemainingAudio")return Dt11RemainingAudioControls(loop,note);
            if(group=="6237Dt11SourceAudio")return Dt11SourceAudio(loop,note);
            if(group=="6237SourceDrinkStacks")return SourceDrinkStacks(loop,note);
            if(group=="6237LateHeadCollision")return LateHeadCollision(loop,note);
            if(group=="6237DamageNumbersTeardown")return DamageNumbersTeardown(loop,note);
            if(group=="6237PracticeDamageNumbers")return PracticeDamageNumbers(loop,note);
            if(group=="6237GalleryFeedbackRightCX4")return GalleryFeedbackC1(loop,"cx4",note,1);
            if(group.StartsWith("6237GalleryFeedback/",StringComparison.Ordinal))return GalleryFeedbackC1(loop,group.Substring("6237GalleryFeedback/".Length),note);
            if(group.StartsWith("98254GalleryTracer/",StringComparison.Ordinal))return GalleryTracer98254(loop,group.Substring("98254GalleryTracer/".Length),note);
            if(group=="98254PaddleWheel")return PaddlesWheel98254(loop,note);
            if(group=="98254PaddleMeter")return PaddleMeter98254(loop,note);
            if(group=="98254TdmHud")return TdmHud98254(loop,note);
            if(group=="98254MeshSidesRepair")return MeshSidesRepair98254(loop,note);
            if(group=="98254MeshOverhangWalk")return MeshOverhangWalk98254(loop,note);
            if(group=="98254ContinuousCrouch")return ContinuousCrouch98254(loop,note);
            if(group=="98254ThinMeshObservation")return ThinMeshObservation98254(loop,note);
            if(group=="98254BuildOverlap")return BuildingPlacement98254(loop,note);
            if(group=="98254BuildBeam")return BuildingBeam98254(loop,note);
            if(group=="98254MaterialResponses")return SharedSheen98254(loop,note);
            if(group=="98254EitherHandTransfersRemaining")return EitherHandTransfers98254(loop,note,true);
            if(group=="98254EitherHandTransfers")return EitherHandTransfers98254(loop,note);
            if(group=="98254SupplyNeighbors")return SupplyNeighbors98254(loop,note);
            if(group=="6237SoundscapeCapture")return SourceSoundscapeCapture(loop,note);
            if(group=="6237SourceSoundscape")return SourceSoundscape(loop,note);
            if(group=="6237KestrelAudio")return KestrelSourceAudio(loop,note);
            if(group=="6237Round2MapVitals")return Round2MapVitals(loop,note);
            if(group=="6237Round2Crouch")return Round2Crouch(loop,note);
            if(group=="6237Round2HudCapture")return Round2HudCapture(loop,note);
            if(group=="6237Round2Hud")return Round2Hud(loop,note);
            if(group=="6237NonmetalV8Bindings")return NonmetalV8Bindings(loop,note);
            if(group.StartsWith("6237NonmetalV8/",StringComparison.Ordinal))return NonmetalV8Preview(loop,group.Substring("6237NonmetalV8/".Length),note);
            if(group=="6237NonmetalV7Bindings")return NonmetalV7Bindings(loop,note);
            if(group.StartsWith("6237NonmetalV7/",StringComparison.Ordinal))return NonmetalV7Preview(loop,group.Substring("6237NonmetalV7/".Length),note);
            if(group=="6237SelectiveFinishC1")return SelectiveFinishC1(loop,note);
            if(group=="6237BotFrontFacesC1")return BotFrontFacesC1(loop,note);
            if(group=="6237BotDamageNumbersC1")return ValleyGunHit(loop,note,true);
            if(group=="6237ScopeBuildRolesC1")return ScopeBuildRolesC1(loop,note);
            if(group=="6237ValleyGunHit")return ValleyGunHit(loop,note);
            if(group=="6237ValleyOptics")return ValleyBotOptics(loop,note);
            if(group=="6237ValleyBots")return ValleyBots(loop,note);
            if(group=="6237OrdinaryHurtVoices")return OrdinaryHurtVoices(loop,note);
            if(group=="6237ValleyRecovery")return ValleyRecoveryNative(loop,note);
            if(group=="6237ValleyRecoveryIsolation")return ValleyRecoveryIsolation(loop,note);
            if(group=="6237RagdollSourceGeometry")return RagdollSourceGeometry(loop,note);
            if(group=="6237RagdollLifecycle")return RagdollLifecycle(loop,note);
            if(group=="6237WardenVisual")return WardenVisualNative(loop,note);
            if(group=="6237WardenRemainingCover")return WardenRemainingCover(loop,note);
            if(group=="6237WardenGuardCadence")return WardenGuardAndCadence(loop,note);
            if(group=="6237WardenCarriedDamage")return WardenCarriedDamage(loop,note);
            if(group=="6237WardenAttacks")return WardenAttacks(loop,note);
            if(group=="6237ValleyMissions")return ValleyMissions(loop,note);
            if(group=="6237ValleyRouteWorld")return ValleyRouteWorld(loop,note);
            if(group=="6237ValleyRouteController")return ValleyRouteTraversal(loop,note,false);
            if(group=="6237ValleyRouteAutonomous")return ValleyRouteTraversal(loop,note,true);
            if(group=="6237ValleyRouteAutonomousUpper")return ValleyRouteTraversal(loop,note,true,1,2);
            if(group=="6237ValleyRouteAutonomousDescending")return ValleyRouteTraversal(loop,note,true,2,3);
            if(group=="6237HandedBackpackClose")return HandedBackpackClose(loop,note);
            if(group=="6237TacticalImageRange")return TacticalImage(loop,note,false);
            if(group=="6237TacticalImageAlpha")return TacticalImage(loop,note,true);
            if(group=="6237TacticalMap")return TacticalMap(loop,note);
            if(group=="6237CacheBackpacks")return CacheBackpacks(loop,note);
            if(group=="6237DrinkHandFit")return DrinkHandFit(loop,note);
            if(group=="6237GalleryHandCapture")return GalleryHandFits(loop,note,true);
            if(group=="6237GalleryHandFits")return GalleryHandFits(loop,note);
            if(group=="6237LegacyScoreboardCapture")return WorldScoreboards(loop,note,true);
            if(group=="6237WorldScoreboards")return WorldScoreboards(loop,note);
            if(group=="6237ZoneHum")return ZoneHum(loop,note);
            if(group=="6237ZoneMist")return ZoneMist(loop,note);
            if(group=="6237ClassicPads")return ClassicPads(loop,note);
            if(group=="6237Navigation")return Navigation(loop,note);
            if(group=="6237Handedness")return Handedness(loop,note);
            if(group=="6237BackpackPaddles")return BackpackPaddles(loop,note);
            if(group=="6237BackpackFullRefusal")return BackpackFullRefusal(loop,note);
            if(group=="6237DominantWorldPickups")return DominantWorldPickups(loop,note);
            if(group=="6237ReverseInclineObservation")return ReverseInclineProbe(loop,note);
            if(group=="6237CompactPod")return CompactPod(loop,note);
            if(group=="6237PodReach")return PodReach(loop,note);
            if(group=="6237MovingPointers")return MovingPointers(loop,note);
            if(group=="6237BackpackCapacity")return BackpackCapacity(loop,note);
            if(group=="6237Cloth")return Cloth(loop,note);
            throw new InvalidOperationException("Unknown declared feedback group "+group);
        }
        static IEnumerator Navigation(R23PlayableLoop loop,Action<string> note)
        {
            string map=loop.Map.MapId;
            loop.ReturnToLobby();loop.UI.OpenMaps();Need(loop.UI.ChooseMap(map),"Actual current map card");yield return null;
            Need(loop.Ready&&loop.ActualMatchRules==null,"Map card enters local lobby without active match");
            Need(Vector3.Distance((Vector3)Read(loop.Rig,"spawn"),loop.Map.WarmupSpawn)<.001f,"Initial lobby spawn");
            loop.UI.OpenMatchRules();loop.UI.SelectMatchPreset("team_revive");Need(loop.UI.RequestMatchStart(),"Actual Start Match");
            float end=Time.realtimeSinceStartup+30;while(loop.ActualMatchRules==null&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;
            Need(loop.ActualMatchRules!=null,"Actual source team-revive rules active");
            loop.UI.OpenMatchRules();Pointer(loop.UI,Button(loop.UI,"LOBBY"),false);yield return null;
            Need(loop.ActualMatchRules==null&&loop.UI.LocalMatchLifeState==null,"Lobby action clears actual round/life state");
            Need(Vector3.Distance((Vector3)Read(loop.Rig,"spawn"),loop.Map.WarmupSpawn)<.001f,"Lobby action restores warmup spawn");
            loop.UI.OpenMatchRules();loop.UI.SelectMatchPreset("team_revive");Need(loop.UI.RequestMatchStart(),"Second actual round start");
            end=Time.realtimeSinceStartup+30;while(loop.ActualMatchRules==null&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;
            loop.ReturnToLobby();loop.UI.OpenMaps();Need(loop.UI.ChooseMap(map),"Same map revisit card");yield return null;
            Need(loop.ActualMatchRules==null&&loop.Gameplay.Health>0&&loop.UI.LocalMatchLifeState==null,"Same map revisit clears prior match/health modal");
            Need(Vector3.Distance((Vector3)Read(loop.Rig,"spawn"),loop.Map.WarmupSpawn)<.001f,"Same map revisit is lobby-first");
            loop.Gameplay.ApplyPlayerDamage(1000);loop.SmokeUiUpdate();Need(loop.UI.Page==R23UiPage.Dead,"Actual local zero-health modal");
            Pointer(loop.UI,Button(loop.UI,"HOME"),false);yield return null;
            Need(loop.UI.Page!=R23UiPage.Dead,"Actual Home callback escapes dead modal");
            loop.UI.OpenMaps();Need(loop.UI.ChooseMap(map),"Map entry after Home escape");
            for(int i=0;i<2;i++){loop.RestartRound();loop.SmokeUiUpdate();Need(loop.Gameplay.Health>0&&loop.UI.Page!=R23UiPage.Dead&&loop.ActualMatchRules==null,"Local restart remains alive/lobby and not trapped");}
            note("Actual map card, team-revive start, Lobby pointer, same-map revisit, induced legacy death/Home and two restarts passed. Original intermittent human trap not reproduced.");
        }
        static IEnumerator Handedness(R23PlayableLoop loop,Action<string> note)
        {
            loop.Gameplay.HolsterCurrent();
            foreach(int primary in new[]{0,1})
            {
                Need(loop.Gameplay.SetPreferredHandWhenHolstered(primary),"Actual holstered preferred hand "+primary);
                var a=loop.Activities;a.ResetRound();var f=new InputFrame{Tracked=true,Tick=990000};
                a.TickConstruction(f,true,true,false,false,primary);
                f.TriggerLeft=primary==1;f.TriggerRight=primary==0;f.Tick++;a.TickConstruction(f,true,true,false,false,primary);
                Need(!a.BuildActive,"Support trigger does not independently build for hand "+primary);
                f.TriggerLeft=f.TriggerRight=false;f.Tick++;a.TickConstruction(f,true,true,false,false,primary);
                f.TriggerLeft=primary==0;f.TriggerRight=primary==1;f.Tick++;a.TickConstruction(f,true,true,false,false,primary);
                Need(a.BuildActive,"Selected dominant trigger admits gesture for hand "+primary);
                a.ResetRound();note("Actual builder primary="+primary+": support rejected, dominant gesture accepted; no controller transforms changed.");yield return null;
            }
            Need(loop.Gameplay.SetPreferredHandWhenHolstered(loop.UI.Settings.DominantHand),"Restore selected preference");
        }
        static IEnumerator BackpackPaddles(R23PlayableLoop loop,Action<string> note)
        {
            loop.ReturnToLobby();loop.UI.OpenMaps();Need(loop.UI.ChooseMap("range"),"Actual Range card for original checkpoint backpacks");
            float ready=Time.realtimeSinceStartup+45;while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<ready)yield return null;
            Need(loop.Ready&&loop.Map.MapId=="range","Actual Range checkpoint loaded: "+loop.Failure);
            loop.RestartRound();loop.Supplies.SetWorldVisible(true);loop.UI.SetCreativeCatalog(true);loop.UI.OpenLoadout();
            // Last catalog page contains all three utility cards, including backpack.
            for(int i=0;i<30;i++)loop.UI.ScrollBackpack(-1,.1f,true);
            loop.UI.RefreshTextLayout();
            object bagButton=Button(loop.UI,null,"catalog/item/backpack");var surface=(Transform)Read(bagButton,"Surface");
            bool icon=false;foreach(var renderer in surface.GetComponentsInChildren<MeshRenderer>(true))if(renderer.name.Contains("backpack_r23_backpack"))icon=true;
            Need(icon,"Genuine source backpack icon rendered on actual card");
            var footer=(TextMesh)Read(loop.UI,"loadoutStatusText");float top=Single.NegativeInfinity;
            foreach(var b in (IEnumerable)Read(loop.UI,"buttons"))
            {
                string id=(string)Read(b,"OwnedId");if(id==null||!id.StartsWith("catalog/",StringComparison.Ordinal))continue;
                var s=(Transform)Read(b,"Surface");var size=(Vector2)Read(b,"Size");
                float lower=s.localPosition.y-size.y*.5f;if(top==Single.NegativeInfinity||lower<top)top=lower;
            }
            Need(footer.transform.localPosition.y+.018f<top,"Actual creative footer clears bottom card bounds");
            var rows=(IDictionary)Read(loop.Supplies,"boxes");var views=new List<GameObject>();
            for(int i=9;i<=11;i++){string id="r24-checkpoint/backpack/"+i;Need(rows.Contains(id),"Actual checkpoint bag "+id);views.Add((GameObject)Read(rows[id],"View"));}
            for(int i=0;i<views.Count;i++)Need(views[i]!=null&&views[i].activeInHierarchy&&views[i].GetComponentsInChildren<MeshRenderer>().Length>0,"Actual uncollected checkpoint model "+i);
            loop.UI.SetPlaying();loop.Supplies.UpdateInputAvailability(new Ashfall.Integration.Playable.Consumables.R24SupplyInputFrame{Enabled=true,LeftEligible=true,RightEligible=true});
            var chosen=views[0];loop.Rig.Right.position=chosen.transform.position;Physics.SyncTransforms();
            Need(loop.Supplies.TryCollect("r24-checkpoint/backpack/9",2)==1,"Actual first checkpoint bag collected");
            Need(!views[0].activeSelf&&views[1].activeInHierarchy&&views[2].activeInHierarchy,"One collection hides only its actual model");
            note("Checkpoint bag actual views: first hidden; other two retain separate active renderers; capacity="+loop.InventoryCapacity+". This does not reproduce the human all-models disappearance.");
            loop.RestartRound();loop.UI.SetCreativeCatalog(true);loop.UI.OpenLoadout();for(int i=0;i<30;i++)loop.UI.ScrollBackpack(-1,.1f,true);
            Pointer(loop.UI,Button(loop.UI,null,"catalog/utility/revive"),true);Call(loop,"ApplyLoadoutRequests");
            note("Paddle Root callback: "+loop.LastCreativeResult+" active="+loop.Paddles.State.Active+" tracked="+loop.Rig.Tracking+" focused="+loop.Rig.SessionFocused);
            Need(loop.Paddles.State.Active,"Actual creative revive cross equips reusable paddle owner");
            loop.StowPaddles();loop.UI.SetCreativeCatalog(false);loop.UI.OpenLoadout();Pointer(loop.UI,Button(loop.UI,null,"utility/revive"),true);
            Need(loop.Paddles.State.Active,"Actual standard backpack revive button equips reusable paddle owner");
            loop.StowPaddles();loop.UI.SetCreativeCatalog(true);loop.UI.SetPlaying();yield return null;
        }
        static IEnumerator Cloth(R23PlayableLoop loop,Action<string> note)
        {
            var originals=new Dictionary<MeshRenderer,Material>();var controls=new List<Material>();var layers=new Dictionary<GameObject,int>();Camera camera=null;
            string output=Path.ChangeExtension(R23PlayableBuild.Argument("-ashfallVRSmoke"),null)+"-images";Directory.CreateDirectory(output);
            loop.ReturnToLobby();float rotation=loop.Preview.RotationMultiplier;loop.Preview.RotationMultiplier=0;
            try
            {
                var shader=Resources.Load<Shader>("R23AppearanceDoubleSided");var before=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Ashfall/Editor/Playable/ArtControls/R23Appearance6237Before.shader");Need(shader!=null&&before!=null,"Current and exact6237 shader control");
                string[][] sets={new[]{"_EMISSION","_METALLICSPECGLOSSMAP","_NORMALMAP","_OCCLUSIONMAP","_SPECULAR_SETUP"},new[]{"_METALLICSPECGLOSSMAP","_NORMALMAP","_SPECULAR_SETUP"},new[]{"_SPECULAR_SETUP"}};
                var variants=new ShaderVariantCollection();try{foreach(var set in sets)variants.Add(new ShaderVariantCollection.ShaderVariant(shader,PassType.ScriptableRenderPipeline,set));variants.WarmUp();}finally{UnityEngine.Object.Destroy(variants);}
                camera=new GameObject("Owned6237 cloth comparison camera").AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=40;camera.cullingMask=1<<30;camera.nearClipPlane=.03f;camera.farClipPlane=20;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.045f,.055f,1);camera.allowHDR=false;
                foreach(string style in new[]{"claude","astra","obsidian","pj_golden"})
                {
                    Need(loop.Preview.Show(style),"Actual preview style "+style);float end=Time.realtimeSinceStartup+40;while((loop.Preview.Loading||loop.Preview.SelectedStyleId!=style)&&loop.Preview.Failure==null&&Time.realtimeSinceStartup<end)yield return null;
                    Need(loop.Preview.Ready&&!loop.Preview.Loading&&loop.Preview.SelectedStyleId==style,"Actual committed style "+style+": "+loop.Preview.Failure);yield return null;
                    var appearance=(R23PlayerAppearance)Read(loop.Preview,"appearance");var avatar=(Transform)Read(loop.Preview,"avatar");
                    foreach(var root in new[]{avatar,appearance.CurrentVisualRoot})foreach(var t in root.GetComponentsInChildren<Transform>(true)){if(!layers.ContainsKey(t.gameObject))layers[t.gameObject]=t.gameObject.layer;t.gameObject.layer=30;}
                    int affected=0;foreach(var r in appearance.CurrentVisualRoot.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        var m=r.sharedMaterial;originals[r]=m;var control=new Material(m);controls.Add(control);
                        if(m.shader==shader&&m.IsKeywordEnabled("_SPECULAR_SETUP")){control.shader=before;control.DisableKeyword("_SPECULAR_SETUP");affected++;}
                    }
                    Need(style=="claude"||style=="astra"?affected>0:affected==0,"Only intended cloth styles changed: "+style+" affected="+affected);
                    var center=avatar.position+Vector3.up*.92f;var eye=avatar.position+avatar.forward*2.8f+avatar.right*.85f+Vector3.up*1.15f;camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(center-eye,Vector3.up));
                    for(int mode=0;mode<2;mode++)
                    {
                        int index=0;foreach(var pair in originals){pair.Key.sharedMaterial=mode==0?controls[index]:pair.Value;index++;}
                        string path=Path.Combine(output,style+(mode==0?"-6237-control.png":"-selective-cloth.png"));Capture(camera,path);
                        foreach(var pair in originals)foreach(var message in ShaderUtil.GetShaderMessages(pair.Key.sharedMaterial.shader))Need(message.severity.ToString()!="Error",message.message);
                        note("UNMATCHED Unity-only "+style+" "+(mode==0?"6237 control":"selective cloth")+" image="+path+" eye="+eye.ToString("F6")+" target="+center.ToString("F6")+" FOV40; identical camera/geometry/lighting.");
                    }
                    Restore(originals,controls,layers);
                }
                note("Three requested DX11 shader variants warmed; actual four outfit images rendered. Android stripping/HMD gloss remain unverified. Obsidian and PJ Gold material paths excluded.");
            }
            finally{Restore(originals,controls,layers);if(camera!=null)UnityEngine.Object.Destroy(camera.gameObject);loop.Preview.RotationMultiplier=rotation;}
        }
        static void Restore(Dictionary<MeshRenderer,Material> originals,List<Material> controls,Dictionary<GameObject,int> layers)
        {foreach(var p in originals)if(p.Key!=null)p.Key.sharedMaterial=p.Value;originals.Clear();foreach(var m in controls)if(m!=null)UnityEngine.Object.Destroy(m);controls.Clear();foreach(var p in layers)if(p.Key!=null)p.Key.layer=p.Value;layers.Clear();}
        static void Capture(Camera camera,string path)
        {Need(!File.Exists(path),"Fresh image path");var target=RenderTexture.GetTemporary(640,800,24,RenderTextureFormat.ARGB32);var old=camera.targetTexture;var active=RenderTexture.active;Texture2D pixels=null;try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels=new Texture2D(640,800,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,640,800),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}finally{camera.targetTexture=old;RenderTexture.active=active;if(pixels!=null)UnityEngine.Object.Destroy(pixels);RenderTexture.ReleaseTemporary(target);}}
    }
}
#endif

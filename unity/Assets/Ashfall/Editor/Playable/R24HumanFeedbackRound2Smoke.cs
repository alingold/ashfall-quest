#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.Calibration;
using Ashfall.Integration.Playable.World;
using Ashfall.Modules.Player;
using UnityEngine;
namespace Ashfall.Editor.Playable
{
    public static partial class R24Feedback6237Smoke
    {
        static IEnumerator Round2MapVitals(R23PlayableLoop loop, Action<string> note)
        {
            note("ROUND2_MAP_VITALS actual Root map deployment fixture entered");
            loop.ReturnToLobby();loop.UI.OpenMaps();Need(loop.UI.ChooseMap("range"),"Actual Range card");
            float end=Time.realtimeSinceStartup+45;
            while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;
            Need(loop.Ready&&loop.Map.MapId=="range","Actual Range loaded");loop.StartPlaying();
            loop.Gameplay.ApplyPlayerDamage(150);Need(loop.Gameplay.Health==50&&loop.Gameplay.Shield==0,"Real exploration Health injured");
            string owned=String.Join(",",loop.Gameplay.OwnedWeaponActionIds);int reserve=loop.Gameplay.ReadStoredAmmoReserveFamily("rifle");
            loop.ReturnToLobby();loop.UI.OpenMaps();Need(loop.UI.ChooseMap(R23MapCatalog.PaintballId),"Actual Paintball card");
            end=Time.realtimeSinceStartup+45;
            while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;
            Need(loop.Ready&&loop.Map.MapId==R23MapCatalog.PaintballId,"Actual second map loaded");loop.StartPlaying();
            Need(loop.Gameplay.Health==100&&loop.Gameplay.Shield==100,"Actual map deployment restores full vitals");
            Need(String.Join(",",loop.Gameplay.OwnedWeaponActionIds)==owned&&loop.Gameplay.ReadStoredAmmoReserveFamily("rifle")==reserve,
                "Map reset does not replace carried owners or grant rifle ammo");
            loop.Gameplay.ApplyPlayerDamage(25);Need(loop.Gameplay.Shield==75,"Actual shield injury");
            loop.UI.ToggleMenu();loop.UI.ToggleMenu();Need(loop.Gameplay.Shield==75,"Pause-menu toggle does not heal");
            long generation=loop.Gameplay.LocalMatchHealth.LifeGeneration;
            loop.ReturnToLobby();loop.UI.OpenMaps();Need(loop.UI.ChooseMap(R23MapCatalog.PaintballId),"Actual same-map reentry");loop.StartPlaying();
            Need(loop.Gameplay.Health==100&&loop.Gameplay.Shield==100&&loop.Gameplay.LocalMatchHealth.LifeGeneration>generation,
                "Explicit same-map deployment creates fresh actual Health life");
            note("Actual Range->Paintball deployment and same-map reentry restore vitals; menu toggle retains injury; carried IDs/ammo retained. No map geometry acceptance.");
        }
        static IEnumerator Round2Crouch(R23PlayableLoop loop, Action<string> note)
        {
            note("ROUND2_CROUCH actual anchored Root minimum-height fixture entered");
            GameObject controls=null;
            try
            {
                loop.ReturnToLobby();loop.UI.OpenMaps();Need(loop.UI.ChooseMap("range"),"Actual Range card");
                float end=Time.realtimeSinceStartup+45;while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;
                Need(loop.Ready&&loop.Map.MapId=="range","Actual Range loaded");loop.StartPlaying();
                controls=new GameObject("Declared round2 crouch floor/wall");controls.transform.SetParent(loop.Map.transform,false);
                CrouchBox(controls.transform,"Declared crouch floor",new Vector3(110,-.1f,-600),new Vector3(20,.2f,20));
                CrouchBox(controls.transform,"Declared crouch side wall",new Vector3(110.65f,1.5f,-600),new Vector3(.2f,3,8));Physics.SyncTransforms();
                foreach(int handIndex in new[]{0,1})
                {
                    loop.RestartRound();loop.Gameplay.HolsterCurrent();loop.Rig.SetSpawn(new Vector3(110,.04f,-600),new Vector3(110,.04f,-590));loop.Calibration.NotifyRootPoseReset();
                    loop.Rig.Head.transform.localPosition=new Vector3(0,R23CalibrationState.TargetEye,0);
                    loop.Rig.Left.localPosition=new Vector3(-.25f,1.15f,.35f);loop.Rig.Right.localPosition=new Vector3(.25f,1.15f,.35f);
                    var hand=handIndex==0?loop.Rig.Left:loop.Rig.Right;hand.position=new Vector3(110.5f,1.2f,-599.7f);
                    loop.Rig.SmokeTrackedHands(new HandSample{Tracked=true},new HandSample{Tracked=true});loop.SmokeUiUpdate();
                    for(int frame=0;frame<12;frame++)CrouchRootFrame(loop,handIndex,false);
                    Need(loop.Rig.Supported,"Real supported standing start");CrouchRootFrame(loop,handIndex,true);
                    Need(handIndex==0?loop.Climbing.LeftAnchored:loop.Climbing.RightAnchored,"Actual selected wall anchor");
                    var raw=loop.Rig.Head.transform.localPosition;var origin=loop.Rig.TrackingOrigin.localPosition;
                    float first=loop.Rig.Head.transform.position.y;
                    for(int frame=0;frame<180;frame++)
                    {
                        hand.localPosition+=Vector3.up*.01f;float before=loop.Rig.Head.transform.position.y;CrouchRootFrame(loop,handIndex,true);
                        float eye=loop.Rig.Head.transform.position.y;
                        if(frame==0)Need(before-eye>.009f&&before-eye<.011f,"Immediate continuous first centimetre");
                        Need(eye>=R23CalibrationState.TargetEye*.5f-.001f,"Actual eye stays above half-height floor");
                        Need(loop.Rig.Head.transform.localPosition==raw&&loop.Rig.TrackingOrigin.localPosition==origin,"Raw head and calibration origin untouched");
                    }
                    Need(first-loop.Rig.Head.transform.position.y>.75f&&Mathf.Abs(loop.Rig.Head.transform.position.y-R23CalibrationState.TargetEye*.5f)<.015f,
                        "Actual anchored pull reaches then respects minimum");
                    loop.SmokeUiUpdate();Call(loop.Appearance,"Present",0f);var body=Call(loop.Appearance,"ReadBodyFrame");
                    Need(Mathf.Abs((float)Read(body,"GroundY"))<.015f&&Mathf.Abs((float)Read(body,"Height")-R23CalibrationState.TargetEye*.5f)<.02f,
                        "Rendered body uses physical floor and lowered stature, rather than underground logical feet");
                    note("physical="+handIndex+" actualEye="+loop.Rig.Head.transform.position.y+" floor="+loop.Rig.CrouchFloorY+" logicalRoot="+loop.Rig.transform.position.y+
                        ";180 real Root pull frames/minimum/body-frame/raw tracking pass. Knee appearance and HMD comfort remain unqualified.");
                    yield return null;
                }
            }
            finally{if(controls!=null){controls.SetActive(false);UnityEngine.Object.Destroy(controls);}loop.RestartRound();}
        }
        static IEnumerator Round2HudCapture(R23PlayableLoop loop, Action<string> note)
        {
            note("ROUND2_HUD_CAPTURE actual Head camera with declared desktop90degree vertical FOV");
            float originalFov=loop.Rig.Head.fieldOfView;
            try
            {
                loop.ReturnToLobby();loop.UI.OpenMatchRules();loop.UI.SelectMatchPreset("team_respawn");
                Need(loop.UI.RequestMatchStart(),"Actual TDM capture setup");
                float end=Time.realtimeSinceStartup+30;while(!loop.MatchActive&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;
                Need(loop.MatchActive,"Actual match for capture");loop.SmokeUiUpdate();loop.UI.RefreshTextLayout();
                foreach(var renderer in loop.UI.CompactMatchHudRoot.parent.GetComponentsInChildren<MeshRenderer>(true))
                    if(renderer.sharedMaterial!=null)C1Shader(renderer.sharedMaterial.shader);
                string output=Path.ChangeExtension(R23PlayableBuild.Argument("-ashfallVRSmoke"),null)+"-images";Directory.CreateDirectory(output);
                string path=Path.Combine(output,"round2-hud-full-desktop90-unmatched.png");
                loop.Rig.Head.fieldOfView=90;CaptureTdmHead(loop,path);
                note("UNMATCHED actualHead1200x800 image="+path+" fixture-onlyFOV90, originalFOV="+originalFov+
                    "; fullHUD view requested, not actualHMDprojection proof. Actual personal="+loop.UI.CompactPersonalScore+
                    "; no accepted death/respawn/clock-expiry suite replay.");
            }
            finally{loop.Rig.Head.fieldOfView=originalFov;loop.EndMatch();loop.ReturnToLobby();}
        }
        static IEnumerator Round2Hud(R23PlayableLoop loop, Action<string> note)
        {
            note("ROUND2_HUD actual TDM personal-death/glyph layout fixture entered");
            try
            {
                loop.ReturnToLobby();loop.UI.OpenMatchRules();loop.UI.SelectMatchPreset("team_respawn");
                Need(loop.UI.SetMatchRule("respawn_delay",1f)&&loop.UI.RequestMatchStart(),"Actual TDM start");
                float end=Time.realtimeSinceStartup+30;while(!loop.MatchActive&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;
                Need(loop.MatchActive,"Actual match starts");int before=loop.Gameplay.LocalMatchHealth.Deaths;
                Need(loop.Gameplay.ApplyLocalMatchFall(),"Actual Health fall death");loop.TickMatch(.02f,true);
                Need(loop.UI.CompactPersonalScore.EndsWith("D "+(before+1)),"Actual death immediately appears in personal HUD");
                int frames=0;while(loop.Gameplay.Health<=0&&frames++<100)loop.TickMatch(.02f,true);
                loop.SmokeUiUpdate();loop.UI.RefreshTextLayout();
                Need(loop.Gameplay.Health>0&&loop.UI.CompactPersonalScore.EndsWith("D "+(before+1)),"Actual respawn retains personal death count");
                var root=loop.UI.CompactMatchHudRoot;var hud=root.parent;
                Need(Mathf.Abs(root.Find("Source score backdrop").localScale.x-hud.Find("Health track").localScale.x)<.000001f,
                    "Score strip aligns exactly with health-track width");
                foreach(string name in new[]{"compactLeftText","compactRightText","compactCapText","compactClockText","compactPersonalText","healthText","shieldText"})
                {
                    var text=(TextMesh)Read(loop.UI,name);Need(text!=null&&!String.IsNullOrEmpty(text.text),"Actual populated HUD label "+name);
                    var renderer=text.GetComponent<MeshRenderer>();var center=text.transform.TransformPoint(renderer.localBounds.center);
                    var local=text.transform.parent.InverseTransformPoint(center);float expected=name=="healthText"?-.456f:name=="shieldText"?-.433f:
                        name=="compactClockText"?-.406f:name=="compactPersonalText"?-.4185f:-.389f;
                    Need(Mathf.Abs(local.y-expected)<.0001f,"Actual generated glyph centre "+name+" y="+local.y);
                }
                string output=Path.ChangeExtension(R23PlayableBuild.Argument("-ashfallVRSmoke"),null)+"-images";Directory.CreateDirectory(output);
                string image=Path.Combine(output,"round2-tdm-hud-unmatched.png");CaptureTdmHead(loop,image);
                note("Actual K/D="+loop.UI.CompactPersonalScore+"; clock="+loop.UI.CompactMatchClock+"; one UNMATCHED1200x800 actualHead image="+image+
                    ". Glyph bounds are actual native measurements; personal visual inspection still required. No shared kill authority inferred.");
            }
            finally{loop.EndMatch();loop.ReturnToLobby();}
        }
    }
}
#endif

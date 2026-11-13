#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;using System.Collections;using System.Collections.Generic;using System.IO;
using Ashfall.Integration.Playable;using Ashfall.Integration.Playable.UI;using Ashfall.Modules.HealthStates;using UnityEngine;using UnityEditor;
namespace Ashfall.Editor.Playable
{
 public static partial class R24Feedback6237Smoke
 {
  static IEnumerator TdmHud98254(R23PlayableLoop loop,Action<string> note)
  {
   note("TDM98254 actual Root rules/clock/local-score HUD fixture entered");var settings=loop.UI.Settings;string output=Path.ChangeExtension(R23PlayableBuild.Argument("-ashfallVRSmoke"),null)+"-images";Directory.CreateDirectory(output);
   try
   {
    loop.ReturnToLobby();loop.UI.OpenMaps();Need(loop.UI.ChooseMap("range"),"Actual Range");float end=Time.realtimeSinceStartup+60;while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;Need(loop.Ready&&loop.Map.MapId=="range","Range loaded");
    loop.UI.OpenMatchRules();loop.UI.SelectMatchPreset("team_respawn");Need(!loop.UI.SelectedMatchRules.ZoneEnabled,"Team deathmatch snapshot has no zone");Need(!loop.UI.SetMatchRule("zone_enabled",true),"Actual TDM zone toggle refuses input");
    Need(loop.UI.SetMatchRule("time_limit",60)&&loop.UI.SetMatchRule("respawn_delay",1f)&&loop.UI.SetMatchRule("protection",0f),"Actual finite source rule controls");
    Pointer(loop.UI,Button(loop.UI,"ZONE"),false);int disabled=0;foreach(var b in (IEnumerable)Read(loop.UI,"buttons")){string help=(string)Read(b,"Help");if(help!=null&&help.StartsWith("Team deathmatch and practice",StringComparison.Ordinal)){Need(!(bool)Read(b,"Enabled"),"Actual zone row button disabled");disabled++;}}
    Need(disabled==4,"All four actual zone controls disabled for team respawn");yield return null;CaptureTdmHead(loop,Path.Combine(output,"tdm-zone-controls-unmatched.png"));
    Need(loop.UI.RequestMatchStart(),"Actual Start Match request");end=Time.realtimeSinceStartup+30;while(!loop.MatchActive&&loop.Failure==null&&Time.realtimeSinceStartup<end)yield return null;
    Need(loop.MatchActive&&loop.ActualMatchRules.Mode=="respawn"&&loop.ActualMatchRules.Teams==2&&loop.ActualMatchRules.TimeLimit==60,"Actual team deathmatch starts");
    Need(!loop.ActualMatchRules.ZoneEnabled&&loop.MatchZoneOwner!=null&&!loop.MatchZoneOwner.Rules.Enabled&&loop.MatchZoneOwner.State==null&&(loop.MatchZone==null||!loop.MatchZone.Visible),"No zone host plan or visible zone in actual TDM");
    loop.SmokeUiUpdate();Need(loop.UI.CompactMatchHudVisible&&loop.UI.CompactMatchClock=="TIME 01:00","Actual gameplay timer appears without opening match rules");
    var initial=loop.ReadWorldScoreState();Need(initial.Rows.Length==2&&initial.Rows[0].Score==0&&initial.Rows[1].Score==0&&initial.Players.Length==1&&initial.Players[0].Kills==0,"Existing local round scorer initializes actual local totals to zero");
    int deaths=loop.Gameplay.LocalMatchHealth.Deaths;Need(loop.Gameplay.ApplyLocalMatchFall(),"Actual Health fall transition");loop.TickMatch(.02f,true);var fallen=loop.ReadWorldScoreState();
    Need(fallen.Players[0].Deaths==deaths+1&&fallen.Players[0].Kills==0&&fallen.Rows[0].Score==0&&fallen.Rows[1].Score==0,"Real local fall increments death without inventing a kill");
    Need(loop.UI.GhostHudVisible&&loop.UI.CompactMatchHudVisible,"Compact score/timer and source ghost notice coexist");
    int respawnFrames=0;while(loop.Gameplay.LocalMatchHealth.Life!=LifeState.Alive&&respawnFrames<100){loop.TickMatch(.02f,true);respawnFrames++;}
    Need(loop.Gameplay.LocalMatchHealth.Life==LifeState.Alive&&loop.Gameplay.LocalMatchHealth.Deaths==deaths+1,"Real clock-driven respawn retains recorded death");
    loop.SmokeUiUpdate();var actual=loop.ReadWorldScoreState();Need(loop.UI.CompactMatchClock=="TIME "+actual.ClockText&&actual.Seconds<=59&&actual.Seconds>=58,"HUD and world boards use same actual remaining second");
    Need(loop.UI.CompactOwnScore=="0"&&loop.UI.CompactOtherScore=="0","Actual local team score captions");var root=loop.UI.CompactMatchHudRoot;Need(root.GetComponentsInChildren<Collider>(true).Length==0,"HUD has no input or physics colliders");
    Need(root.Find("Source score backdrop").GetComponent<MeshRenderer>().sortingOrder<root.Find("Own score plate").GetComponent<MeshRenderer>().sortingOrder,"Actual score plates sort in front of backing");
    foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))foreach(var message in ShaderUtil.GetShaderMessages(renderer.sharedMaterial.shader))Need(message.severity.ToString()!="Error",message.message);
    yield return null;CaptureTdmHead(loop,Path.Combine(output,"tdm-gameplay-timer-unmatched.png"));
    note("Actual source-layout HUD: clock="+loop.UI.CompactMatchClock+" own/other="+loop.UI.CompactOwnScore+"/"+loop.UI.CompactOtherScore+" healthDeaths="+loop.Gameplay.LocalMatchHealth.Deaths+" respawnFrames="+respawnFrames+" headFOV="+loop.Rig.Head.fieldOfView+" twoUNMATCHED1200x800images. No shared combat kills or HMD acceptance inferred.");
    int frames=0;while(loop.MatchActive&&frames<3100){loop.TickMatch(.02f,true);frames++;}
    Need(!loop.MatchActive&&loop.MatchClock>=60&&loop.MatchClock<60.1,"Actual timer completes the match");Need(!loop.UI.CompactMatchHudVisible&&loop.ReadWorldScoreState().Seconds==0,"Gameplay HUD hides on results and final world clock stays zero");
    loop.ReturnToMapLobby();Need(loop.ActualMatchRules==null&&!loop.UI.CompactMatchHudVisible,"Actual lobby return clears compact match presentation");
    note("Actual UI TDM no-zone,60s simulated-frame clock expiry,one real Health death+respawn and single local score owner passed. LAN shared combat/kill credit remains unavailable and is not simulated here.");
   }
   finally{loop.EndMatch();loop.UI.SetSettings(settings);Call(loop,"ApplySettings",settings);loop.ReturnToLobby();}
  }
  static void CaptureTdmHead(R23PlayableLoop loop,string path)
  {
   Need(!File.Exists(path),"Fresh HUD capture");var camera=loop.Rig.Head;var target=RenderTexture.GetTemporary(1200,800,24,RenderTextureFormat.ARGB32);var old=camera.targetTexture;var active=RenderTexture.active;Texture2D pixels=null;
   try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels=new Texture2D(1200,800,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1200,800),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}
   finally{camera.targetTexture=old;RenderTexture.active=active;if(pixels!=null)UnityEngine.Object.Destroy(pixels);RenderTexture.ReleaseTemporary(target);}
  }
 }
}
#endif

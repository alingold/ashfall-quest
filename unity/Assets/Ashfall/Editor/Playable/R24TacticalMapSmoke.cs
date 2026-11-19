#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;using System.Collections;using System.IO;
using Ashfall.Core;using Ashfall.Modules.Player;using Ashfall.Modules.Modes;using Ashfall.Integration.Playable;using Ashfall.Integration.Playable.HandsWeapons;using Ashfall.Integration.Playable.UI;using UnityEngine;using UnityEditor;using UnityEngine.Rendering.Universal;
namespace Ashfall.Editor.Playable
{
    public static partial class R24Feedback6237Smoke
    {
        static IEnumerator TacticalImage(R23PlayableLoop loop,Action<string> note,bool alpha)
        {
            var original=loop.UI.Settings;try
            {
                loop.ReturnToLobby();loop.UI.OpenMaps();string id=alpha?"archive:alfa-aglae-souza":"range";Need(loop.UI.ChooseMap(id),"Actual map card");float until=Time.realtimeSinceStartup+90;while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<until)yield return null;Need(loop.Ready&&loop.Map.MapId==id,"Actual image map loaded");loop.StartPlaying();loop.Gameplay.HolsterCurrent();Call(loop,"ApplySettings",original.With(dominantHand:1));Need(loop.Gameplay.SetPreferredHandWhenHolstered(1),"Actual hand mode");for(int i=0;i<8;i++)loop.Rig.SmokeXrMove(Vector2.zero,.02f);
                int captures=loop.TacticalMap.OverviewCaptures;loop.Rig.SmokeTrackedHands(new HandSample{Tracked=true,Trigger=1},new HandSample{Tracked=true});loop.SmokeUiUpdate();Need(loop.TacticalMap.Opened,"Actual support hold displays image board");
                until=Time.realtimeSinceStartup+30;while(loop.TacticalMap.TopImage==null&&loop.TacticalMap.Failure==null&&Time.realtimeSinceStartup<until)yield return null;Need(loop.TacticalMap.TopImage!=null&&loop.TacticalMap.Failure==null,"Actual top image assigned");
                if(alpha)Need(loop.TacticalMap.Overview!=null&&loop.TacticalMap.OverviewDepthBits>=16&&loop.TacticalMap.OverviewCaptures==captures+1,"One actual loaded Alpha scene render");
                if(alpha)note("Actual overview ownership: "+loop.TacticalMap.OverviewCaptureInfo);bool nonblank=ReadActualMapPixels(loop.TacticalMap.TopImage,note,alpha);loop.TacticalMap.Refresh();CaptureTactical(loop,alpha?"alpha":"range");Need(nonblank,"Actual top image has nonblank world/source color");loop.ReturnToLobby();Need(!loop.TacticalMap.Opened,"Actual withdrawal hides board");
            }finally{loop.TacticalMap.Close();loop.ReturnToLobby();Call(loop,"ApplySettings",original);}yield return null;
        }
        static bool ReadActualMapPixels(Texture source,Action<string> note,bool alpha)
        {
            var active=RenderTexture.active;Texture2D pixels=null;RenderTexture target=null;try
            {
                var actual=source as RenderTexture;
                if(actual!=null)
                {
                    // Read the destination itself. A scaled Graphics.Blit is a
                    // second rendering path and is not an oracle for whether
                    // the original camera's render target contains pixels.
                    RenderTexture.active=actual;pixels=new Texture2D(actual.width,actual.height,TextureFormat.RGBA32,false);pixels.ReadPixels(new Rect(0,0,actual.width,actual.height),0,0);
                }
                else
                {
                    target=RenderTexture.GetTemporary(128,128,0,RenderTextureFormat.ARGB32);Graphics.Blit(source,target);RenderTexture.active=target;pixels=new Texture2D(128,128,TextureFormat.RGBA32,false);pixels.ReadPixels(new Rect(0,0,128,128),0,0);
                }
                pixels.Apply();var colors=pixels.GetPixels();float min=1,max=0,minAlpha=1,maxAlpha=0,minLuma=1,maxLuma=0;Vector3 average=Vector3.zero;foreach(var c in colors){average+=new Vector3(c.r,c.g,c.b);min=Mathf.Min(min,c.r,c.g,c.b);max=Mathf.Max(max,c.r,c.g,c.b);minAlpha=Mathf.Min(minAlpha,c.a);maxAlpha=Mathf.Max(maxAlpha,c.a);float luma=c.r*.2126f+c.g*.7152f+c.b*.0722f;minLuma=Mathf.Min(minLuma,luma);maxLuma=Mathf.Max(maxLuma,luma);}average/=colors.Length;note("Actual "+(alpha?(actual!=null?"DIRECT Alpha scene RT":"Alpha frozen GPU snapshot"):"Range imported PNG")+" source="+source.width+"x"+source.height+" RGBmean="+average+" min="+min+" max="+max+" rawAlpha="+minAlpha+".."+maxAlpha+" SPATIALluma="+minLuma+".."+maxLuma);return maxLuma-minLuma>.03f&&average.magnitude>.05f&&(alpha||average.z>.15f);
            }finally{RenderTexture.active=active;if(pixels!=null)UnityEngine.Object.Destroy(pixels);if(target!=null)RenderTexture.ReleaseTemporary(target);}
        }
        static IEnumerator TacticalMap(R23PlayableLoop loop,Action<string> note)
        {
            var settings=loop.UI.Settings;try
            {
                loop.ReturnToLobby();loop.UI.OpenMaps();Need(loop.UI.ChooseMap("range"),"Actual Range card");float until=Time.realtimeSinceStartup+60;while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<until)yield return null;Need(loop.Ready&&loop.Map.MapId=="range","Actual Range loaded");loop.StartPlaying();
                int capacity=loop.InventoryCapacity;var inventory=string.Join("|",loop.Gameplay.OwnedWeaponActionIds);var map=loop.TacticalMap;Need(map!=null,"Actual tactical runtime composed");
                foreach(int hand in new[]{1,0})
                {
                    loop.Gameplay.HolsterCurrent();Call(loop,"ApplySettings",settings.With(dominantHand:hand));Need(loop.Gameplay.SetPreferredHandWhenHolstered(hand),"Actual preferred hand");for(int i=0;i<8;i++)loop.Rig.SmokeXrMove(Vector2.zero,.02f);
                    var left=new HandSample{Tracked=true};var right=left;loop.Rig.SmokeTrackedHands(left,right);loop.StepTacticalMap(left,right,true);Need(!map.Opened,"Neutral hold closed");
                    var primary=left;primary.Trigger=1;if(hand==0)left=primary;else right=primary;loop.StepTacticalMap(left,right,true);Need(!map.Opened,"Dominant trigger alone never opens support map");
                    left.Trigger=right.Trigger=0;if(hand==0)right.Trigger=1;else left.Trigger=1;loop.Rig.SmokeTrackedHands(left,right);loop.SmokeUiUpdate();Need(map.Opened,"Actual Root support-trigger map for dominant "+hand);
                    Need(!loop.Rig.ConstructionInputAllowed,"Tactical view owns build input without blocking walking");Need(map.Board.GetComponentsInChildren<Collider>(true).Length==0,"Map board has no physical blockers");var head=loop.Rig.Head.transform;var savedHead=head.rotation;var savedBody=loop.Rig.transform.position;
                    try
                    {
                        var yaw=Quaternion.LookRotation(Vector3.ProjectOnPlane(head.forward,Vector3.up));
                        map.Sample(false,true);head.rotation=Quaternion.AngleAxis(180,Vector3.up)*yaw*Quaternion.Euler(24,0,0);
                        var forward=Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;
                        loop.StepTacticalMap(left,right,true);Need(map.Opened,"Support trigger opens after 180 degree head turn");
                        var expectedAnchor=head.position+forward*.8f-Vector3.up*.2f;
                        Need(Vector3.Distance(map.Board.position,expectedAnchor)<.001f,"Board opens .8m in front of turned head and .2m below eye level");
                        Need(Vector3.Dot(Vector3.ProjectOnPlane(map.Board.forward,Vector3.up).normalized,forward)>.9999f,"Board yaw matches turned head");
                        Vector3 pinned=map.Board.position;Quaternion facing=map.Board.rotation;
                        head.rotation=savedHead;loop.Rig.transform.position+=Vector3.right*.25f;
                        loop.StepTacticalMap(left,right,true);map.Refresh();
                        Need(Vector3.Distance(map.Board.position,pinned)<.001f&&Quaternion.Angle(map.Board.rotation,facing)<.001f,"Held board stays at world pose after head and body motion");
                    }
                    finally{loop.Rig.transform.position=savedBody;head.rotation=savedHead;map.Close();loop.StepTacticalMap(left,right,true);}
                    var h=loop.Rig.Left.position;var q=loop.Rig.Left.rotation;map.Refresh();Need(loop.Rig.Left.position==h&&Quaternion.Angle(loop.Rig.Left.rotation,q)<.001f,"Map never writes tracked pose");
                    var frame=loop.Rig.transform.position;var info=loop.LaunchMapCatalog;Ashfall.Integration.Playable.TeamLaunch.R24LaunchMapInfo row;Need(info.TryRead("range",out row),"Actual authored Range bounds");var origin=loop.Map.transform.TransformPoint(new Vector3(row.RepresentedOriginUnity.X,row.RepresentedOriginUnity.Y,row.RepresentedOriginUnity.Z));var expected=new TacticalProjection(new Float3(origin.x,origin.y,origin.z),row.CatalogMinX,row.CatalogMinZ,row.CatalogMaxX,row.CatalogMaxZ).Pixel(new Float3(frame.x,frame.y,frame.z));Need(Vector2.Distance(map.PlayerPixel,new Vector2((float)expected.X,(float)expected.Y))<.001f,"Actual represented Range origin reflected once");
                    note("Dominant="+hand+" actualRoot holdOpen=true playerPixel="+map.PlayerPixel+" heading="+map.PlayerHeading+" sourceOrigin="+origin+" trackingRetained=true");
                    loop.StepTacticalMap(left,right,false);Need(!map.Opened,"Focus loss closes immediately");loop.StepTacticalMap(left,right,true);Need(map.Opened,"Still held eligible support reopens like source");left.Trigger=right.Trigger=0;loop.StepTacticalMap(left,right,true);Need(!map.Opened,"Release closes");
                }
                loop.Rig.SmokeTrackedHands(new HandSample{Tracked=true},new HandSample{Tracked=true,Trigger=1});loop.SmokeUiUpdate();Need(map.Opened,"Left-handed support is physical right");CaptureTactical(loop,"range");
                Need(loop.InventoryCapacity==capacity&&string.Join("|",loop.Gameplay.OwnedWeaponActionIds)==inventory,"Range map hold preserves inventory/capacity");loop.UI.OpenMatchRules();loop.SmokeUiUpdate();Need(!map.Opened,"Actual field menu closes map");loop.UI.SetPlaying();
                loop.ReturnToLobby();loop.StepTacticalMap(new HandSample{Tracked=true,Trigger=1},new HandSample{Tracked=true,Trigger=1},true);Need(!map.Opened,"Actual skyscraper menu refuses tactical hold");
                loop.UI.OpenMaps();Need(loop.UI.ChooseMap("archive:alfa-aglae-souza"),"Actual Alpha card");until=Time.realtimeSinceStartup+90;while(!loop.Ready&&loop.Failure==null&&Time.realtimeSinceStartup<until)yield return null;Need(loop.Ready&&loop.Map.MapId=="archive:alfa-aglae-souza","Actual Alpha loaded");loop.StartPlaying();loop.Gameplay.HolsterCurrent();capacity=loop.InventoryCapacity;inventory=string.Join("|",loop.Gameplay.OwnedWeaponActionIds);for(int i=0;i<8;i++)loop.Rig.SmokeXrMove(Vector2.zero,.02f);
                var support=new HandSample{Tracked=true,Trigger=1};loop.Rig.SmokeTrackedHands(new HandSample{Tracked=true},support);loop.SmokeUiUpdate();Need(map.Opened,"Alpha actual hold opened");until=Time.realtimeSinceStartup+30;while(map.Overview==null&&map.Failure==null&&Time.realtimeSinceStartup<until)yield return null;Need(map.Overview!=null&&map.OverviewCaptures==1&&map.Failure==null,"Exactly one actual archive north-up capture");map.Refresh();Need(map.OverviewDepthBits>=16,"Overview URP depth attached");CaptureTactical(loop,"alpha");
                Need(loop.InventoryCapacity==capacity&&string.Join("|",loop.Gameplay.OwnedWeaponActionIds)==inventory,"Map presentation preserves real inventory/capacity");loop.ReturnToLobby();Need(!map.Opened,"Real map withdrawal closes board");note("Actual Range mirrored controls and Alpha one-shot overview; two UNMATCHED source-layout Unity-only board PNGs, no voice/network-combat/other-map/HMD acceptance.");
            }
            finally{loop.TacticalMap.Close();loop.ReturnToLobby();Call(loop,"ApplySettings",settings);}
        }
        static void CaptureTactical(R23PlayableLoop loop,string map)
        {
            foreach(var r in loop.TacticalMap.Board.GetComponentsInChildren<Renderer>(true))foreach(var material in r.sharedMaterials)if(material!=null)foreach(var m in ShaderUtil.GetShaderMessages(material.shader))Need(m.severity.ToString()!="Error",m.message);
            var go=new GameObject("UNMATCHED source-layout tactical capture");try{var camera=go.AddComponent<Camera>();camera.enabled=false;camera.GetUniversalAdditionalCameraData().allowXRRendering=false;camera.fieldOfView=58;camera.nearClipPlane=.02f;camera.farClipPlane=10;camera.cullingMask=1<<5;camera.transform.SetPositionAndRotation(loop.Rig.Head.transform.position,Quaternion.LookRotation(loop.TacticalMap.Board.forward,Vector3.up));camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.16f,.19f);string dir=Path.Combine(Path.GetDirectoryName(R23PlayableBuild.Argument("-ashfallVRSmoke")),"tactical-images");Directory.CreateDirectory(dir);CaptureTacticalLandscape(camera,Path.Combine(dir,map+"-tactical-unmatched.png"));}finally{UnityEngine.Object.Destroy(go);}
        }
        static void CaptureTacticalLandscape(Camera camera,string path){Need(!File.Exists(path),"Fresh image path");var target=RenderTexture.GetTemporary(1200,800,24,RenderTextureFormat.ARGB32);var active=RenderTexture.active;Texture2D pixels=null;try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;pixels=new Texture2D(1200,800,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1200,800),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}finally{camera.targetTexture=null;RenderTexture.active=active;if(pixels!=null)UnityEngine.Object.Destroy(pixels);RenderTexture.ReleaseTemporary(target);}}
    }
}
#endif

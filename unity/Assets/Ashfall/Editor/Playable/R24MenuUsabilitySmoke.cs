#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.UI;
using UnityEditor;
using UnityEngine;

namespace Ashfall.Editor.Playable
{
    public sealed class R24MenuUsabilitySmoke
    {
        [Serializable] sealed class ImageRecord {public string path,label,colorSpace;public Vector3 eye,forward,up;public float fov,aspect;public int width,height,samples;public bool matchedGodot=false;}
        [Serializable] sealed class Result
        {
            public bool success,nativeExecuted,menuPassed,settingsPassed,settingsExecuted,settingsRequested=true,headsetVerified=false,matchedGodot=false;
            public string error="",menuJson="";
            public List<string> settings=new List<string>(),shaderErrors=new List<string>();public List<ImageRecord> images=new List<ImageRecord>();
        }
        const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        const string PreferenceKey="ashfall.r23.personal-ui-settings.v1";
        readonly Result result=new Result();
        R23PlayableLoop loop;float deadline;string imageDirectory;
        static object Read(object value,string name)
        {var field=value.GetType().GetField(name,Fields);if(field==null)throw new InvalidOperationException("Missing actual UI field "+name);return field.GetValue(value);}
        static void Need(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}

        public IEnumerator Run()
        {
            deadline=Time.realtimeSinceStartup+360;
            result.settingsRequested=R23PlayableBuild.Argument("-ashfallMenuGalleryOnly")!="true";
            float readyUntil=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<readyUntil){loop=UnityEngine.Object.FindAnyObjectByType<R23PlayableLoop>();if(loop!=null&&(loop.Ready||loop.Failure!=null))break;yield return null;}
            if(loop==null||!loop.Ready||UnityEngine.XR.XRSettings.isDeviceActive){result.error="Actual ready XR-off Root required";Finish();yield break;}
            result.nativeExecuted=true;
            imageDirectory=Path.ChangeExtension(R23PlayableBuild.Argument("-ashfallVRSmoke"),null)+"-images";
            Directory.CreateDirectory(imageDirectory);
            loop.ReturnToLobby();yield return null;
            var menu=new R24MenuGalleryInteractionFixture();
            var run=Drive(menu.Run(loop,delegate(string text){Debug.Log("MENU USABILITY "+text);},Capture,R23PlayableBuild.Argument("-ashfallMenuGalleryFollowup")!="true"),"menu");
            while(run.MoveNext())yield return run.Current;
            result.menuPassed=menu.Passed;result.menuJson=menu.Json??"";
            if(!menu.Passed)result.error+="menu: "+menu.Error+"\n";
            if(result.settingsRequested){result.settingsExecuted=true;run=Drive(SettingsHelp(),"settings-help");while(run.MoveNext())yield return run.Current;}
            Finish();
        }
        IEnumerator Drive(IEnumerator work,string phase)
        {
            var stack=new Stack<IEnumerator>();stack.Push(work);
            try{
                while(stack.Count>0){bool more=false;object next=null;
                    try{Need(Time.realtimeSinceStartup<deadline-12,"360s bound with cleanup margin");more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;}
                    catch(Exception error){result.error+=phase+": "+error+"\n";Debug.LogError("MENU USABILITY FAIL "+phase+": "+error);break;}
                    if(!more){var disposable=stack.Pop() as IDisposable;if(disposable!=null)disposable.Dispose();continue;}
                    var nested=next as IEnumerator;if(nested!=null)stack.Push(nested);else yield return next;
                }
            }finally{while(stack.Count>0){var disposable=stack.Pop() as IDisposable;if(disposable!=null)disposable.Dispose();}}
        }
        object Button(string caption)
        {
            foreach(object button in (IEnumerable)Read(loop.UI,"buttons")){var label=(TextMesh)Read(button,"Caption");if(label!=null&&label.text==caption)return button;}
            throw new InvalidOperationException("Actual button absent: "+caption);
        }
        void Click(string caption)
        {
            var button=Button(caption);Need((bool)Read(button,"Enabled"),"Actual enabled button "+caption);
            var surface=(Transform)Read(button,"Surface");Vector3 origin=surface.position-surface.forward*.4f,direction=surface.forward;
            loop.UI.UpdateLoadoutPointer(1,origin,direction,false,false,true);
            loop.UI.UpdateLoadoutPointer(1,origin,direction,true,false,true);
            loop.UI.UpdateLoadoutPointer(1,origin,direction,false,false,true);
        }
        object HelpRegion(string text)
        {
            foreach(object region in (IEnumerable)Read(loop.UI,"settingHelpRegions"))if(((string)Read(region,"Copy")).IndexOf(text,StringComparison.OrdinalIgnoreCase)>=0)return region;
            throw new InvalidOperationException("Actual help region absent: "+text);
        }
        bool Hover(string needle,bool click=false)
        {
            object region=HelpRegion(needle);var space=(Transform)Read(region,"Space");var center=(Vector3)Read(region,"Center");Vector3 point=space.TransformPoint(center),origin=point-space.forward*.4f;
            loop.UI.UpdateLoadoutPointer(0,Vector3.zero,Vector3.forward,false,false,false);
            bool consumed=loop.UI.UpdateLoadoutPointer(1,origin,space.forward,false,false,true);
            if(click){consumed|=loop.UI.UpdateLoadoutPointer(1,origin,space.forward,true,false,true);consumed|=loop.UI.UpdateLoadoutPointer(1,origin,space.forward,false,false,true);}
            Need(loop.UI.CurrentSettingsHelp.IndexOf(needle,StringComparison.OrdinalIgnoreCase)>=0,"Actual displayed help for "+needle);
            result.settings.Add(loop.UI.CurrentSettingsHelp);return consumed;
        }
        IEnumerator SettingsHelp()
        {
            bool oldManual=loop.ManualSmoke,oldExternal=loop.ExternalProbePump,oldRig=loop.Rig.enabled;
            R23UiSettings saved=loop.UI.Settings;bool hadPreference=PlayerPrefs.HasKey(PreferenceKey);string preference=PlayerPrefs.GetString(PreferenceKey,"");int callbacks=0;
            Action<R23UiSettings> changed=delegate{callbacks++;};loop.UI.SettingsChanged+=changed;
            try{
                loop.ManualSmoke=true;loop.ExternalProbePump=true;loop.Rig.enabled=false;
                loop.ReturnToLobby();loop.UI.OpenSettings();Click("GENERAL");yield return null;
                Need(!Hover("music volume is not connected",true),"Disabled music help must not hit/consume a button");
                Need(callbacks==0&&R23UiSettingsCodec.SameValue(saved,loop.UI.Settings),"Disabled hover/click changes no preference or callback");
                loop.UI.UpdateLoadoutPointer(1,Vector3.zero,Vector3.forward,false,false,false);
                Need(loop.UI.CurrentSettingsHelp.IndexOf("music volume",StringComparison.OrdinalIgnoreCase)<0,"Invalid held-hand ray clears prior help");
                Need(Hover("Weapon, movement, menu"),"Active SFX slider remains an actual button target");
                // Drag at the existing value and move vertically off the row. This
                // exercises real help pinning without changing a user's volume.
                object row=null;foreach(object candidate in (IEnumerable)Read(loop.UI,"numericRows")){var text=(TextMesh)Read(candidate,"Text");if(text!=null&&text.text.StartsWith("SFX VOLUME",StringComparison.Ordinal))row=candidate;}
                Need(row!=null,"Actual SFX numeric row");var knob=(Transform)Read(row,"Knob");var parent=knob.parent;Vector3 at=knob.position+parent.right*(loop.UI.Settings.SfxVolume<.5f?.0005f:-.0005f);
                loop.UI.UpdateLoadoutPointer(1,at-parent.forward*.4f,parent.forward,false,false,true);
                loop.UI.UpdateLoadoutPointer(1,at-parent.forward*.4f,parent.forward,true,false,true);
                loop.UI.UpdateLoadoutPointer(1,at+parent.up*.16f-parent.forward*.4f,parent.forward,true,false,true);
                Need(loop.UI.CurrentSettingsHelp.IndexOf("Weapon, movement, menu",StringComparison.Ordinal)>=0,"Help remains pinned through actual slider drag");
                loop.UI.UpdateLoadoutPointer(1,at+parent.up*.16f-parent.forward*.4f,parent.forward,false,false,true);
                Need(callbacks==0&&R23UiSettingsCodec.SameValue(saved,loop.UI.Settings),"Same-value drag preserves settings");
                Capture(loop.Rig.Head,"unity-only-unmatched-settings-handles-help");
                loop.UI.OpenMatchRules();Hover("Free-for-all: respawn rules with no teams");Click("EQUIPMENT");
                Need(!Hover("Keep carried items and ammo",true),"Locked inventory-retention label explains without consuming input");
                Hover("full item catalog");Click("RESPAWN");Hover("Spawn/revive protection");Click("ZONE");Hover("Shield does not absorb");
                Need(loop.ActualMatchRules==null,"Explaining presets/rules never starts a match");
                Need(callbacks==0&&R23UiSettingsCodec.SameValue(saved,loop.UI.Settings),"Help-only interactions preserve actual settings");
                Capture(loop.Rig.Head,"unity-only-unmatched-zone-rule-help");
                result.settingsPassed=true;
            }finally{
                loop.UI.SettingsChanged-=changed;
                if(!R23UiSettingsCodec.SameValue(saved,loop.UI.Settings)){loop.UI.SetSettings(saved);loop.GetType().GetMethod("ApplySettings",Fields).Invoke(loop,new object[]{saved});}
                loop.UI.ShowReady();if(hadPreference)PlayerPrefs.SetString(PreferenceKey,preference);else PlayerPrefs.DeleteKey(PreferenceKey);PlayerPrefs.Save();
                loop.ManualSmoke=oldManual;loop.ExternalProbePump=oldExternal;loop.Rig.enabled=oldRig;
            }
        }
        void Capture(Camera camera,string label)
        {
            Need(camera!=null,"Actual production camera");string path=Path.Combine(imageDirectory,label+".png");Need(!File.Exists(path),"Never overwrite captures");
            var render=RenderTexture.GetTemporary(1200,800,24,RenderTextureFormat.ARGB32);var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;Texture2D pixels=null;
            try{camera.targetTexture=render;camera.Render();RenderTexture.active=render;pixels=new Texture2D(1200,800,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1200,800),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());result.images.Add(new ImageRecord{path=path,label=label,colorSpace=QualitySettings.activeColorSpace.ToString(),eye=camera.transform.position,forward=camera.transform.forward,up=camera.transform.up,fov=camera.fieldOfView,aspect=camera.aspect,width=1200,height=800,samples=render.antiAliasing});}
            finally{camera.targetTexture=oldTarget;RenderTexture.active=oldActive;if(pixels!=null)UnityEngine.Object.Destroy(pixels);RenderTexture.ReleaseTemporary(render);}
            var checkedShaders=new HashSet<Shader>();foreach(var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))if(renderer.enabled&&renderer.gameObject.activeInHierarchy)foreach(var material in renderer.sharedMaterials)if(material!=null&&checkedShaders.Add(material.shader))foreach(var message in ShaderUtil.GetShaderMessages(material.shader))if(message.severity.ToString()=="Error")result.shaderErrors.Add(material.shader.name+": "+message.message);
            Need(result.shaderErrors.Count==0,"Actual rendered material shader errors: "+String.Join(" | ",result.shaderErrors));
        }
        void Finish(){result.success=result.nativeExecuted&&result.menuPassed&&(!result.settingsRequested||result.settingsPassed)&&result.error.Length==0&&result.shaderErrors.Count==0;R23SmokeControl.Finish(result.success,JsonUtility.ToJson(result,true));}
    }
}
#endif

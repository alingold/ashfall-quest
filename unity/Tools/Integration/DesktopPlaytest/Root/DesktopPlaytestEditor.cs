#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.SceneManagement;
using Ashfall.Integration.DesktopPlaytest;
[InitializeOnLoad]
public static class DesktopPlaytestEditor
{
    const string Key="ASHFALL_DESKTOP_NATIVE_19";
    [Serializable]public sealed class Results{public bool compiled,noActiveXR,arena,arenaReview,pawn,pawnReview,combat,combatOriginalExpected,combatReview,construction,hud,bootstrapReview,hardening,completed;public string errors="";}
    static Results result;
    static bool priorOptions;static EnterPlayModeOptions priorMode;
    static double started;
    static DesktopPlaytestEditor(){EditorApplication.update+=Tick;}
    static string Arg(string key){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,key);if(i<0||i+1>=args.Length)throw new ArgumentException(key);return args[i+1];}
    public static void Native()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        result=new Results{compiled=true};priorOptions=EditorSettings.enterPlayModeOptionsEnabled;priorMode=EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        started=EditorApplication.timeSinceStartup;SessionState.SetInt(Key,1);EditorApplication.isPlaying=true;
    }
    static void Tick()
    {
        int state=SessionState.GetInt(Key,0);if(state==0)return;
        if(state!=3&&EditorApplication.timeSinceStartup-started>120){result.errors+="Native gate timeout\n";SessionState.SetInt(Key,3);EditorApplication.isPlaying=false;}
        if(state==1&&EditorApplication.isPlaying)
        {
            SessionState.SetInt(Key,2);result.noActiveXR=true;
            var ds=new System.Collections.Generic.List<UnityEngine.XR.XRDisplaySubsystem>();SubsystemManager.GetSubsystems(ds);foreach(var d in ds)if(d.running)result.noActiveXR=false;
            DesktopNativeRunner.Finished=Done;new GameObject("root desktop native gates").AddComponent<DesktopNativeRunner>();
        }
        if(state==3&&!EditorApplication.isPlaying)
        {
            SessionState.SetInt(Key,0);EditorSettings.enterPlayModeOptionsEnabled=priorOptions;EditorSettings.enterPlayModeOptions=priorMode;
            File.WriteAllText(Arg("-ashfallNativeResult"),JsonUtility.ToJson(result,true));
            EditorApplication.Exit(result.completed&&result.noActiveXR&&result.arena&&result.arenaReview&&result.pawn&&result.pawnReview&&result.combat&&result.combatOriginalExpected&&result.combatReview&&result.construction&&result.hud&&result.bootstrapReview&&result.hardening&&result.errors.Length==0?0:2);
        }
    }
    static void Done(string name,Exception error)
    {
        if(name=="combatOriginalExpected"){
            result.combatOriginalExpected=error!=null&&error.Message=="CombatReview failures: removed target does not remain an impossible objective";
            if(!result.combatOriginalExpected)result.errors+="Original combat fixture did not reproduce its target-clear expectation mismatch: "+error+"\n";
            return;
        }
        bool okay=error==null;if(error!=null){result.errors+=name+": "+error+"\n";Debug.LogException(error);}
        if(name=="arena")result.arena=okay;if(name=="pawn")result.pawn=okay;if(name=="combat")result.combat=okay;if(name=="construction")result.construction=okay;if(name=="hud")result.hud=okay;
        if(name=="pawnReview")result.pawnReview=okay;
        if(name=="arenaReview")result.arenaReview=okay;if(name=="combatReview")result.combatReview=okay;if(name=="bootstrapReview")result.bootstrapReview=okay;if(name=="hardening")result.hardening=okay;
        if(name=="done"){result.completed=error==null;SessionState.SetInt(Key,3);EditorApplication.isPlaying=false;}
    }
    public static void CreateSceneAndBuild()
    {
        PlayerSettings.companyName="AshfallPlaytest";PlayerSettings.productName="Ashfall PC Playtest";
        PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=800;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=true;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=new GameObject("ASHFALL synthetic desktop playtest");var bootstrap=root.AddComponent<DesktopPlaytestBootstrap>();
        Directory.CreateDirectory("Assets/DesktopShaderRetention");var materials=new System.Collections.Generic.List<Material>();
        foreach(var shaderName in new[]{"Standard","Universal Render Pipeline/Lit"})
        {
            var shader=Shader.Find(shaderName);if(!shader)continue;var m=new Material(shader);
            string path="Assets/DesktopShaderRetention/"+shaderName.Replace('/','_')+".mat";
            if(AssetDatabase.LoadAssetAtPath<Material>(path)!=null)AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m,path);materials.Add(m);
        }
        bootstrap.ShaderRetention=materials.ToArray();
        RenderSettings.ambientLight=new Color(.55f,.6f,.7f);RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
        EditorSceneManager.SaveScene(scene,"Assets/DesktopPlaytest.unity");AssetDatabase.SaveAssets();
        Ashfall.Tools.DesktopClosure.DesktopBuildRunner.Build();
    }
}
#endif

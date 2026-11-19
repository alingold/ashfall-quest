using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace Ashfall.Editor.Playable
{
    // Linux desktop player for two-peer network rigs (R25NetBot). XR is always off.
    // Invoke with -buildTarget Linux64 -executeMethod Ashfall.Editor.Playable.R26LinuxBuild.BuildLinux
    // -ashfallLinuxBuild <new absolute .x86_64 path> [-ashfallDevelopment false] [-ashfallDeepProfile true].
    public static class R26LinuxBuild
    {
        public static void BuildLinux()
        {
            try
            {
                if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.StandaloneLinux64)throw new Exception("Start Editor with -buildTarget Linux64");
                R23PlayableBuild.Prepare();string output=R23PlayableBuild.Argument("-ashfallLinuxBuild");
                if(String.IsNullOrEmpty(output)||!Path.IsPathRooted(output)||!output.EndsWith(".x86_64",StringComparison.Ordinal))throw new ArgumentException("New absolute -ashfallLinuxBuild .x86_64 required");
                if(Directory.Exists(Path.GetDirectoryName(output)))throw new Exception("Preserve previous build: choose a new output directory");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                bool development=R23PlayableBuild.Argument("-ashfallDevelopment","true")!="false";
                // Optional method-level markers for rig diagnosis: launch the player with -deepprofiling.
                bool deep=development&&R23PlayableBuild.Argument("-ashfallDeepProfile","false")=="true";
                var xr=R23PlayableBuild.Xr();
                bool previous=xr!=null&&xr.InitManagerOnStart;if(xr!=null){xr.InitManagerOnStart=false;EditorUtility.SetDirty(xr);AssetDatabase.SaveAssets();}
                // The rig player is Vulkan-only: R23AlpineTerrain and R23AppearanceSpecular do not compile for GLCore,
                // which made a usable build report two errors. The project graphics settings are restored afterwards.
                bool defaultApis=PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneLinux64);var apis=PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneLinux64);
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneLinux64,false);PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneLinux64,new[]{UnityEngine.Rendering.GraphicsDeviceType.Vulkan});
                BuildReport report;
                try{report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{R23PlayableBuild.ScenePath},locationPathName=output,target=BuildTarget.StandaloneLinux64,options=development?(deep?BuildOptions.Development|BuildOptions.EnableDeepProfilingSupport:BuildOptions.Development):BuildOptions.None});}
                finally{PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneLinux64,apis);PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneLinux64,defaultApis);if(xr!=null){xr.InitManagerOnStart=previous;EditorUtility.SetDirty(xr);}AssetDatabase.SaveAssets();}
                if(report.summary.result!=BuildResult.Succeeded||report.summary.totalErrors!=0)throw new Exception("Linux build failed: "+report.summary.result+" errors="+report.summary.totalErrors);
                Debug.Log("R26 Linux player built: "+output);EditorApplication.Exit(0);
            }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(2);}
        }
    }
}

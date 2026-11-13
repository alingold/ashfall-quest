#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
namespace Ashfall.Tools.DesktopClosure
{
    // Staged proposal only. Root copies to its sole project's Editor folder and
    // supplies its already-authored synthetic scene. Never executes on import.
    public static class DesktopBuildRunner
    {
        static string Argument(string key)
        {
            string[] args=Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length-1;i++)if(args[i]==key)return args[i+1];
            throw new ArgumentException("missing "+key);
        }
        public static void Build()
        {
            string scene=Argument("-ashfallDesktopScene");
            string output=Argument("-ashfallDesktopOutput");
            if(!scene.StartsWith("Assets/",StringComparison.Ordinal) || !scene.EndsWith(".unity",StringComparison.Ordinal) || !File.Exists(scene))
                throw new ArgumentException("root scene must exist under Assets");
            if(!Path.IsPathRooted(output) || !output.EndsWith(".exe",StringComparison.OrdinalIgnoreCase) || Path.GetPathRoot(output).Replace('/','\\').ToUpperInvariant()!="C:\\")
                throw new ArgumentException("new absolute C: exe output required");
            if(Directory.Exists(Path.GetDirectoryName(output)))
                throw new InvalidOperationException("preserve existing build: output parent must be new");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var options=new BuildPlayerOptions{scenes=new[]{scene},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development};
            BuildReport report=BuildPipeline.BuildPlayer(options);
            if(report.summary.result!=BuildResult.Succeeded || report.summary.totalErrors!=0 || !File.Exists(output))
                throw new InvalidOperationException("Windows build failed: "+report.summary.result+" errors="+report.summary.totalErrors);
            UnityEngine.Debug.Log("DesktopClosure Windows build PASS; standalone launch/playability remain unverified: "+output);
        }
    }
}
#endif

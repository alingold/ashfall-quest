using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Ashfall.Editor.Playable
{
    // Only the raw map/weapon mesh streams are read in full by UnityWebRequest.
    // Retain all other generated no-compress extensions and every payload byte.
    public static class R24QuestStreamingCompressionPolicy
    {
        public static string Rewrite(string properties)
        {
            if(properties==null)throw new ArgumentNullException("properties");
            var pattern=new Regex(@"(?m)^unityStreamingAssets=([^\r\n]*)");
            var matches=pattern.Matches(properties);
            if(matches.Count!=1)throw new InvalidDataException("Expected one generated unityStreamingAssets property");
            var keep=new List<string>();bool bytes=false,mesh=false;
            foreach(string value in matches[0].Groups[1].Value.Split(','))
            {
                string extension=value.Trim();
                if(extension==".bytes"){bytes=true;continue;}
                if(extension==".meshbin"){mesh=true;continue;}
                if(extension.Length==0)throw new InvalidDataException("Empty no-compress extension disables all APK compression");
                keep.Add(extension);
            }
            if(bytes!=mesh)throw new InvalidDataException("Partially rewritten raw geometry extensions; inspect before packaging");
            // Incremental Unity builds may retain our prior generated property.
            // Both absent is already configured, not a new packaging failure.
            if(!bytes)return properties;
            return properties.Substring(0,matches[0].Index)+"unityStreamingAssets="+String.Join(", ",keep.ToArray())+properties.Substring(matches[0].Index+matches[0].Length);
        }
    }
}

#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
namespace Ashfall.Editor.Playable
{
    // Scoped to our explicit CLI Quest build; native Editor smoke never enters it.
    public sealed class R24QuestStreamingCompression:UnityEditor.Android.IPostGenerateGradleAndroidProject
    {
        public int callbackOrder{get{return 1000;}}
        static string Argument(string name){var args=Environment.GetCommandLineArgs();for(int i=0;i+1<args.Length;i++)if(args[i]==name)return args[i+1];return null;}
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string output=Argument("-ashfallQuestBuild");
            if(String.IsNullOrEmpty(output)||Argument("-ashfallCompressGeometry")!="true")return;
            if(UnityEditor.PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android).StartsWith("org.alingold.ashfallunityquest",StringComparison.Ordinal)==false)throw new InvalidDataException("Wrong package for Unity Quest geometry compression");
            string library=Path.GetFullPath(path),root=Path.GetDirectoryName(library);
            if(Path.GetFileName(library)!="unityLibrary")throw new InvalidDataException("Unexpected generated Gradle module: "+library);
            string gradle=File.ReadAllText(Path.Combine(library,"build.gradle"));
            if(!Regex.IsMatch(gradle,@"noCompress\s*=.*unityStreamingAssets\.tokenize"))throw new InvalidDataException("Generated noCompress binding changed; inspect before packaging");
            // AGP's local lint AAR contains all raw source assets and now exceeds
            // ZIP32's4GiB limit. Enable ZIP64 only for generated Gradle Zip tasks;
            // Android's APK packager still creates the normal compressed APK.
            const string zip64="\n// ASHFALL: source-preserving large intermediate archives\n"+
                "tasks.withType(org.gradle.api.tasks.bundling.Zip).configureEach { zip64 = true }\n";
            if(!gradle.Contains("// ASHFALL: source-preserving large intermediate archives"))
                File.WriteAllText(Path.Combine(library,"build.gradle"),gradle+zip64,new System.Text.UTF8Encoding(false));
            // Packaging-only optional lint produces a full raw-assets AAR. The
            // explicit playtest flag skips only lint tasks in both generated
            // modules; compile, package, sign and payload validation stay enabled.
            if(Argument("-ashfallSkipReleaseLint")=="true"){
                const string marker="// ASHFALL: bounded playtest skips optional lint archives";
                const string skip="\n"+marker+"\nrootProject.allprojects { tasks.configureEach { if (name.toLowerCase(java.util.Locale.ROOT).contains('lint')) { enabled = false } } }\ngradle.taskGraph.whenReady { graph -> graph.allTasks.findAll { it.name.toLowerCase(java.util.Locale.ROOT).contains('lint') }.each { t -> if (t.enabled) throw new GradleException('ASHFALL optional lint still enabled: ' + t.path); println('ASHFALL_OPTIONAL_LINT_DISABLED ' + t.path) } }\n";
                foreach(string module in new[]{"unityLibrary","launcher"}){string file=Path.Combine(root,module,"build.gradle"),text=File.ReadAllText(file);int old=text.IndexOf("\n"+marker,StringComparison.Ordinal);if(old>=0){string tail=text.Substring(old);foreach(string line in tail.Split('\n')){string owned=line.Trim();if(owned.Length>0&&owned!=marker&&!owned.StartsWith("tasks.configureEach { if (name.toLowerCase(java.util.Locale.ROOT).contains('lint'))")&&!owned.StartsWith("rootProject.allprojects { tasks.configureEach")&&!owned.StartsWith("gradle.taskGraph.whenReady { graph ->"))throw new InvalidDataException("Unexpected content after owned lint marker");}text=text.Substring(0,old);}File.WriteAllText(file,text+skip,new System.Text.UTF8Encoding(false));}
            }
            string properties=Path.Combine(root,"gradle.properties");
            File.WriteAllText(properties,R24QuestStreamingCompressionPolicy.Rewrite(File.ReadAllText(properties)),new System.Text.UTF8Encoding(false));
            UnityEngine.Debug.Log("Unity Quest: APK compression enabled for raw .bytes/.meshbin streams; source paths and uncompressed bytes unchanged.");
        }
    }
}
#endif

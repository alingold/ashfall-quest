#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Ashfall.Integration.Playable;
namespace Ashfall.Editor.Playable
{
    [InitializeOnLoad]
    public static class R25NetBotEditor
    {
        const string Key="Ashfall.NetBot.Editor";
        static R25NetBotEditor(){EditorApplication.update+=Pump;}
        public static void Run()
        {
            if(!R25NetBot.Requested)throw new System.ArgumentException("-ashfallNetBot required");
            R23PlayableBuild.Prepare();var xr=R23PlayableBuild.Xr();xr.InitManagerOnStart=false;
            SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
        }
        static void Pump()
        {
            if(!SessionState.GetBool(Key,false)||!Application.isPlaying||!R25NetBot.Finished)return;
            bool ok=R25NetBot.Passed;SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;
            R23PlayableBuild.Xr().InitManagerOnStart=true;EditorApplication.Exit(ok?0:2);
        }
    }
}
#endif

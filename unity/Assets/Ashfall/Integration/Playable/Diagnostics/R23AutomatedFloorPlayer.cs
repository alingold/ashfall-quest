#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
namespace Ashfall.Integration.Playable.Diagnostics
{
    // Explicit batch-only exported-player diagnostic. The dedicated build disables
    // XR automatic initialization; this entry never starts a loader or a headset.
    public sealed class R23AutomatedFloorPlayer:MonoBehaviour
    {
        string output;
        const string HeightKey="ashfall.r23.vertical-combat.height.v1";string savedHeight;bool hadHeight;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void BeginIfRequested()
        {
            var args=Environment.GetCommandLineArgs();string path=null;
            for(int i=0;i<args.Length-1;i++)if(args[i]=="-ashfallAutomaticFloorProbe")path=args[i+1];
            if(path==null)return;
            if(!Application.isBatchMode||!Path.IsPathRooted(path)||File.Exists(path)){Debug.LogError("New absolute batch-only floor output required");Application.Quit(3);return;}
            var displays=new List<XRDisplaySubsystem>();SubsystemManager.GetSubsystems(displays);bool running=false;foreach(var display in displays)running|=display.running;
            if(running||(XRGeneralSettings.Instance!=null&&XRGeneralSettings.Instance.InitManagerOnStart)){Debug.LogError("Automated floor player requires XR initialization disabled in its build");Application.Quit(3);return;}
            Debug.Log("R23 AUTOMATED FLOOR: XR initialization disabled; no running XR display");
            var runner=new GameObject("Explicit XR-disabled floor probe").AddComponent<R23AutomatedFloorPlayer>();runner.output=path;runner.hadHeight=PlayerPrefs.HasKey(HeightKey);runner.savedHeight=PlayerPrefs.GetString(HeightKey,"");runner.StartCoroutine(new R23FloorProbe().Start(runner.Finish));
        }
        void Finish(bool passed,string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,json);
            if(hadHeight)PlayerPrefs.SetString(HeightKey,savedHeight);else PlayerPrefs.DeleteKey(HeightKey);PlayerPrefs.Save();
            Debug.Log("R23 AUTOMATED FLOOR complete passed="+passed+" output="+output);Application.Quit(passed?0:2);
        }
    }
}
#endif

#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace Ashfall.Integration.Playable
{
    // Recover only a failed initial loader. Never tear down an initialized
    // session because a user removes the headset, opens Dash or loses focus.
    public sealed class R23XrStartupRecovery:MonoBehaviour
    {
        readonly List<XRDisplaySubsystem> displays=new List<XRDisplaySubsystem>();
        public int Attempts {get;private set;}
        public bool DisplayStarted {get;private set;}
        public string LastFailure {get;private set;}
        bool waiting;
        bool Running()
        {
            displays.Clear();SubsystemManager.GetSubsystems(displays);
            foreach(var display in displays)if(display.running)return true;
            return false;
        }
        IEnumerator Start()
        {
            // XRGeneralSettings owns the initial Awake/Start attempt. Its public
            // manual loader API requires graphics initialization to finish first.
            yield return null;yield return null;
            if(Application.isEditor||Application.isBatchMode)yield break;
            var general=XRGeneralSettings.Instance;
            if(general==null||!general.InitManagerOnStart||general.Manager==null)yield break;
            var manager=general.Manager;float delay=3;
            while(isActiveAndEnabled)
            {
                if(Running()){DisplayStarted=true;waiting=false;yield break;}
                // A live initialized loader may simply be waiting for its session
                // to become visible. It owns that lifecycle; do not restart it.
                if(manager.activeLoader!=null){waiting=true;yield return new WaitForSecondsRealtime(1);continue;}
                waiting=true;yield return new WaitForSecondsRealtime(delay);
                if(Running()){DisplayStarted=true;waiting=false;yield break;}
                if(manager.activeLoader!=null)continue;
                try
                {
                    Attempts++;Debug.Log("R23 XR startup: retry "+Attempts+" after headset unavailable");
                    manager.InitializeLoaderSync();
                    if(manager.activeLoader!=null&&manager.isInitializationComplete)manager.StartSubsystems();
                    LastFailure=manager.activeLoader==null?"Headset unavailable through the active OpenXR runtime":null;
                }
                catch(Exception error){LastFailure=error.Message;Debug.LogWarning("R23 XR startup retry: "+error.Message);}
                delay=Mathf.Min(10,delay*2);
            }
        }
        void OnGUI()
        {
            if(!waiting||DisplayStarted||Running())return;
            float width=Mathf.Min(Screen.width-40,640);
            GUI.Box(new Rect((Screen.width-width)*.5f,24,width,76),"Connect Quest Link in the headset.\nAshfall will retry VR startup automatically.");
        }
    }
}
#endif

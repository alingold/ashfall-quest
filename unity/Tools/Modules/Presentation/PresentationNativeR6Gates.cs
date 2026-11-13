#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Modules.Presentation;
using Ashfall.Modules.Presentation.Engine;
using Ashfall.Modules.Player;
using Ashfall.Integration.PersonalSettings;
using UnityEngine;
namespace Ashfall.Tools.Presentation
{
    public static class PresentationNativeR6Gates
    {
        // Coordinator calls inside its isolated serialized Editor fixture. Destination
        // must be a new dedicated settings path; no shared scene/bootstrap writes.
        public static void Run(string destination)
        {
            var host=new PersonalSettingsAdapter(destination,SettingsSource.Quest,new Actions());
            var input=new PlayerInput();var binding=new PlayerSettingsBinding(input);
            var runtime=new PersonalSettingsRuntimeHost(host,binding);
            var menu=new PresentationModel(runtime);var hud=new HudModel();
            var go=new GameObject("PresentationR6Fixture");
            try {
                var adapter=go.AddComponent<PresentationAdapter>();adapter.DrawDesktop=false;adapter.Bind(menu,hud);
                adapter.Navigate(0,0,false,false,true);
                if(!menu.IsOpen||!binding.Read(1,.02f,new DeviceSample{Focused=true}).Paused)throw new Exception("native menu ownership");
                adapter.Navigate(0,0,true,false,false);
                if(menu.IsOpen||runtime.RuntimePending)throw new Exception("native resume");
                var motor=binding.CreateLocomotion();
                if(!(motor.Settings is TypedPlayerSettingsPort))throw new Exception("native immutable motor port");
            } finally {UnityEngine.Object.DestroyImmediate(go);}
        }
        private sealed class Actions:ISettingsUiActions
        {public void SetPaused(bool p){}public bool CanRequest(string a){return false;}public void Request(string a,long o){throw new InvalidOperationException();}}
    }
}
#endif

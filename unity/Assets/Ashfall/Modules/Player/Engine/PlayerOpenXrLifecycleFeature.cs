#if UNITY_6000_0_OR_NEWER
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR;
namespace Ashfall.Modules.Player
{
    // Coordinator must explicitly register/enable this feature in its owned settings.
    // OpenXR XrSessionState FOCUSED = 5 (xr.xml); no loader/settings changes here.
    public sealed class PlayerOpenXrLifecycleFeature:OpenXRFeature,IXrPlayerLifecycle
    {
        bool focused;
        public event System.Action Invalidated;
        public bool SessionFocused {get {return focused;} }
        public bool Present {get {bool present;return InputDevices.GetDeviceAtXRNode(XRNode.Head).TryGetFeatureValue(CommonUsages.userPresence,out present)&&present;} }
        void Clear() {focused=false;if(Invalidated!=null)Invalidated();}
        protected override void OnSessionStateChange(int oldState,int newState) {focused=newState==5;if(!focused)Clear();}
        protected override void OnSessionEnd(ulong session) {Clear();}
        protected override void OnInstanceDestroy(ulong instance) {Clear();}
    }
}
#endif

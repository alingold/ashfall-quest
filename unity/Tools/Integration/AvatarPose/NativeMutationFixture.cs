#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using Ashfall.Modules.Player;
using Ashfall.Integration.AvatarPose;
using UnityEngine;
public static class NativeAvatarPoseMutationFixture
{
    // Coordinator supplies a real configured driver, 16-part pilot/real gloves and
    // native provider. Synthetic transform inputs only; no XR/device/quality proof.
    // Run under its serialized lane, yielding between Pump calls. Never called here.
    public static IEnumerator Run(UnityPlayerDriver driver,NativePoseBinding.Provider provider)
    {
        if(driver.UseXr)throw new InvalidOperationException("Synthetic fixture must not consume real XR devices");
        var root=driver.transform;var head=driver.Head;var left=driver.LeftHand;var right=driver.RightHand;
        Vector3 initial=root.position,h=head.localPosition,l=left.localPosition,r=right.localPosition;Quaternion rotation=root.rotation;
        try {
            root.position+=new Vector3(3,0,2);root.rotation=Quaternion.Euler(0,75,0);
            yield return null;
            var frozenHead=head.localToWorldMatrix;var frozenLeft=left.localToWorldMatrix;
            if(!provider.Pump(.016f,true))throw new Exception("post-rootmove/turn full frame unavailable");
            if(head.localToWorldMatrix!=frozenHead||left.localToWorldMatrix!=frozenLeft)throw new Exception("presentation wrote tracked transforms");
            head.localPosition=h-Vector3.up*.5f;left.localPosition=l+Vector3.up*.2f;
            yield return null;if(!provider.Pump(.016f,true))throw new Exception("crouch/hand update failed");
            driver.Paused=true;yield return null;if(!provider.Pump(.016f,true))throw new Exception("pause incorrectly treated as tracking loss");
            driver.enabled=false;yield return null;if(provider.Pump(.016f,true))throw new Exception("disabled producer accepted");
            driver.enabled=true;driver.Paused=false;yield return null;
            // Re-enable requires central fresh-generation binding, not old provider reuse.
        } finally {provider.Dispose();root.position=initial;root.rotation=rotation;head.localPosition=h;left.localPosition=l;right.localPosition=r;}
    }
}
#endif

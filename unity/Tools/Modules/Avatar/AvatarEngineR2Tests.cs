#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
using Ashfall.Modules.Avatar;
// Coordinator engine lane entry point; not executed by neutral csc.
public static class AvatarEngineR2Tests
{
    static void MustReject(GameObject root)
    {
        try { AvatarView.ValidateInertHierarchy(root); }
        catch(InvalidOperationException) { return; }
        throw new Exception("Unsafe hierarchy accepted");
    }
    public static void Run()
    {
        foreach(var type in new[]{typeof(Animation),typeof(AudioSource),typeof(Camera),typeof(ArticulationBody),typeof(Animator),typeof(BoxCollider)}) {
            var root=new GameObject("malicious"); root.SetActive(false);
            try { root.AddComponent(type); MustReject(root); }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        var rig=new GameObject("rig"); rig.SetActive(false);
        var outside=new GameObject("external");
        try {
            var bone=new GameObject("bone"); bone.transform.SetParent(rig.transform,false);
            var renderer=rig.AddComponent<SkinnedMeshRenderer>();
            renderer.rootBone=bone.transform; renderer.bones=new[]{bone.transform};
            AvatarView.ValidateInertHierarchy(rig);
            renderer.rootBone=outside.transform; MustReject(rig);
            renderer.rootBone=bone.transform; renderer.bones=new[]{outside.transform}; MustReject(rig);
            renderer.bones=new Transform[]{null}; MustReject(rig);
        } finally { UnityEngine.Object.DestroyImmediate(rig); UnityEngine.Object.DestroyImmediate(outside); }
    }
}
#endif

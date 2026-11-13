#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
using Ashfall.Modules.Avatar;
// Compile/run ONLY in the coordinator's real frozen editor lane; no Unity stubs.
public static class AvatarEngineR3Tests
{
    static void Need(bool b,string m){if(!b)throw new Exception(m);}
    static void Reject(GameObject root){try{AvatarView.ValidateInertHierarchy(root);}catch(InvalidOperationException){return;}throw new Exception("Unsafe hierarchy accepted");}
    public static void Run(PreparedPart left,PreparedPart right) {
        AvatarEngineR2Tests.Run();
        foreach(var part in new[]{left,right}) {
            var parent=new GameObject("inactive-test");parent.SetActive(false);var resources=new List<UnityEngine.Object>();var outside=new GameObject("external");
            try {
                var data=VerifiedSkinReader.Read(part);SkinBinding binding;
                var root=VerifiedSkinFactory.Create(part,data,parent.transform,resources,out binding);
                Need(!root.activeInHierarchy,"Premature hierarchy activation");AvatarView.ValidateInertHierarchy(root);
                var renderer=root.GetComponentInChildren<SkinnedMeshRenderer>(true);
                Need(renderer.bones.Length==26&&renderer.sharedMesh.bindposes.Length==26,"Source joint mapping lost");
                var baked=new Mesh();resources.Add(baked);renderer.BakeMesh(baked);
                var expected=data.Deform(data.CopyLocalMatrices());var actual=baked.vertices;
                Need(actual.Length*3==expected.Length,"Baked vertex count");for(int i=0;i<actual.Length;i++)Need((actual[i]-new Vector3(expected[i*3],expected[i*3+1],expected[i*3+2])).magnitude<0.0001f,"Real Unity rest skinning differs");
                var original=renderer.bones;renderer.bones=new[]{outside.transform};Reject(root);renderer.bones=new Transform[]{null};Reject(root);renderer.bones=original;
                var originalRoot=renderer.rootBone;renderer.rootBone=outside.transform;Reject(root);renderer.rootBone=null;Reject(root);renderer.rootBone=originalRoot;
                var child=new GameObject("inactive-malicious");child.transform.SetParent(root.transform,false);child.SetActive(false);child.AddComponent<Camera>();Reject(root);
            } finally {UnityEngine.Object.DestroyImmediate(parent);UnityEngine.Object.DestroyImmediate(outside);foreach(var resource in resources)UnityEngine.Object.DestroyImmediate(resource);}
        }
    }
}
#endif

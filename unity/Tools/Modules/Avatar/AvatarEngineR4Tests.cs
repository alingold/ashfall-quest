#if UNITY_6000_0_OR_NEWER
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Ashfall.Modules.Avatar;
// Coordinator only. Include AvatarR4Fixtures.cs and earlier engine harnesses in the same
// isolated Editor test assembly. Set cwd to the frozen AvatarR4 packet. Does not build or run devices.
public static class AvatarEngineR4Tests
{
 static void Need(bool b,string m){if(!b)throw new Exception(m);}
 static int Live<T>() where T:UnityEngine.Object{return Resources.FindObjectsOfTypeAll<T>().Length;}
 static Dictionary<string,Matrix4x4> Frame(){var f=new Dictionary<string,Matrix4x4>();foreach(string label in AvatarAssembler.RequiredLabels())f.Add(label,Matrix4x4.identity);return f;}
 public static void Run(){
 AvatarEngineR2Tests.Run();var a=AvatarR4Fixtures.Assembly();var receipts=AvatarR4Fixtures.Receipts(a);
 int meshes=Live<Mesh>(),materials=Live<Material>(),textures=Live<Texture2D>();var host=new GameObject("AvatarR4-owned-test");
 try{
 var view=host.AddComponent<AvatarView>();view.Assemble(a,true,receipts);Need(host.transform.childCount==1&&!host.transform.GetChild(0).gameObject.activeSelf,"Incomplete presentation visible");
 Need(host.GetComponentsInChildren<Renderer>(true).Length==16,"Actual16part assembly");
 foreach(var part in new[]{a.CopyParts()[14],a.CopyParts()[15]}){
 var skin=VerifiedSkinReader.Read(part);var pose=AvatarR4Fixtures.Grip(skin,part.Label=="LeftHand"?"left":"right").Update(0,0,"held","sword","sword",1);
 view.PresentHandPose(part.Label,pose);var root=host.transform.GetChild(0).Find(part.Label);var renderer=root.GetComponentInChildren<SkinnedMeshRenderer>(true);var bake=new Mesh();
 try{renderer.BakeMesh(bake);var expected=skin.Deform(pose.CopyLocalMatrices());var actual=bake.vertices;Need(actual.Length*3==expected.Length,"Posed bake count");for(int i=0;i<actual.Length;i++)Need((actual[i]-new Vector3(expected[i*3],expected[i*3+1],expected[i*3+2])).magnitude<.0001f,"Authored nonidentity BakeMesh discrepancy");Need(renderer.sharedMaterial.GetTexture("_BaseMap")!=null&&renderer.sharedMaterial.GetTexture("_MetallicGlossMap")!=null&&renderer.sharedMaterial.GetTexture("_BumpMap")!=null,"Missing authored material");}finally{UnityEngine.Object.DestroyImmediate(bake);}
 var outside=new GameObject("outside");try{var bones=renderer.bones;renderer.bones=new[]{outside.transform};Reject(root.gameObject);renderer.bones=new Transform[]{null};Reject(root.gameObject);renderer.bones=bones;var old=renderer.rootBone;renderer.rootBone=outside.transform;Reject(root.gameObject);renderer.rootBone=null;Reject(root.gameObject);renderer.rootBone=old;}finally{UnityEngine.Object.DestroyImmediate(outside);}
 }
 view.Present(Frame(),true,false);Need(host.transform.GetChild(0).gameObject.activeSelf,"Valid full frame hidden");view.Present(Frame(),false,false);Need(!host.transform.GetChild(0).gameObject.activeSelf,"Tracking loss visible");var partial=Frame();partial.Remove("Jacket");view.Present(partial,true,false);Need(!host.transform.GetChild(0).gameObject.activeSelf,"Partial frame visible");
 int oldMeshes=Live<Mesh>(),oldMaterials=Live<Material>(),oldTextures=Live<Texture2D>();var oldRoot=host.transform.GetChild(0);AvatarR4Fixtures.Source source;var invalid=AvatarR4Fixtures.Manifest(out source);invalid.Parts[15].Node="MissingLateSkin";var bad=AvatarAssembler.Prepare(invalid,source,null,AvatarUse.UnreviewedPilot);bool rejected=false;try{view.Assemble(bad,true,AvatarR4Fixtures.Receipts(bad));}catch(AvatarValidationException){rejected=true;}Need(rejected,"Failedswap accepted");Need(host.transform.childCount==1&&host.transform.GetChild(0)==oldRoot,"Failedswap changed active object");Need(Live<Mesh>()==oldMeshes&&Live<Material>()==oldMaterials&&Live<Texture2D>()==oldTextures,"Failedswap leaked resources");
 view.Assemble(a,true,receipts);Need(host.transform.childCount==1&&Live<Mesh>()==oldMeshes&&Live<Material>()==oldMaterials&&Live<Texture2D>()==oldTextures,"Successfulswap leaked resources");
 var malicious=new GameObject("inactive-component");malicious.transform.SetParent(host.transform.GetChild(0),false);malicious.SetActive(false);malicious.AddComponent<Camera>();Reject(host.transform.GetChild(0).gameObject);UnityEngine.Object.DestroyImmediate(malicious);
 }finally{UnityEngine.Object.DestroyImmediate(host);}
 Need(Live<Mesh>()==meshes&&Live<Material>()==materials&&Live<Texture2D>()==textures,"Dispose leaked owned resources");Debug.Log("AvatarR4 Editor harness PASS:16part/posedBake/material/containment/failedswap/lifecycle; no visual or device approval");
 }
 static void Reject(GameObject root){try{AvatarView.ValidateInertHierarchy(root);}catch(InvalidOperationException){return;}throw new Exception("Containment accepted");}
}
#endif

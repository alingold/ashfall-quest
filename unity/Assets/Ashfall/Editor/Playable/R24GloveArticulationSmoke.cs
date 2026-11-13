#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.HandsWeapons;
using UnityEngine;
using UnityEditor;
namespace Ashfall.Editor.Playable
{
    public static class R24GloveArticulationSmoke
    {
        [Serializable]sealed class Case{public string name="",profile="";public int hand=0,wristNode=0;public bool sword=false,holding=false;public float grip=0,trigger=0;public float[] positions=null,normals=null,wristWorld=null;}
        [Serializable]sealed class Oracle{public Case[] cases=null;}
        [Serializable]sealed class Result{public bool success=false,nativeExecuted=false,headsetVerified=false,matchedGodotCamera=false,cleanup=false;public string error="",oracleSha256="";public int cases=0,vertices=0;public float maxPositionError=0,maxNormalError=0;public List<string> images=new List<string>();}
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        static object Read(object o,string field){return o.GetType().GetField(field,Flags).GetValue(o);}
        static void Need(bool value,string why){if(!value)throw new InvalidOperationException(why);}
        static void Pose(object visuals,Case c){visuals.GetType().GetMethod("PoseHand",Flags).Invoke(visuals,new object[]{c.hand,c.profile,c.sword,c.holding,c.grip,c.trigger,.025f});}
        static bool Finite(float v){return !float.IsNaN(v)&&!float.IsInfinity(v);}
        public static IEnumerator Check(R23PlayableLoop loop,Action<string> note)
        {
            var receipt=new Result{nativeExecuted=true};string directory=Path.GetDirectoryName(R23PlayableBuild.Argument("-ashfallVRSmoke"));
            var visuals=Read(loop.Gameplay,"visuals");var filters=new MeshFilter[2];var materials=new Material[2];var uv=new Vector2[2][];var indices=new int[2][];var layers=new Dictionary<Transform,int>();
            var raw=new[]{loop.Rig.Left,loop.Rig.Right};var oldPositions=new[]{raw[0].position,raw[1].position};var oldRotations=new[]{raw[0].rotation,raw[1].rotation};Camera camera=null;bool oldPump=loop.Gameplay.ExternalInputPump;
            try{
                Need(!UnityEngine.XR.XRSettings.isDeviceActive,"XR disabled required");
                string path="D:/ashfall-r6-staging/hand-articulation-next/oracles.json";byte[] bytes=File.ReadAllBytes(path);using(var hash=SHA256.Create())receipt.oracleSha256=BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
                var oracle=JsonUtility.FromJson<Oracle>(System.Text.Encoding.UTF8.GetString(bytes));Need(oracle.cases.Length==18,"18 exact frozen-source pose oracles");
                loop.Gameplay.ExternalInputPump=true;loop.Gameplay.HolsterCurrent();loop.Gameplay.TickInput(0,new R23HandsInputFrame{LeftTracked=true,RightTracked=true});
                for(int hand=0;hand<2;hand++){
                    var root=hand==0?loop.Gameplay.SourceLeftGloveRoot:loop.Gameplay.SourceRightGloveRoot;filters[hand]=root.GetComponentInChildren<MeshFilter>(true);Need(filters[hand]!=null&&filters[hand].sharedMesh.vertexCount==812,"Actual live source glove");
                    materials[hand]=filters[hand].GetComponent<Renderer>().sharedMaterial;uv[hand]=filters[hand].sharedMesh.uv;indices[hand]=filters[hand].sharedMesh.triangles;
                    foreach(var message in ShaderUtil.GetShaderMessages(materials[hand].shader))Need(message.severity.ToString()!="Error",message.message);
                    foreach(var renderer in root.GetComponentsInChildren<Renderer>(true)){layers[renderer.transform]=renderer.gameObject.layer;renderer.gameObject.layer=30;}
                }
                foreach(var c in oracle.cases){
                    // Isolated source art input, not fabricated inventory/support ownership.
                    // The previous --support-grip run covers actual owner contact/release.
                    for(int frame=0;frame<48;frame++)Pose(visuals,c);
                    var hands=(IDictionary)Read(visuals,"articulatedHands");var boneWorld=(Matrix4x4[])Read(hands[c.hand==0?"left":"right"],"world");var wrist=boneWorld[c.wristNode];for(int row=0;row<4;row++)for(int col=0;col<4;col++)Need(Mathf.Abs(wrist[row,col]-c.wristWorld[row*4+col])<.000002f,"Source wrist bone unchanged by finger animation");
                    for(int hand=0;hand<2;hand++)Need(raw[hand].position==oldPositions[hand]&&Quaternion.Angle(raw[hand].rotation,oldRotations[hand])<.001f,"Finger art leaves raw tracked controllers unchanged");
                    var mesh=filters[c.hand].sharedMesh;var actual=mesh.vertices;var normal=mesh.normals;var tangents=mesh.tangents;Need(c.positions.Length==actual.Length*3,"Oracle vertex order/count");float maxP=0,maxN=0;
                    for(int i=0;i<actual.Length;i++){
                        var expected=new Vector3(c.positions[i*3],c.positions[i*3+1],c.positions[i*3+2]);var expectedNormal=new Vector3(c.normals[i*3],c.normals[i*3+1],c.normals[i*3+2]);maxP=Mathf.Max(maxP,Vector3.Distance(actual[i],expected));maxN=Mathf.Max(maxN,Vector3.Distance(normal[i],expectedNormal));
                        Need(Finite(actual[i].x)&&Finite(actual[i].y)&&Finite(actual[i].z)&&Mathf.Abs(normal[i].magnitude-1)<.0001f,"Finite source positions/unit normals");Need(Finite(tangents[i].x)&&Finite(tangents[i].y)&&Finite(tangents[i].z)&&Mathf.Abs(new Vector3(tangents[i].x,tangents[i].y,tangents[i].z).magnitude-1)<.0001f&&Mathf.Abs(Vector3.Dot(normal[i],new Vector3(tangents[i].x,tangents[i].y,tangents[i].z)))<.0001f,"Deformed orthonormal tangent frame");
                    }
                    receipt.maxPositionError=Mathf.Max(receipt.maxPositionError,maxP);receipt.maxNormalError=Mathf.Max(receipt.maxNormalError,maxN);note(c.hand+"/"+c.name+" max position error="+maxP+" normal="+maxN);
                    Need(maxP<.0002f&&maxN<.005f,"Source skin pose within declared .2mm position/.005 normal-direction tolerance");
                    var currentUv=mesh.uv;var currentIndices=mesh.triangles;Need(mesh.vertexCount==812&&currentUv.Length==uv[c.hand].Length&&currentIndices.Length==indices[c.hand].Length,"Original mesh topology count");for(int i=0;i<currentUv.Length;i++)Need(currentUv[i]==uv[c.hand][i],"UV unchanged");for(int i=0;i<currentIndices.Length;i++)Need(currentIndices[i]==indices[c.hand][i],"Indices unchanged");Need(filters[c.hand].GetComponent<Renderer>().sharedMaterial==materials[c.hand],"Actual outfit material retained");receipt.cases++;receipt.vertices+=actual.Length;
                    yield return null;
                }
                camera=new GameObject("XR-off glove articulation comparison").AddComponent<Camera>();camera.enabled=false;camera.stereoTargetEye=StereoTargetEyeMask.None;camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.065f,.08f);camera.fieldOfView=36;camera.nearClipPlane=.01f;camera.farClipPlane=5;
                loop.Rig.Left.SetPositionAndRotation(new Vector3(-.115f,1.3f,.4f),Quaternion.Euler(-90,0,0));loop.Rig.Right.SetPositionAndRotation(new Vector3(.115f,1.3f,.4f),Quaternion.Euler(-90,0,0));loop.Gameplay.RefreshFeedbackPresentation();camera.transform.position=new Vector3(.39f,1.48f,.75f);camera.transform.LookAt(new Vector3(0,1.3f,.43f));
                foreach(string state in new[]{"freeOpen","rifle"}){
                    foreach(var c in oracle.cases)if(c.name==state)for(int i=0;i<48;i++)Pose(visuals,c);
                    var target=RenderTexture.GetTemporary(1200,800,24,RenderTextureFormat.ARGB32);RenderTexture old=RenderTexture.active;Texture2D image=null;
                    try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image=new Texture2D(1200,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1200,800),0,0);image.Apply();string file=Path.Combine(directory,"unity-only-gloves-"+state+".png");File.WriteAllBytes(file,image.EncodeToPNG());receipt.images.Add(file);note("UNMATCHED Unity-only source pose input capture "+file);}
                    finally{camera.targetTexture=null;RenderTexture.active=old;RenderTexture.ReleaseTemporary(target);if(image!=null)UnityEngine.Object.Destroy(image);}
                }
                receipt.success=true;
            }
            finally{
                foreach(var pair in layers)if(pair.Key!=null)pair.Key.gameObject.layer=pair.Value;
                for(int hand=0;hand<2;hand++){raw[hand].SetPositionAndRotation(oldPositions[hand],oldRotations[hand]);for(int i=0;i<48;i++)Pose(visuals,new Case{hand=hand});}
                if(camera!=null)UnityEngine.Object.Destroy(camera.gameObject);loop.Gameplay.ExternalInputPump=oldPump;receipt.cleanup=true;File.WriteAllText(Path.Combine(directory,"glove-articulation-details.json"),JsonUtility.ToJson(receipt,true));
            }
        }
    }
}
#endif

#if UNITY_6000_0_OR_NEWER
using System;using System.Collections;using System.Collections.Generic;using System.IO;using UnityEngine;using UnityEngine.Networking;using UnityEngine.Rendering;
namespace Ashfall.Integration.Playable.Appearance
{
    // Independent preview-owned static bake of the actual source Grip5 neutral
    // skeleton pose. No tracked hand/gameplay glove/bore/gesture ownership.
    internal sealed class R23MenuGlovePreview:IDisposable
    {
        readonly List<Mesh> meshes=new List<Mesh>();readonly List<GameObject> objects=new List<GameObject>();UnityWebRequest pending;bool disposed;
        internal static void SetPose(Transform hand,bool left,bool customKit)
        {
            if(!customKit){float side=left?-1:1;hand.localPosition=new Vector3(side*.26f,.94f,.04f);hand.localRotation=Quaternion.AngleAxis(-side*.2f*Mathf.Rad2Deg,Vector3.forward);return;}
            if(left){hand.localPosition=new Vector3(-0.25336682995f,0.91708036322f,0.016605241018f);hand.localRotation=Quaternion.LookRotation(new Vector3(0.042423765052f,-0.99898487402f,-0.015147463076f),new Vector3(-0.2332159078f,-0.024643954159f,0.97211265596f));}
            else{hand.localPosition=new Vector3(0.25336683014f,0.91708036295f,0.016605241573f);hand.localRotation=Quaternion.LookRotation(new Vector3(-0.042423762264f,-0.9989848743f,-0.015147452429f),new Vector3(0.23321590593f,-0.024643943072f,0.97211265669f));}
        }
        internal IEnumerator Load(bool left,Transform parent)
        {
            if(disposed||parent==null)throw new InvalidOperationException("Source menu glove owner unavailable");string file="preview-menu-"+(left?"left":"right")+".meshbin",path=Application.streamingAssetsPath+"/PlayableR23/Appearance/"+file;if(!path.Contains("://"))path=new Uri(path).AbsoluteUri;
            using(var request=UnityWebRequest.Get(path))
            {
                pending=request;request.timeout=20;try{yield return request.SendWebRequest();if(disposed)yield break;if(request.result!=UnityWebRequest.Result.Success)throw new IOException("Source menu neutral glove "+file+": "+request.error);Build(request.downloadHandler.data,parent,left);}finally{if(ReferenceEquals(pending,request))pending=null;}
            }
        }
        void Build(byte[] bytes,Transform parent,bool left)
        {
            using(var stream=new MemoryStream(bytes,false))using(var r=new BinaryReader(stream))
            {
                if(r.ReadInt32()!=0x5232334d)throw new IOException("source menu glove mesh header");int count=r.ReadInt32();if(count<1||count>4)throw new IOException("source menu glove surface count");
                for(int part=0;part<count;part++)
                {
                    int nv=r.ReadInt32(),ni=r.ReadInt32(),material=r.ReadInt32();if(nv<3||nv>2048||ni<3||ni>12000||ni%3!=0||material!=0)throw new IOException("source menu glove mesh limits");var vertices=new Vector3[nv];var normals=new Vector3[nv];var uv=new Vector2[nv];var triangles=new int[ni];
                    for(int i=0;i<nv;i++){vertices[i]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());normals[i]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());uv[i]=new Vector2(r.ReadSingle(),r.ReadSingle());if(!Finite(vertices[i])||!Finite(normals[i])||normals[i].sqrMagnitude<.99f||normals[i].sqrMagnitude>1.01f||!Finite(new Vector3(uv[i].x,uv[i].y,0)))throw new IOException("nonfinite source menu glove attributes");}
                    for(int i=0;i<ni;i++){triangles[i]=r.ReadInt32();if(triangles[i]<0||triangles[i]>=nv)throw new IOException("source menu glove triangle index");}
                    var mesh=new Mesh();meshes.Add(mesh);mesh.name="Source Grip5 neutral "+(left?"left":"right")+" glove";mesh.indexFormat=IndexFormat.UInt32;mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();mesh.RecalculateTangents();var node=new GameObject(mesh.name);objects.Add(node);node.layer=18;node.transform.SetParent(parent,false);node.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=node.AddComponent<MeshRenderer>();renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.enabled=false; // Committed appearance owner supplies the exact source glove material.
                }
                if(stream.Position!=stream.Length)throw new IOException("trailing source menu glove payload");
            }
        }
        static bool Finite(Vector3 value){return !Single.IsNaN(value.x)&&!Single.IsNaN(value.y)&&!Single.IsNaN(value.z)&&!Single.IsInfinity(value.x)&&!Single.IsInfinity(value.y)&&!Single.IsInfinity(value.z);}
        public void Dispose(){if(disposed)return;disposed=true;if(pending!=null)pending.Abort();foreach(var node in objects)if(node!=null){node.SetActive(false);UnityEngine.Object.Destroy(node);}objects.Clear();foreach(var mesh in meshes)if(mesh!=null)UnityEngine.Object.Destroy(mesh);meshes.Clear();}
    }
}
#endif

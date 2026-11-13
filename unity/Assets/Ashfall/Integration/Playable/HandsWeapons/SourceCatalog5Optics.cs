#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // Attach to the actual scaled weapon model. No colliders, camera or tracked-root writes.
    public sealed class SourceCatalog5Optics:MonoBehaviour
    {
        readonly List<UnityEngine.Object> resources=new List<UnityEngine.Object>();Transform assembly;bool initialized;
        public Transform RearReference {get;private set;}public Transform FrontReference {get;private set;}
        public void Initialize(string id,float modelScale,Transform actualMuzzle)
        {
            if(initialized)throw new InvalidOperationException("Catalog optic initialized");
            if(float.IsNaN(modelScale)||float.IsInfinity(modelScale)||modelScale<=0)throw new ArgumentOutOfRangeException("modelScale");initialized=true;
            if(id=="rfb")
            {
                if(actualMuzzle==null)throw new ArgumentNullException("actualMuzzle");
                gameObject.AddComponent<R23ReflexOptic>().Initialize(modelScale,actualMuzzle,.165f-.006f/modelScale);
                // The reused housing helper defers primitive-collider destruction until end of
                // frame; disable its visual colliders immediately before any host physics query.
                foreach(var c in GetComponentsInChildren<Collider>(true))c.enabled=false;
                foreach(var r in GetComponentsInChildren<Renderer>(true))r.gameObject.layer=18;
                return;
            }
            if(id!="s85")return;
            assembly=new GameObject("Source S85 floating ring-dot sight").transform;assembly.SetParent(transform,false);assembly.localPosition=Vector3.down*(.0381f/modelScale);
            var shader=Shader.Find("Universal Render Pipeline/Unlit");if(shader==null)throw new InvalidOperationException("URP Unlit required for source S85 rings");
            var glow=new Material(shader);glow.SetColor("_BaseColor",new Color(.96f,.025f,.76f));resources.Add(glow);
            var red=new Material(shader);red.SetColor("_BaseColor",new Color(1,.045f,.025f));resources.Add(red);
            float rear=.135f-.01f/1.12f,front=Mathf.Lerp(rear,.045f,(.605f-.015f)/(80.76f-.015f));
            Part("RearRingLuminousEdge",new Vector3(0,rear,.0125f),Ring(.016f,.0004f),glow);
            Part("FrontAlignmentRing",new Vector3(0,(rear+front)*.5f,.31f),Ring(.0101f,.0003f),glow);
            Part("SolidFrontDot",new Vector3(0,front,.605f),Sphere(.0022f),red);
            RearReference=Marker("RearReference",new Vector3(0,rear,.015f));FrontReference=Marker("FrontReference",new Vector3(0,front,.605f));
        }
        Transform Marker(string name,Vector3 p){var t=new GameObject(name).transform;t.SetParent(assembly,false);t.localPosition=p;return t;}
        void Part(string name,Vector3 p,Mesh mesh,Material material)
        {resources.Add(mesh);var go=new GameObject(name);go.layer=18;go.transform.SetParent(assembly,false);go.transform.localPosition=p;go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;}
        static Mesh Ring(float radius,float tube)
        {
            const int rings=48,sides=8;var v=new Vector3[(rings+1)*(sides+1)];var n=new Vector3[v.Length];var indices=new int[rings*sides*6];int at=0;
            for(int i=0;i<=rings;i++)for(int j=0;j<=sides;j++)
            {float u=i*Mathf.PI*2/rings,w=j*Mathf.PI*2/sides;int p=i*(sides+1)+j;n[p]=new Vector3(Mathf.Cos(w)*Mathf.Cos(u),Mathf.Cos(w)*Mathf.Sin(u),Mathf.Sin(w));v[p]=new Vector3(radius*Mathf.Cos(u),radius*Mathf.Sin(u),0)+n[p]*tube;
                if(i<rings&&j<sides){indices[at++]=p;indices[at++]=p+sides+1;indices[at++]=p+1;indices[at++]=p+1;indices[at++]=p+sides+1;indices[at++]=p+sides+2;}}
            return Mesh(v,n,indices,"Source S85 torus48x8");
        }
        static Mesh Sphere(float radius)
        {
            const int columns=12,rows=7;var v=new Vector3[(columns+1)*(rows+1)];var n=new Vector3[v.Length];var ix=new int[columns*rows*6];int at=0;
            for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++)
            {float u=x*Mathf.PI*2/columns,w=y*Mathf.PI/rows;int p=y*(columns+1)+x;n[p]=new Vector3(Mathf.Sin(w)*Mathf.Cos(u),Mathf.Cos(w),Mathf.Sin(w)*Mathf.Sin(u));v[p]=n[p]*radius;
                if(x<columns&&y<rows){ix[at++]=p;ix[at++]=p+1;ix[at++]=p+columns+1;ix[at++]=p+1;ix[at++]=p+columns+2;ix[at++]=p+columns+1;}}
            return Mesh(v,n,ix,"Source S85 sphere12x6");
        }
        static Mesh Mesh(Vector3[] v,Vector3[] n,int[] indices,string name){var m=new Mesh();m.name=name;m.vertices=v;m.normals=n;m.triangles=indices;m.RecalculateBounds();return m;}
        void OnDestroy(){foreach(var resource in resources)if(resource!=null)Destroy(resource);if(assembly!=null)Destroy(assembly.gameObject);}
    }
}
#endif

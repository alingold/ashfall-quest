#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using UnityEngine;
namespace Ashfall.Integration.Playable.Explosives
{
    // Small reusable low-poly mesh vocabulary. One combined mesh per surface group.
    public sealed class R25ItemArtGeometry
    {
        readonly List<CombineInstance> pieces = new List<CombineInstance>();
        readonly List<Mesh> owned = new List<Mesh>();
        public void Box(Vector3 at, Vector3 size, Vector3 angles)
        {
            var mesh = new Mesh();
            mesh.vertices = new[]{new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f),new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f)};
            mesh.triangles = new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5};
            Add(mesh, at, size, angles);
        }
        public void Ring(Vector3 at,float radius,float tube,Vector3 angles,int segments=16)
        {
            var v=new Vector3[segments*6];var ix=new int[segments*36];int k=0;
            for(int i=0;i<segments;i++)for(int j=0;j<6;j++){
                float a=i*Mathf.PI*2/segments,b=j*Mathf.PI/3;
                v[i*6+j]=new Vector3(Mathf.Cos(a)*(radius+tube*Mathf.Cos(b)),tube*Mathf.Sin(b),Mathf.Sin(a)*(radius+tube*Mathf.Cos(b)));
                int n=((i+1)%segments)*6,m=(j+1)%6,c=i*6;
                ix[k++]=c+j;ix[k++]=n+m;ix[k++]=n+j;ix[k++]=c+j;ix[k++]=c+m;ix[k++]=n+m;
            }
            var mesh=new Mesh();mesh.vertices=v;mesh.triangles=ix;Add(mesh,at,Vector3.one,angles);
        }
        public void Cylinder(Vector3 at,float radius,float height,Vector3 angles,int segments=12,float taper=1)
        {
            var v=new Vector3[segments*2+2];var ix=new int[segments*12];int k=0;
            for(int i=0;i<segments;i++){
                float a=i*Mathf.PI*2/segments;v[i]=new Vector3(Mathf.Cos(a)*radius,-height/2,Mathf.Sin(a)*radius);
                v[i+segments]=new Vector3(Mathf.Cos(a)*radius*taper,height/2,Mathf.Sin(a)*radius*taper);
                int n=(i+1)%segments;ix[k++]=i;ix[k++]=n+segments;ix[k++]=n;ix[k++]=i;ix[k++]=i+segments;ix[k++]=n+segments;
                ix[k++]=segments*2;ix[k++]=i;ix[k++]=n;ix[k++]=segments*2+1;ix[k++]=n+segments;ix[k++]=i+segments;
            }
            v[segments*2]=Vector3.down*height/2;v[segments*2+1]=Vector3.up*height/2;
            var mesh=new Mesh();mesh.vertices=v;mesh.triangles=ix;Add(mesh,at,Vector3.one,angles);
        }
        void Add(Mesh mesh,Vector3 at,Vector3 size,Vector3 angles)
        {mesh.RecalculateNormals();owned.Add(mesh);pieces.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(at,Quaternion.Euler(angles),size)});}
        public Mesh Build(string name)
        {var mesh=new Mesh();mesh.name=name;mesh.CombineMeshes(pieces.ToArray(),true,true);mesh.RecalculateBounds();foreach(var m in owned)Object.Destroy(m);owned.Clear();pieces.Clear();return mesh;}
        public static Transform Attach(Transform parent,string name,R25ItemArtGeometry geometry,Material material)
        {var go=new GameObject(name);go.layer=parent.gameObject.layer;go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=geometry.Build(name);go.AddComponent<MeshRenderer>().sharedMaterial=material;return go.transform;}
        public static Material Surface(Color color,float metallic,float smoothness,bool glow=false)
        {var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smoothness);if(glow){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*2);}return m;}
    }
    public static class R25ThrowableArt
    {
        public static Transform Create(Transform parent,string kind)
        {
            var root=new GameObject(kind+" distinct canister").transform;root.SetParent(parent,false);root.gameObject.layer=18;
            bool flash=kind=="flash";var body=new R25ItemArtGeometry();var dark=new R25ItemArtGeometry();var accent=new R25ItemArtGeometry();
            float radius=flash?.032f:.038f,height=flash?.12f:.17f;
            // Center remains the old grip origin. Smoke is taller; flash has six-sided caps.
            if(flash){
                dark.Cylinder(Vector3.zero,.025f,height,Vector3.zero);
                body.Cylinder(new Vector3(0,-.065f,0),.037f,.018f,Vector3.zero,6);
                body.Cylinder(new Vector3(0,.065f,0),.037f,.018f,Vector3.zero,6);
                // Open cage: actual gaps, not painted holes, reveal a warm inner core.
                for(int i=0;i<10;i++){float a=i*Mathf.PI/5;body.Box(new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius),new Vector3(.008f,.12f,.006f),new Vector3(0,-a*Mathf.Rad2Deg,0));}
                for(int i=-2;i<=2;i++)body.Ring(new Vector3(0,i*.022f,0),radius,.004f,Vector3.zero,10);
                accent.Ring(new Vector3(0,-.044f,0),.037f,.005f,Vector3.zero,12);
            }else{
                body.Cylinder(Vector3.zero,radius,height,Vector3.zero,16);
                dark.Cylinder(new Vector3(0,.093f,0),.021f,.026f,Vector3.zero,8);
                accent.Cylinder(new Vector3(0,.005f,0),radius+.001f,.045f,Vector3.zero,16);
                // Three pale stencil bars read without a texture or font dependency.
                for(int i=0;i<3;i++)dark.Box(new Vector3((i-1)*.011f,.005f,.039f),new Vector3(.006f,.024f,.002f),Vector3.zero);
            }
            body.Box(new Vector3(.028f,.05f,0),new Vector3(.012f,.12f,.014f),new Vector3(0,0,14));
            dark.Ring(new Vector3(-.018f,height/2+.03f,0),.018f,.003f,new Vector3(90,0,0),12);
            var steel=R25ItemArtGeometry.Surface(flash?new Color(.64f,.69f,.70f):new Color(.23f,.28f,.18f),flash?.45f:.05f,flash?.38f:.16f);
            var black=R25ItemArtGeometry.Surface(new Color(.045f,.055f,.06f),.3f,.25f);
            var band=R25ItemArtGeometry.Surface(flash?new Color(1,.68f,.07f):new Color(.75f,.22f,.07f),0,.25f);
            R25ItemArtGeometry.Attach(root,"Canister and safety spoon",body,steel);R25ItemArtGeometry.Attach(root,"Fuze ring and stencil",dark,black);R25ItemArtGeometry.Attach(root,"Identification band",accent,band);
            if(flash){var core=new R25ItemArtGeometry();core.Cylinder(Vector3.zero,.026f,.11f,Vector3.zero);R25ItemArtGeometry.Attach(root,"Warm vent core",core,R25ItemArtGeometry.Surface(new Color(.85f,.68f,.25f),0,.25f,true));}
            return root;
        }
    }
}
#endif

#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
using Ashfall.Core;
namespace Ashfall.Integration.Playable.Explosives
{
    public struct R24ExplosionVisualState {public string Kind;public Vector3 Position;public float Age;public bool Active,PressureGrounded;}
    // Source R5 mesh/shader effect, isolated from damage and projectile lifetime.
    // Eight reused instances in the host; no primitive colliders or per-frame allocations.
    internal sealed class R24ExplosionVisuals:IDisposable
    {
        internal sealed class Library:IDisposable
        {
            internal readonly Mesh Fire=Sphere(20,10,.5f),Smoke=Sphere(16,8,.5f),Zone=Sphere(16,8,.6f),Ring=Torus(),Pressure=Quad();
            public void Dispose(){foreach(var m in new[]{Fire,Smoke,Zone,Ring,Pressure})UnityEngine.Object.Destroy(m);}
            static Mesh Sphere(int radial,int rings,float radius)
            {
                int vertical=rings+1;var p=new Vector3[(radial+1)*(vertical+1)];var n=new Vector3[p.Length];var uv=new Vector2[p.Length];var ix=new int[radial*vertical*6];int at=0;
                for(int y=0;y<=vertical;y++)for(int x=0;x<=radial;x++){float a=x*Mathf.PI*2/radial,b=y*Mathf.PI/vertical;int i=y*(radial+1)+x;var q=new Vector3(Mathf.Cos(a)*Mathf.Sin(b),Mathf.Cos(b),-Mathf.Sin(a)*Mathf.Sin(b));p[i]=q*radius;n[i]=q;uv[i]=new Vector2((float)x/radial,(float)y/vertical);if(x<radial&&y<vertical){foreach(int k in new[]{i,i+radial+1,i+1,i+1,i+radial+1,i+radial+2})ix[at++]=k;}}
                var m=new Mesh();m.name="Actual source SphereMesh "+radial+"x"+rings;m.vertices=p;m.normals=n;m.uv=uv;m.triangles=ix;m.RecalculateBounds();return m;
            }
            static Mesh Torus()
            {
                const int r=40,s=6;var p=new Vector3[(r+1)*(s+1)];var n=new Vector3[p.Length];var ix=new int[r*s*6];int at=0;
                for(int i=0;i<=r;i++)for(int j=0;j<=s;j++){float a=i*Mathf.PI*2/r,b=j*Mathf.PI*2/s;int k=i*(s+1)+j;float extent=.95f+.05f*Mathf.Cos(b);p[k]=new Vector3(Mathf.Cos(a)*extent,.05f*Mathf.Sin(b),-Mathf.Sin(a)*extent);n[k]=new Vector3(Mathf.Cos(a)*Mathf.Cos(b),Mathf.Sin(b),-Mathf.Sin(a)*Mathf.Cos(b));if(i<r&&j<s)foreach(int index in new[]{k,k+s+1,k+1,k+1,k+s+1,k+s+2})ix[at++]=index;}
                var m=new Mesh();m.name="Source purple TorusMesh .90/1 40x6";m.vertices=p;m.normals=n;m.triangles=ix;m.RecalculateBounds();return m;
            }
            static Mesh Quad(){var m=new Mesh();m.name="Source pressure QuadMesh2x2";m.vertices=new[]{new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(1,1,0),new Vector3(-1,1,0)};m.normals=new[]{Vector3.forward,Vector3.forward,Vector3.forward,Vector3.forward};m.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};m.triangles=new[]{0,1,2,0,2,3};m.RecalculateBounds();return m;}
        }
        sealed class Batch:IDisposable
        {
            internal readonly GameObject Object;readonly Mesh mesh;readonly Vector3[] p,n;readonly Vector3[] basePoints;readonly Vector3[] baseNormals;readonly int count;
            internal Batch(string label,Transform parent,Vector3 size,int instances,Material material)
            {
                count=instances;Object=new GameObject(label);Object.transform.SetParent(parent,false);Object.layer=2;mesh=new Mesh();mesh.name=label;mesh.MarkDynamic();p=new Vector3[count*24];n=new Vector3[p.Length];basePoints=new Vector3[24];baseNormals=new Vector3[24];var ix=new int[count*36];int at=0;
                for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2)
                {
                    Vector3 normal=axis==0?Vector3.right*sign:axis==1?Vector3.up*sign:Vector3.forward*sign;Vector3 u=axis==1?Vector3.right:Vector3.up,v=Vector3.Cross(normal,u);int k=(axis*2+(sign==1?1:0))*4;
                    baseNormals[k]=baseNormals[k+1]=baseNormals[k+2]=baseNormals[k+3]=normal;
                    basePoints[k]=Vector3.Scale(normal-u-v,size)*.5f;basePoints[k+1]=Vector3.Scale(normal+u-v,size)*.5f;basePoints[k+2]=Vector3.Scale(normal+u+v,size)*.5f;basePoints[k+3]=Vector3.Scale(normal-u+v,size)*.5f;
                }
                for(int i=0;i<count;i++)for(int face=0;face<6;face++){int k=i*24+face*4;foreach(int x in new[]{0,1,2,0,2,3})ix[at++]=k+x;}mesh.vertices=p;mesh.normals=n;mesh.triangles=ix;Object.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=Object.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            }
            internal void Set(int i,Vector3 center,Quaternion rotation,float scale)
            {for(int j=0;j<24;j++){int k=i*24+j;p[k]=center+rotation*(basePoints[j]*scale);n[k]=rotation*baseNormals[j];}}
            internal void Publish(){mesh.vertices=p;mesh.normals=n;mesh.RecalculateBounds();}
            public void Dispose(){UnityEngine.Object.Destroy(mesh);}
        }
        readonly GameObject root,blast,zone;readonly Light light;readonly Transform core,pressure,zoneCore;readonly Transform[] fire=new Transform[7],smoke=new Transform[9],rings=new Transform[3];readonly Batch embers,debris,sparks;readonly Material coreM,fireM,smokeM,pressureM,emberM,debrisM,ringM,zoneCoreM,sparkM;readonly List<Material> materials=new List<Material>();bool grounded;float age=3;string kind;
        static Vector3 V(Float3 p){return new Vector3(p.X,p.Y,p.Z);}static Color Purple(float a){return new Color(173/255f,88/255f,1,a);}
        // Reflection conjugates the source YXZ Euler basis; it is applied once.
        static Quaternion Euler(Float3 r){return Quaternion.AngleAxis(-r.Y*Mathf.Rad2Deg,Vector3.up)*Quaternion.AngleAxis(-r.X*Mathf.Rad2Deg,Vector3.right)*Quaternion.AngleAxis(r.Z*Mathf.Rad2Deg,Vector3.forward);}
        Material Material(Shader shader,int mode,bool additive=false,float hot=0){var m=new Material(shader);m.SetFloat("_Mode",mode);m.SetFloat("_DstBlend",additive?1:10);m.SetFloat("_Hot",hot);m.SetColor("_Color",Color.white);materials.Add(m);return m;}
        static Transform Part(string label,Transform parent,Mesh mesh,Material material){var go=new GameObject(label);go.layer=2;go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;return go.transform;}
        internal R24ExplosionVisuals(Transform parent,Library library,Shader shader)
        {
            root=new GameObject("R5 reused original explosive effect");root.transform.SetParent(parent,false);blast=new GameObject("Original fire smoke pressure debris");blast.transform.SetParent(root.transform,false);zone=new GameObject("Original purple energy bloom");zone.transform.SetParent(root.transform,false);
            light=root.AddComponent<Light>();light.type=LightType.Point;light.range=10;light.shadows=LightShadows.None;
            coreM=Material(shader,0,false,1);fireM=Material(shader,0);smokeM=Material(shader,1);pressureM=Material(shader,2,true);emberM=Material(shader,3,true);debrisM=Material(shader,3);debrisM.SetColor("_Color",new Color(52/255f,48/255f,43/255f,1));ringM=Material(shader,3,true);zoneCoreM=Material(shader,3,true);sparkM=Material(shader,3,true);
            core=Part("Hot core",blast.transform,library.Fire,coreM);for(int i=0;i<fire.Length;i++){fire[i]=Part("Fire "+i,blast.transform,library.Fire,fireM);fire[i].localRotation=Euler(new Float3(i*.71f,i*1.31f,i*.43f));}
            for(int i=0;i<smoke.Length;i++){smoke[i]=Part("Smoke "+i,blast.transform,library.Smoke,smokeM);smoke[i].localRotation=Euler(new Float3(i*.57f,i*.93f,i*.37f));}
            pressure=Part("Ground pressure",blast.transform,library.Pressure,pressureM);pressure.localRotation=Euler(new Float3(-Mathf.PI*.5f,0,0));embers=new Batch("20 source ember instances combined",blast.transform,new Vector3(.018f,.018f,.16f),20,emberM);debris=new Batch("9 source debris instances combined",blast.transform,new Vector3(.075f,.055f,.10f),9,debrisM);
            for(int i=0;i<3;i++){rings[i]=Part("Purple ring "+i,zone.transform,library.Ring,ringM);rings[i].localRotation=Euler(new Float3(i*.4f,0,i*.9f));}zoneCore=Part("Purple core",zone.transform,library.Zone,zoneCoreM);sparks=new Batch("28 source purple shards combined",zone.transform,new Vector3(.025f,.025f,.3f),28,sparkM);root.SetActive(false);
        }
        internal void Play(string type,Vector3 position,bool pressureGrounded,Vector3 ground)
        {kind=type;age=0;grounded=pressureGrounded;light.color=type=="zone"?Purple(1):new Color(1,155/255f,67/255f);root.transform.SetPositionAndRotation(position,Quaternion.identity);pressure.position=ground+Vector3.up*.025f;blast.SetActive(type!="zone");zone.SetActive(type=="zone");root.SetActive(true);Present();}
        internal void Step(float dt){if(age>=2.5f)return;age+=dt;if(age>=2.5f){Clear();return;}Present();}
        void Present()
        {
            light.intensity=R24ExplosionMotion.LightEnergy(age);light.enabled=light.intensity>0;
            if(kind=="zone")
            {
                float fade=R24ExplosionMotion.ZoneFade(age);ringM.SetColor("_Color",Purple(fade*.55f));zoneCoreM.SetColor("_Color",Purple(Mathf.Max(0,1-age/.55f)));sparkM.SetColor("_Color",Purple(fade));
                for(int i=0;i<3;i++)rings[i].localScale=Vector3.one*R24ExplosionMotion.ZoneRingScale(age,i);zoneCore.localScale=Vector3.one*(.3f+age*2.8f);
                for(int i=0;i<28;i++){Vector3 direction=V(R24ExplosionMotion.ZoneDirection(i)),position=V(R24ExplosionMotion.ZoneSparkPosition(age,i));sparks.Set(i,position,Quaternion.LookRotation(direction*20-position,Vector3.up),1);}sparks.Publish();return;
            }
            bool rocket=kind=="smaw";coreM.SetFloat("_Age",age);fireM.SetFloat("_Age",age);smokeM.SetFloat("_Age",age);pressureM.SetFloat("_Age",age);core.gameObject.SetActive(age<.62f);core.localScale=Vector3.one*R24ExplosionMotion.CoreScale(age,rocket);
            for(int i=0;i<fire.Length;i++){fire[i].gameObject.SetActive(age<.82f);fire[i].localPosition=V(R24ExplosionMotion.FirePosition(age,i,rocket));fire[i].localScale=V(R24ExplosionMotion.FireScale(age,i,rocket));}
            for(int i=0;i<smoke.Length;i++){smoke[i].gameObject.SetActive(age>.045f&&age<2.45f);smoke[i].localPosition=V(R24ExplosionMotion.SmokePosition(age,i,rocket));smoke[i].localScale=V(R24ExplosionMotion.SmokeScale(age,i,rocket));}
            pressure.localScale=Vector3.one*(.30f+age*12);pressure.gameObject.SetActive(grounded&&age<.55f);
            float emberFade=R24ExplosionMotion.EmberFade(age);embers.Object.SetActive(age<1.65f);emberM.SetColor("_Color",new Color(1,.28f+emberFade*.42f,.035f+emberFade*.18f,emberFade));
            for(int i=0;i<20;i++)embers.Set(i,V(R24ExplosionMotion.EmberPosition(age,i)),Quaternion.LookRotation(V(R24ExplosionMotion.EmberDirection(i)),Vector3.up),Mathf.Max(.1f,1-age*.43f));embers.Publish();debris.Object.SetActive(age<2.25f);debrisM.SetColor("_Color",new Color(52/255f,48/255f,43/255f,R24ExplosionMotion.DebrisFade(age)));
            for(int i=0;i<9;i++)debris.Set(i,V(R24ExplosionMotion.DebrisPosition(age,i)),Euler(R24ExplosionMotion.DebrisEuler(age,i)),1);debris.Publish();
        }
        internal void Clear(){age=3;if(root!=null)root.SetActive(false);}
        internal R24ExplosionVisualState Read(){return new R24ExplosionVisualState{Kind=kind,Age=age,Position=root.transform.position,Active=root.activeSelf,PressureGrounded=grounded};}
        public void Dispose(){embers.Dispose();debris.Dispose();sparks.Dispose();foreach(var m in materials)UnityEngine.Object.Destroy(m);UnityEngine.Object.Destroy(root);}
    }
}
#endif

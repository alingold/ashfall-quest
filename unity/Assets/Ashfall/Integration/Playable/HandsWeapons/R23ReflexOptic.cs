#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // Port of common_reflex_090 + authored_reflex_sight: explicit MK18 holographic mount.
    // The bore remains the tracked weapon's muzzle. The shader projects that target for each eye.
    internal sealed class R23ReflexOptic : MonoBehaviour
    {
        readonly List<Material> materials=new List<Material>();
        Material reticle;
        Mesh rings,secondaryRing;
        public Transform Window {get;private set;}
        public Transform RearReference {get;private set;}public Transform FrontReference {get;private set;}
        public void SetAimTarget(Vector3 point){if(reticle!=null)reticle.SetVector("_AimTarget",point);}
        Transform muzzle;
        // authored_ring_dot_sight.gd retains only these floating luminous rings and red bead.
        public void InitializeAkRings() {
            float rear=.135f,front=Mathf.Lerp(rear,.045f,(.605f-.015f)/(80.76f-.015f));
            var glow=new Material(Shader.Find("Universal Render Pipeline/Unlit"));glow.SetColor("_BaseColor",new Color(.96f,.025f,.76f));materials.Add(glow);
            var dot=new Material(Shader.Find("Universal Render Pipeline/Unlit"));dot.SetColor("_BaseColor",new Color(1,.045f,.025f));materials.Add(dot);
            rings=RingMesh();
            RearReference=Ring("Source rear luminous alignment ring",new Vector3(0,rear,.0125f),1,glow);
            Ring("Source forward alignment ring",new Vector3(0,(rear+front)*.5f,.31f),.0101f/.016f,glow);
            var bead=GameObject.CreatePrimitive(PrimitiveType.Sphere);bead.name="Source solid red front bead";FrontReference=bead.transform;bead.transform.SetParent(transform,false);bead.transform.localPosition=new Vector3(0,front,.605f);bead.transform.localScale=Vector3.one*.0044f;Destroy(bead.GetComponent<Collider>());bead.GetComponent<Renderer>().sharedMaterial=dot;
        }
        public void InitializeCx4Rings(Transform bore){
            muzzle=bore;float rear=.135f,front=Mathf.Lerp(rear,.045f,(.465f-.015f)/(80.62f-.015f));
            var glow=new Material(Shader.Find("Universal Render Pipeline/Unlit"));glow.SetColor("_BaseColor",new Color(.96f,.025f,.76f));materials.Add(glow);
            rings=RingMesh();secondaryRing=RingMesh(.0101f,.0003f);
            Ring("Source CX4 rear luminous alignment ring",new Vector3(0,rear,.0125f),1,glow);
            Ring("Source CX4 forward alignment ring",new Vector3(0,(rear+front)*.5f,.24f),1,glow,secondaryRing);
            RearReference=new GameObject("Source CX4 rear reference").transform;RearReference.SetParent(transform,false);RearReference.localPosition=new Vector3(0,rear,.015f);
            FrontReference=new GameObject("Source CX4 front reference").transform;FrontReference.SetParent(transform,false);FrontReference.localPosition=new Vector3(0,front,.465f);
            var shader=Resources.Load<Shader>("R23Cx4Reticle");if(shader==null)throw new InvalidOperationException("Missing R23Cx4Reticle shader");reticle=new Material(shader);materials.Add(reticle);
            var lens=GameObject.CreatePrimitive(PrimitiveType.Quad);lens.name="Source CX4 R5 circular collimated dot";lens.transform.SetParent(FrontReference,false);lens.transform.localPosition=new Vector3(0,0,-.004f);lens.transform.localScale=new Vector3(.034f,.034f,1);Destroy(lens.GetComponent<Collider>());lens.GetComponent<Renderer>().sharedMaterial=reticle;
            Update();
        }
        Transform Ring(string label,Vector3 at,float scale,Material material,Mesh mesh=null) {
            var go=new GameObject(label);go.transform.SetParent(transform,false);go.transform.localPosition=at;go.transform.localScale=Vector3.one*scale;go.AddComponent<MeshFilter>().sharedMesh=mesh==null?rings:mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;return go.transform;
        }
        static Mesh RingMesh(float radius=.016f,float tube=.0004f) {
            const int steps=48,sides=8;var v=new Vector3[(steps+1)*(sides+1)];var n=new Vector3[v.Length];var indices=new int[steps*sides*6];int cursor=0;
            for(int i=0;i<=steps;i++)for(int j=0;j<=sides;j++) {
                float u=i*Mathf.PI*2/steps,w=j*Mathf.PI*2/sides;int p=i*(sides+1)+j;
                n[p]=new Vector3(Mathf.Cos(w)*Mathf.Cos(u),Mathf.Cos(w)*Mathf.Sin(u),Mathf.Sin(w));
                v[p]=new Vector3(radius*Mathf.Cos(u),radius*Mathf.Sin(u),0)+n[p]*tube;
                if(i<steps&&j<sides){indices[cursor++]=p;indices[cursor++]=p+sides+1;indices[cursor++]=p+1;indices[cursor++]=p+1;indices[cursor++]=p+sides+1;indices[cursor++]=p+sides+2;}
            }
            var mesh=new Mesh();mesh.name="Source ring sight torus";mesh.vertices=v;mesh.normals=n;mesh.triangles=indices;mesh.RecalculateBounds();return mesh;
        }
        public void Initialize(float modelScale,Transform bore,float mountHeight=.13f,float opticScale=1) {
            muzzle=bore;
            var mount=new GameObject("R23 authored holographic reflex").transform;mount.SetParent(transform,false);
            mount.localPosition=new Vector3(0,mountHeight,.10f);mount.localScale=Vector3.one*opticScale/modelScale;
            float width=.039f*Mathf.Sqrt(2),height=.035f*Mathf.Sqrt(2),half=height*.5f;
            var metal=Material(new Color(.11f,.13f,.14f),.8f,.38f);var steel=Material(new Color(.42f,.45f,.47f),.9f,.76f);
            foreach(int side in new[]{-1,1}) {
                Box(mount,new Vector3(width+.006f,.003f,.028f),new Vector3(0,side*(half+.0015f),.0112f),metal);
                Box(mount,new Vector3(.003f,height,.028f),new Vector3(side*(width*.5f+.0015f),0,.0112f),metal);
                Box(mount,new Vector3(.005f,.007f,.010f),new Vector3(side*(width*.5f+.005f),-half-.006f,-.008f),metal);
                for(int i=0;i<5;i++)Box(mount,new Vector3(.0025f,.010f,.0015f),new Vector3(side*(width*.5f+.0028f),-.008f,.018f-i*.004f),metal);
                Box(mount,new Vector3(.006f,.002f,.005f),new Vector3(side*.013f,-half-.002f,-.006f),steel);
            }
            Box(mount,new Vector3(width+.012f,.012f,.068f),new Vector3(0,-half-.009f,.014f),metal);
            Box(mount,new Vector3(.034f,.008f,.052f),new Vector3(0,-half-.019f,.014f),metal);
            Box(mount,new Vector3(.020f,.002f,.034f),new Vector3(0,-half-.001f,.012f),steel);
            var shader=Resources.Load<Shader>("R23ReflexReticle");
            if(shader==null)throw new InvalidOperationException("Missing R23ReflexReticle shader");
            reticle=new Material(shader);materials.Add(reticle);
            var lens=GameObject.CreatePrimitive(PrimitiveType.Quad);lens.name="Clear reflex window and stereo bore dot";Window=lens.transform;lens.transform.SetParent(mount,false);lens.transform.localPosition=new Vector3(0,0,-.004f);lens.transform.localScale=new Vector3(width,height,1);Destroy(lens.GetComponent<Collider>());lens.GetComponent<Renderer>().sharedMaterial=reticle;
            Update();
        }
        Material Material(Color color,float metallic,float smoothness) {
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smoothness);materials.Add(m);return m;
        }
        static void Box(Transform parent,Vector3 size,Vector3 position,Material material) {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Source reflex housing";go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;Destroy(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;
        }
        void Update() { if(reticle!=null&&muzzle!=null)reticle.SetVector("_AimTarget",muzzle.position+muzzle.forward*180); }
        void OnDestroy() { foreach(var m in materials)if(m!=null)Destroy(m);if(rings!=null)Destroy(rings);if(secondaryRing!=null)Destroy(secondaryRing); }
    }
}
#endif

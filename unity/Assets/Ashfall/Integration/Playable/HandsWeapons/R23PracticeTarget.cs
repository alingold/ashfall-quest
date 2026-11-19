#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using Ashfall.Modules.HealthStates;
using UnityEngine;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // Source practice_dummy.gd dimensions, colours, hitboxes and native-health readout.
    internal sealed class R23PracticeTarget : MonoBehaviour
    {
        readonly List<Material> materials=new List<Material>();
        readonly List<Mesh> meshes=new List<Mesh>();
        public string TargetId { get; private set; }
        public Collider Body { get; private set; }
        public Collider Head { get; private set; }
        TextMesh readout;
        public void Initialize(string id,Camera camera) {
            TargetId=id;
            var body=gameObject.AddComponent<BoxCollider>();body.size=new Vector3(.58f,1.43f,.30f);body.center=new Vector3(0,.84f,0);Body=body;
            var head=new GameObject("Source head hitbox");head.transform.SetParent(transform,false);head.transform.localPosition=new Vector3(0,1.72f,0);var box=head.AddComponent<BoxCollider>();box.size=new Vector3(.34f,.31f,.22f);Head=box;
            Piece(new Vector3(.55f,.82f,.18f),new Vector3(0,1.14f,0),new Color(.14f,.23f,.28f));
            Piece(new Vector3(.34f,.31f,.22f),new Vector3(0,1.72f,0),new Color(.3f,.78f,.87f));
            Piece(new Vector3(.78f,.12f,.15f),new Vector3(0,1.46f,0),new Color(.16f,.28f,.33f));
            Piece(new Vector3(.09f,.55f,.09f),new Vector3(0,.36f,0),new Color(.34f,.4f,.42f));
            Piece(new Vector3(.85f,.08f,.6f),new Vector3(0,.04f,0),new Color(.11f,.14f,.16f));
            var ringMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));ringMaterial.SetColor("_BaseColor",new Color(.98f,.47f,.13f));materials.Add(ringMaterial);
            for(int i=0;i<3;i++)Ring(.05f+i*.047f,.058f+i*.047f,new Vector3(0,1.15f,-.097f-i*.001f),ringMaterial);
            var label=new GameObject("PRACTICE TARGET native health readout");label.transform.SetParent(transform,false);label.transform.localPosition=new Vector3(0,2.15f,0);readout=label.AddComponent<TextMesh>();
            readout.font=Resources.Load<Font>("R23GodotUI")??Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");readout.fontSize=40;readout.characterSize=.025f;readout.anchor=TextAnchor.MiddleCenter;readout.alignment=TextAlignment.Center;readout.color=Color.white;R26WorldText.Bind(readout);
            label.AddComponent<R24SourceLabelBillboard>().Fallback=camera;
        }
        public bool Owns(Collider collider) { return collider==Body||collider==Head; }
        public void Refresh(R23RoundState round) {
            var state=round.ReadTarget(TargetId);if(state==null)return;
            readout.text="PRACTICE TARGET\nSHIELD "+Mathf.RoundToInt(state.Shield)+"  HEALTH "+Mathf.RoundToInt(state.Health)+"\nLAST "+Mathf.RoundToInt(round.LastTargetDamage(TargetId))+"  TOTAL "+Mathf.RoundToInt(round.TotalTargetDamage(TargetId));
            // Source dummy remains present after death and resets 1.5s after its last real hit.
            Body.enabled=Head.enabled=true;
        }
        void Piece(Vector3 size,Vector3 position,Color tint) {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Source mannequin piece";go.transform.SetParent(transform,false);go.transform.localPosition=position;go.transform.localScale=size;Destroy(go.GetComponent<Collider>());
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.SetColor("_BaseColor",tint);material.SetFloat("_Smoothness",.25f);materials.Add(material);go.GetComponent<Renderer>().sharedMaterial=material;
        }
        void Ring(float inner,float outer,Vector3 at,Material material) {
            const int steps=48,sides=8;float radius=(inner+outer)*.5f,tube=(outer-inner)*.5f;
            var vertices=new Vector3[(steps+1)*(sides+1)];var normals=new Vector3[vertices.Length];var indices=new int[steps*sides*6];int cursor=0;
            for(int i=0;i<=steps;i++)for(int j=0;j<=sides;j++) {
                float u=i*Mathf.PI*2/steps,w=j*Mathf.PI*2/sides;int p=i*(sides+1)+j;normals[p]=new Vector3(Mathf.Cos(w)*Mathf.Cos(u),Mathf.Cos(w)*Mathf.Sin(u),Mathf.Sin(w));vertices[p]=new Vector3(radius*Mathf.Cos(u),radius*Mathf.Sin(u),0)+normals[p]*tube;
                if(i<steps&&j<sides){indices[cursor++]=p;indices[cursor++]=p+sides+1;indices[cursor++]=p+1;indices[cursor++]=p+1;indices[cursor++]=p+sides+1;indices[cursor++]=p+sides+2;}
            }
            var mesh=new Mesh();mesh.vertices=vertices;mesh.normals=normals;mesh.triangles=indices;mesh.RecalculateBounds();meshes.Add(mesh);
            var ring=new GameObject("Source orange bullseye ring");ring.transform.SetParent(transform,false);ring.transform.localPosition=at;ring.AddComponent<MeshFilter>().sharedMesh=mesh;ring.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
        void OnDestroy() { foreach(var material in materials)Destroy(material);foreach(var mesh in meshes)Destroy(mesh); }
    }
}
#endif

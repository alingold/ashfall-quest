#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace Ashfall.Integration.Playable.Lobby
{
    // Four combined opaque meshes and one tiny ember batch. No colliders or gameplay.
    public sealed class R25NightCity : MonoBehaviour
    {
        GameObject root;
        readonly List<Object> owned = new List<Object>();
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<int> indices = new List<int>();
        Material wall, ember, teal;
        public void Build()
        {
            root=new GameObject("Night city presentation");root.transform.SetParent(transform,false);
            wall=Surface("Night distant towers",new Color(.035f,.045f,.065f),Color.black,true);
            ember=Surface("Night ember",Color.black,new Color(1,.416f,.169f),false);
            teal=Surface("Night teal",Color.black,new Color(.12f,.58f,.55f),false);
            for(int layer=0;layer<3;layer++)for(int i=0;i<22;i++)
            {
                float angle=i*Mathf.PI*2/22+.13f*layer;
                float radius=38+layer*28;
                float h=12+(i*17+layer*11)%29;
                Box(new Vector3(Mathf.Sin(angle)*radius,h/2,Mathf.Cos(angle)*radius),new Vector3(5+ i%4,h,6));
            }
            Box(new Vector3(-9,6.5f,10.05f),new Vector3(5.3f,1.6f,.12f));
            Flush(wall,"Layered skyline");
            for(int i=0;i<14;i++)
            {
                float x=(i%2==0?-1:1)*(10+i%3*2);float z=-18+i*4;
                Box(new Vector3(x,5,z),new Vector3(.10f,8,.10f));
                Box(new Vector3(x,9.2f,z),new Vector3(.24f,.16f,.24f));
            }
            // Broken perimeter light around the existing landing and outfit pads.
            Ring(new Vector3(1.35f,.325f,.65f),.85f);
            Ring(new Vector3(0,.08f,5),3.4f);
            for(int layer=0;layer<3;layer++)for(int i=0;i<22;i++)
            {
                float angle=i*Mathf.PI*2/22+.13f*layer;
                float radius=38+layer*28;
                float h=12+(i*17+layer*11)%29;
                Box(new Vector3(Mathf.Sin(angle)*radius,h+.12f,Mathf.Cos(angle)*radius),new Vector3(.22f,.18f,.22f));
            }
            Flush(ember,"Ember pylons and pad outlines");
            for(int i=0;i<12;i++)
                Box(new Vector3((i%2==0?-1:1)*9,2.7f,-20+i*4),new Vector3(.12f,3.8f,.12f));
            // Narrow billboard frames avoid large transparent cards.
            Box(new Vector3(-9,7,10),new Vector3(5,.08f,.12f));
            Box(new Vector3(-9,5.8f,10),new Vector3(5,.08f,.12f));
            Flush(teal,"Teal wayfinding");
            Label("ASHFALL",new Vector3(-9,6.5f,9.9f),.12f,new Color(1,.55f,.28f));
            Label("09 / NIGHT DOCK",new Vector3(9,4,12),.065f,new Color(.3f,.85f,.8f));
            StageLight("Neutral outfit key",new Vector3(1,3,-1.5f),new Color(1,.94f,.86f),12.2f,6);
            StageLight("Cool stage rim",new Vector3(3,2.8f,2.5f),new Color(.55f,.75f,1),2.2f,6);
            BuildEmbers();
        }
        Material Surface(string label,Color color,Color glow,bool facade)
        {
            var m=new Material(Resources.Load<Shader>("R23LobbySurface"));m.name=label;
            m.SetColor("_BaseColor",color);m.SetColor("_Emission",glow*1.5f);
            m.SetFloat("_Night",1);m.SetFloat("_Facade",facade?1:0);m.SetFloat("_Roughness",.8f);
            owned.Add(m);return m;
        }
        void Box(Vector3 at,Vector3 size)
        {
            Vector3[] axes={Vector3.right,Vector3.up,Vector3.forward};
            for(int a=0;a<3;a++)for(int sign=-1;sign<=1;sign+=2)
            {
                Vector3 n=axes[a]*sign,u=axes[(a+1)%3],v=axes[(a+2)%3]*sign;
                int start=vertices.Count;
                for(int k=0;k<4;k++)
                {
                    Vector3 corner=n+u*(k==0||k==3?-1:1)+v*(k<2?-1:1);
                    vertices.Add(at+Vector3.Scale(corner,size)*.5f);normals.Add(n);
                }
                indices.Add(start);indices.Add(start+1);indices.Add(start+2);
                indices.Add(start);indices.Add(start+2);indices.Add(start+3);
            }
        }
        void Ring(Vector3 at,float radius)
        {
            for(int i=0;i<48;i++)
            {float a=i*Mathf.PI*2/48;Box(at+new Vector3(Mathf.Sin(a)*radius,0,Mathf.Cos(a)*radius),new Vector3(.09f,.025f,.09f));}
        }
        void Flush(Material material,string label)
        {
            var mesh=new Mesh();mesh.name=label;mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();owned.Add(mesh);
            var go=new GameObject(label);go.transform.SetParent(root.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            vertices.Clear();normals.Clear();indices.Clear();
        }
        void Label(string text,Vector3 at,float size,Color color)
        {
            var go=new GameObject(text);go.transform.SetParent(root.transform,false);go.transform.localPosition=at;
            var t=go.AddComponent<TextMesh>();t.text=text;t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=64;t.characterSize=size;t.anchor=TextAnchor.MiddleCenter;t.color=color;
            R26WorldText.Bind(t);
        }
        void StageLight(string label,Vector3 at,Color color,float intensity,float range)
        {
            var go=new GameObject(label);go.transform.SetParent(root.transform,false);go.transform.localPosition=at;
            var l=go.AddComponent<Light>();l.type=LightType.Point;l.color=color;l.intensity=intensity;l.range=range;l.shadows=LightShadows.None;
        }
        void BuildEmbers()
        {
            var uv=new List<Vector2>();
            for(int i=0;i<160;i++)
            {
                float x=Mathf.Sin(i*76.2f)*14,z=Mathf.Cos(i*23.7f)*19,y=(i*.731f)%6;
                int start=vertices.Count;
                for(int k=0;k<4;k++){vertices.Add(new Vector3(x,y,z));normals.Add(Vector3.up);uv.Add(new Vector2(k==0||k==3?-1:1,k<2?-1:1));}
                indices.Add(start);indices.Add(start+1);indices.Add(start+2);indices.Add(start);indices.Add(start+2);indices.Add(start+3);
            }
            var mat=new Material(Resources.Load<Shader>("R25LobbyEmbers"));owned.Add(mat);
            Flush(mat,"160 drifting embers");var mesh=(Mesh)owned[owned.Count-1];mesh.SetUVs(0,uv);mesh.bounds=new Bounds(new Vector3(0,3,0),new Vector3(32,10,42));
        }
        public void SetVisible(bool show){if(root!=null)root.SetActive(show);}
        void OnDestroy(){foreach(var item in owned)if(item!=null)Destroy(item);if(root!=null)Destroy(root);}
    }
}
#endif

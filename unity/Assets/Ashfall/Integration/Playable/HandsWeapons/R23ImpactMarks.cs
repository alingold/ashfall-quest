#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Core;
using UnityEngine;
using UnityEngine.Rendering;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // Persistent source fracture patches, separate from the existing110ms spark.
    // Lifetime is world-owned, not carried-weapon presentation/holster state.
    public sealed class R23ImpactMarks:IDisposable,IR23ImpactProjection
    {
        readonly R23ImpactMarksState state=new R23ImpactMarksState();
        readonly Transform root;readonly Func<Collider,bool> actorOrIgnored;
        readonly Mesh[] meshes=new Mesh[R23ImpactMarksKernel.Capacity];readonly MeshRenderer[] renderers=new MeshRenderer[R23ImpactMarksKernel.Capacity];readonly MeshFilter[] filters=new MeshFilter[R23ImpactMarksKernel.Capacity];
        readonly Material material;readonly Texture2D texture;readonly MaterialPropertyBlock tint=new MaterialPropertyBlock();
        bool disposed;readonly float[] lastAlpha=new float[R23ImpactMarksKernel.Capacity];
        public int Count {get{return state.Count;}}public long Accepted {get{return state.Accepted;}}public long Rejected {get{return state.Rejected;}}
        public long Sequence {get{return state.Sequence;}}public int LastSlot {get{return state.Accepted==0?-1:(state.Cursor+R23ImpactMarksKernel.Capacity-1)%R23ImpactMarksKernel.Capacity;}}public int LastProjectionRays {get;private set;}
        public bool Paused {get{return state.Paused;}}
        public R23ImpactMarks(Transform content,Func<Collider,bool> actorOrIgnoredSurface)
        {
            if(content==null)throw new ArgumentNullException("content");actorOrIgnored=actorOrIgnoredSurface;
            root=new GameObject("R23 source impact marks512 /90s").transform;root.SetParent(content,false);
            var shader=Resources.Load<Shader>("R23ImpactMarks");if(shader==null)throw new InvalidOperationException("R23 impact shader missing");
            texture=new Texture2D(64,64,TextureFormat.RGBA32,true,false);texture.name="Original source radial fracture64";texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Trilinear;
            var pixels=new Color[64*64];for(int y=0;y<64;y++)for(int x=0;x<64;x++){float r,g,b,a;R23ImpactMarksKernel.TexturePixel(x,y,out r,out g,out b,out a);pixels[(63-y)*64+x]=new Color(r,g,b,a);}texture.SetPixels(pixels);texture.Apply(true,true);
            material=new Material(shader);material.name="R23 source unlit alpha fracture";material.SetTexture("_MainTex",texture);
            for(int i=0;i<renderers.Length;i++){
                var go=new GameObject("Source impact mark "+i);go.layer=19;go.transform.SetParent(root,false);filters[i]=go.AddComponent<MeshFilter>();renderers[i]=go.AddComponent<MeshRenderer>();renderers[i].sharedMaterial=material;renderers[i].shadowCastingMode=ShadowCastingMode.Off;renderers[i].receiveShadows=false;renderers[i].sortingOrder=i;if(renderers[i]!=null)renderers[i].enabled=false;lastAlpha[i]=0;
            }
        }
        R23ImpactSurface Describe(Collider collider)
        {
            var result=new R23ImpactSurface{Source=collider};if(collider==null)return result;
            result.Static=collider.attachedRigidbody==null&&!(collider is CharacterController);result.AllowedLayer=collider.gameObject.layer==0||collider.gameObject.layer==8||collider.gameObject.layer==19;
            result.Excluded=!collider.enabled||collider.isTrigger||!collider.gameObject.activeInHierarchy;
            result.DamageReceiver=actorOrIgnored!=null&&actorOrIgnored(collider);
            var metadata=collider.GetComponentInParent<R23ImpactMarkSurface>();if(metadata!=null){result.Rendered=metadata.RenderedBallisticSurface;result.Painted=metadata.PaintedBallisticSurface;result.Excluded|=metadata.BallisticProxyIgnore||metadata.StairSupport;result.DamageReceiver|=metadata.DamageReceiver;result.HitReceiver=metadata.HitReceiver;}
            return result;
        }
        R23ImpactSample IR23ImpactProjection.Cast(Float3 from,Float3 to)
        {
            Vector3 start=V(from),delta=V(to)-start;float best=float.PositiveInfinity;var sample=new R23ImpactSample();
            foreach(var hit in Physics.RaycastAll(start,delta.normalized,delta.magnitude,(1<<0)|(1<<8)|(1<<19),QueryTriggerInteraction.Ignore)){
                var surface=Describe(hit.collider);if(surface.Excluded||actorOrIgnored!=null&&actorOrIgnored(hit.collider)||hit.distance>=best)continue;
                best=hit.distance;sample=new R23ImpactSample{Found=true,Point=F(hit.point),Normal=F(hit.normal),Surface=surface};
            }
            return sample;
        }
        public bool AddHit(Vector3 point,Vector3 normal,Collider collider,bool pellet=false)
        {
            if(disposed||root==null)return false;R23ImpactPatch patch;LastProjectionRays=0;
            if(!R23ImpactMarksKernel.TryBuild(F(point),F(normal),Describe(collider),pellet,state.Sequence,this,out patch)){state.Reject();return false;}
            int index=state.Commit(collider);LastProjectionRays=patch.Rays;
            if(meshes[index]==null){meshes[index]=new Mesh();meshes[index].name="Source conformed impact patch8tri";meshes[index].MarkDynamic();filters[index].sharedMesh=meshes[index];}
            var points=new Vector3[9];var uv=new Vector2[9];for(int i=0;i<9;i++){points[i]=V(patch.Points[i])-V(patch.Origin);uv[i]=new Vector2(patch.U[i],patch.V[i]);}
            meshes[index].Clear();meshes[index].vertices=points;meshes[index].uv=uv;meshes[index].triangles=R23ImpactMarksKernel.Indices;meshes[index].RecalculateBounds();
            filters[index].transform.position=V(patch.Origin);filters[index].transform.rotation=Quaternion.identity;filters[index].transform.localScale=Vector3.one;renderers[index].enabled=true;
            tint.SetColor("_Color",Color.white);renderers[index].SetPropertyBlock(tint);lastAlpha[index]=1;return true;
        }
        static bool Alive(object source){var collider=source as Collider;return collider!=null&&collider.enabled&&collider.gameObject.activeInHierarchy;}
        public void Advance(float seconds){if(disposed||root==null)return;state.Advance(seconds,Alive);for(int i=0;i<renderers.Length;i++){float alpha=R23ImpactMarksKernel.Alpha(state.Age(i));if(renderers[i]==null||lastAlpha[i]==alpha)continue;lastAlpha[i]=alpha;renderers[i].enabled=alpha>0;if(alpha>0){tint.SetColor("_Color",new Color(1,1,1,alpha));renderers[i].SetPropertyBlock(tint);}}}
        public void SetPaused(bool value){state.Paused=value;}
        public void SetVisible(bool visible){if(!disposed&&root!=null)root.gameObject.SetActive(visible);}
        public void Clear(){if(disposed)return;state.Clear();for(int i=0;i<renderers.Length;i++){if(renderers[i]!=null)renderers[i].enabled=false;lastAlpha[i]=0;}}
        public float ReadAge(int slot){if(slot<0||slot>=renderers.Length)throw new ArgumentOutOfRangeException("slot");return state.Age(slot);}
        public bool TryReadWorldVertex(int slot,int vertex,out Vector3 point){point=Vector3.zero;if(slot<0||slot>=meshes.Length||vertex<0||vertex>=9||state.Age(slot)<0||meshes[slot]==null||filters[slot]==null)return false;point=filters[slot].transform.TransformPoint(meshes[slot].vertices[vertex]);return true;}
        public void Dispose(){if(disposed)return;Clear();disposed=true;foreach(var mesh in meshes)if(mesh!=null)UnityEngine.Object.Destroy(mesh);if(root!=null)UnityEngine.Object.Destroy(root.gameObject);if(material!=null)UnityEngine.Object.Destroy(material);if(texture!=null)UnityEngine.Object.Destroy(texture);}
        static Vector3 V(Float3 p){return new Vector3(p.X,p.Y,p.Z);}static Float3 F(Vector3 p){return new Float3(p.x,p.y,p.z);}
    }
}
#endif

#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using UnityEngine.Rendering;
namespace Ashfall.Integration.Playable.Appearance
{
    // Fixed world-space history; one mesh, one draw and one shared material per avatar.
    [DefaultExecutionOrder(1300)]
    public sealed class R26FlightTrails:MonoBehaviour
    {
        // All flight-trail tuning. Widths are full widths, distances metres, time seconds.
        const float Lifetime=.8f,SampleInterval=.025f,StartSpeed=3,FullSpeed=18,MinWidth=.06f,MaxWidth=.18f;
        const float HeadAlpha=.95f,TeleportDistance=8,MaxFrameGap=.2f,CoreEdge=.28f,GlowEdge=.72f,EdgeFeather=.9f;
        const int History=33,Rows=History+1;
        static readonly Color Core=new Color(1.8f,3.2f,3.5f,1),Glow=new Color(.02f,1.25f,2,1),Edge=new Color(.008f,.035f,.09f,1);

        struct Stamp {public Vector3 Left,Right;public float Time,Strength;}
        readonly Stamp[] history=new Stamp[History];
        readonly Vector3[] vertices=new Vector3[Rows*4],tangents=new Vector3[Rows*4];
        readonly Vector2[] uv=new Vector2[Rows*4];
        readonly Color[] colors=new Color[Rows*4];
        static Material shared;static int users;
        R23PlayerAppearance appearance;Mesh mesh;MeshRenderer view;GameObject art;int cursor,count;float nextSample,previousTime,emissionBlend;
        Vector3 previousRoot;bool haveRoot,ownsMaterial;
        public void Initialize(R23PlayerAppearance owner){appearance=owner;}
        void EnsureMesh()
        {
            if(mesh!=null)return;
            if(shared==null){shared=new Material(Resources.Load<Shader>("R26FlightTrails"));shared.name="R26 flight trails shared";shared.SetColor("_Core",Core);shared.SetColor("_Glow",Glow);shared.SetColor("_Edge",Edge);shared.SetVector("_Bands",new Vector4(CoreEdge,GlowEdge,EdgeFeather,0));}
            users++;ownsMaterial=true;art=new GameObject("R26 elbow flight trails");art.transform.SetParent(transform,false);
            mesh=new Mesh();mesh.name="R26 bounded elbow history";mesh.MarkDynamic();mesh.vertices=vertices;mesh.normals=tangents;mesh.uv=uv;mesh.colors=colors;
            var indices=new int[(Rows-1)*12];int at=0;
            for(int arm=0;arm<2;arm++)for(int row=0;row<Rows-1;row++){int v=arm*Rows*2+row*2;indices[at++]=v;indices[at++]=v+1;indices[at++]=v+2;indices[at++]=v+1;indices[at++]=v+3;indices[at++]=v+2;}
            mesh.triangles=indices;art.AddComponent<MeshFilter>().sharedMesh=mesh;view=art.AddComponent<MeshRenderer>();view.sharedMaterial=shared;view.shadowCastingMode=ShadowCastingMode.Off;view.receiveShadows=false;view.lightProbeUsage=LightProbeUsage.Off;view.reflectionProbeUsage=ReflectionProbeUsage.Off;view.enabled=false;
        }
        void LateUpdate()
        {
            Vector3 a,b,velocity;bool emitting;float now=Time.unscaledTime;
            if(appearance==null||!appearance.ReadFlightTrailPose(out a,out b,out velocity,out emitting)){ResetHistory();return;}
            if(haveRoot&&(Vector3.Distance(previousRoot,transform.position)>TeleportDistance||now-previousTime>MaxFrameGap))ResetHistory();
            previousRoot=transform.position;previousTime=now;haveRoot=true;
            emissionBlend=Mathf.MoveTowards(emissionBlend,emitting?1:0,Time.unscaledDeltaTime/.2f);
            float strength=emissionBlend*Mathf.InverseLerp(StartSpeed,FullSpeed,velocity.magnitude);
            while(count>0&&now-history[(cursor-count+History)%History].Time>=Lifetime)count--;
            Stamp head=new Stamp{Left=a,Right=b,Time=now,Strength=strength};
            if(strength>0&&now>=nextSample){history[cursor]=head;cursor=(cursor+1)%History;count=Mathf.Min(count+1,History);nextSample=now+SampleInterval;}
            if(count==0){if(view!=null)view.enabled=false;return;}
            EnsureMesh();if(strength<=0)head=history[(cursor-1+History)%History];
            Matrix4x4 local=art.transform.worldToLocalMatrix;
            for(int arm=0;arm<2;arm++)
            {
                Vector3 previous=arm==0?head.Left:head.Right;
                for(int row=0;row<Rows;row++)
                {
                    Stamp s=row==0?head:history[(cursor-Mathf.Min(row,count)+History)%History];
                    Vector3 point=arm==0?s.Left:s.Right;float life=Mathf.Clamp01(1-(now-s.Time)/Lifetime);
                    Vector3 next=row+1<count?(arm==0?history[(cursor-row-1+History)%History].Left:history[(cursor-row-1+History)%History].Right):point;
                    Vector3 tangent=previous-next;if(tangent.sqrMagnitude<.000001f)tangent=velocity.sqrMagnitude>.001f?velocity:transform.forward;
                    Vector3 p=local.MultiplyPoint3x4(point),n=local.MultiplyVector(tangent.normalized);int v=arm*Rows*2+row*2;
                    float taper=life*life,width=Mathf.Lerp(MinWidth,MaxWidth,s.Strength)*life*.5f;
                    Color color=new Color(1,1,1,row>count?0:HeadAlpha*s.Strength*taper);
                    vertices[v]=vertices[v+1]=p;tangents[v]=tangents[v+1]=n;uv[v]=new Vector2(-1,width);uv[v+1]=new Vector2(1,width);colors[v]=colors[v+1]=color;previous=point;
                }
            }
            mesh.vertices=vertices;mesh.normals=tangents;mesh.uv=uv;mesh.colors=colors;mesh.RecalculateBounds();var bounds=mesh.bounds;bounds.Expand(MaxWidth*2);mesh.bounds=bounds;view.enabled=true;
        }
        void ResetHistory(){count=cursor=0;nextSample=emissionBlend=0;haveRoot=false;if(view!=null)view.enabled=false;}
        void OnDisable(){ResetHistory();}
        void OnDestroy(){if(mesh!=null)Destroy(mesh);if(art!=null)Destroy(art);if(ownsMaterial&&--users==0){Destroy(shared);shared=null;}}
    }
}
#endif

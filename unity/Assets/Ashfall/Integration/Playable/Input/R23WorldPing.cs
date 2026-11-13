using System;
namespace Ashfall.Integration.Playable.Input
{
    // The production lifetime/scale rules are independently CPU executable.
    public sealed class R23WorldPingState
    {
        public const float Lifetime=20,NormalOffset=.035f,FontPixels=36,PixelSize=.001f;
        public float Remaining {get;private set;}
        public void Place(){Remaining=Lifetime;}
        public void Clear(){Remaining=0;}
        public void Tick(float seconds,bool valid)
        {if(!valid){Clear();return;}if(!float.IsNaN(seconds)&&!float.IsInfinity(seconds))Remaining=Math.Max(0,Remaining-Math.Max(0,seconds));}
        public static float DistanceScale(float distance)
        {return float.IsNaN(distance)||float.IsInfinity(distance)?.7f:Math.Max(.7f,Math.Min(6,distance*.12f));}
        public static bool WorldLayer(int layer){return layer==0||layer==8;}
    }
}
#if UNITY_6000_0_OR_NEWER
namespace Ashfall.Integration.Playable.Input
{
    using UnityEngine;
    using UnityEngine.Rendering;
    // Original world_ping.gd local support-hand marker. Root owns input edges,
    // session validity and map transitions; no inventory, damage or network writes.
    public sealed class R23WorldPing:MonoBehaviour
    {
        readonly R23WorldPingState state=new R23WorldPingState();
        Transform head,actor,marker;Mesh blueMesh,outlineMesh;Material blue,outline;
        bool initialized,valid;
        public bool Visible {get{return marker!=null&&marker.gameObject.activeSelf;}}
        public float Remaining {get{return state.Remaining;}}
        public Vector3 Position {get{return marker==null?Vector3.zero:marker.position;}}
        public Vector3 SurfacePoint {get;private set;}
        public Vector3 SurfaceNormal {get;private set;}
        public event Action<Vector3> Pinged;
        public void Initialize(Transform view,Transform ownerActor)
        {
            if(initialized)throw new InvalidOperationException("World ping already initialized");
            if(view==null||ownerActor==null)throw new ArgumentException("Head and actor required");
            head=view;actor=ownerActor;
            var shader=Shader.Find("Universal Render Pipeline/Unlit");if(shader==null)throw new InvalidOperationException("World ping requires existing URP Unlit");
            marker=new GameObject("Source local world ping ◇").transform;
            try
            {
                blue=Finish(shader,new Color(.1f,.48f,1));outline=Finish(shader,new Color(.005f,.01f,.025f));
                float radius=R23WorldPingState.FontPixels*R23WorldPingState.PixelSize*.5f;
                float inner=radius-R23WorldPingState.PixelSize*2.5f;
                float border=8*R23WorldPingState.PixelSize*Mathf.Sqrt(2);
                blueMesh=Diamond(radius,inner);outlineMesh=Diamond(radius+border,Mathf.Max(0,inner-border));
                Part("Source dark8px outline",outlineMesh,outline,.0005f);Part("Source blue hollow diamond",blueMesh,blue,0);
                initialized=true;ClearAll();
            }
            catch{if(marker!=null)Destroy(marker.gameObject);Release();throw;}
        }
        static Material Finish(Shader shader,Color color)
        {
            var material=new Material(shader);material.name="Source depth-tested world ping";
            material.SetColor("_BaseColor",color);material.SetFloat("_Cull",0);material.SetFloat("_ZWrite",0);
            if(material.HasProperty("_ZTest"))material.SetFloat("_ZTest",(float)CompareFunction.LessEqual);
            material.renderQueue=2010;return material;
        }
        void Part(string label,Mesh mesh,Material material,float z)
        {
            var go=new GameObject(label);go.transform.SetParent(marker,false);go.transform.localPosition=new Vector3(0,0,z);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        static Mesh Diamond(float outer,float inner)
        {
            var vertices=new Vector3[8];var triangles=new int[24];
            for(int i=0;i<4;i++)
            {
                float angle=i*Mathf.PI*.5f;var axis=new Vector3(Mathf.Sin(angle),Mathf.Cos(angle),0);vertices[i]=axis*outer;vertices[i+4]=axis*inner;
                int next=(i+1)%4,o=i*6;triangles[o]=i;triangles[o+1]=next;triangles[o+2]=next+4;triangles[o+3]=i;triangles[o+4]=next+4;triangles[o+5]=i+4;
            }
            var mesh=new Mesh{name="Source hollow diamond ping contour"};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateBounds();return mesh;
        }
        public bool TryPlace(Ray ray)
        {
            Require();if(!valid||!isActiveAndEnabled||!Finite(ray.origin)||!Finite(ray.direction)||ray.direction.sqrMagnitude<.000001f)return false;
            var camera=head.GetComponent<Camera>();float distance=Mathf.Max(100000,camera==null?0:camera.farClipPlane*2);
            RaycastHit nearest=new RaycastHit();float closest=float.PositiveInfinity;bool found=false,backfaces=Physics.queriesHitBackfaces;
            try
            {
                Physics.SyncTransforms();
                Physics.queriesHitBackfaces=true;
                foreach(var hit in Physics.RaycastAll(ray.origin,ray.direction.normalized,distance,(1<<0)|(1<<8),QueryTriggerInteraction.Ignore))
                {
                    var collider=hit.collider;if(collider==null||!R23WorldPingState.WorldLayer(collider.gameObject.layer)||collider.transform==actor||collider.transform.IsChildOf(actor))continue;
                    if(hit.distance<closest){nearest=hit;closest=hit.distance;found=true;}
                }
            }
            finally{Physics.queriesHitBackfaces=backfaces;}
            if(!found)return false;
            SurfacePoint=nearest.point;SurfaceNormal=nearest.normal;
            marker.position=nearest.point+nearest.normal*R23WorldPingState.NormalOffset;state.Place();UpdateMarker();
            if(Pinged!=null)Pinged(SurfacePoint);return true;
        }
        public void Tick(float seconds,bool inputValid)
        {Require();valid=inputValid&&head!=null&&actor!=null&&isActiveAndEnabled;state.Tick(seconds,valid);UpdateMarker();}
        void UpdateMarker()
        {
            if(marker==null)return;marker.gameObject.SetActive(valid&&state.Remaining>0);if(!Visible)return;
            // upright_ui.gd keeps view pitch while removing headset roll.
            Vector3 forward=head.forward.normalized,right=Vector3.Cross(Vector3.up,forward);
            if(right.sqrMagnitude<.000001f)right=Vector3.right;else right.Normalize();
            marker.rotation=Quaternion.LookRotation(forward,Vector3.Cross(forward,right).normalized);
            marker.localScale=Vector3.one*.5f*R23WorldPingState.DistanceScale(Vector3.Distance(head.position,marker.position));
        }
        public void ClearAll(){state.Clear();valid=false;SurfacePoint=SurfaceNormal=Vector3.zero;if(marker!=null)marker.gameObject.SetActive(false);}
        void Require(){if(!initialized)throw new InvalidOperationException("Initialize world ping first");}
        static bool Finite(Vector3 p){return !(float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.x)||float.IsInfinity(p.y)||float.IsInfinity(p.z));}
        void Release(){if(blueMesh!=null)Destroy(blueMesh);if(outlineMesh!=null)Destroy(outlineMesh);if(blue!=null)Destroy(blue);if(outline!=null)Destroy(outline);}
        void OnDisable(){ClearAll();}
        void OnApplicationFocus(bool focused){if(!focused)ClearAll();}
        void OnApplicationPause(bool paused){if(paused)ClearAll();}
        void OnDestroy(){ClearAll();if(marker!=null)Destroy(marker.gameObject);Release();}
    }
}
#endif

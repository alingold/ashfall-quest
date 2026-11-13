#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Climbing;
using UnityEngine;
namespace Ashfall.Integration.Playable.Climbing
{
    // The source's 168-point flat-bottom goggle hull. Queries move only this hidden
    // proxy; the rig remains the sole publisher of accepted root translations.
    public sealed class R23HeadCollision:MonoBehaviour
    {
        public const float FloorNormal=.42261826f,Margin=.001f;
        Transform player,head;Rigidbody proxy;MeshCollider hull;Mesh mesh;
        readonly Collider[] overlaps=new Collider[256];
        public bool Ready {get{return proxy!=null&&hull!=null&&player!=null&&head!=null;}}
        public void Initialize(Transform playerRoot,Transform trackedHead)
        {
            if(player!=null)throw new InvalidOperationException("head collision already initialized");
            if(playerRoot==null||trackedHead==null)throw new ArgumentException("live root and tracked head required");
            player=playerRoot;head=trackedHead;mesh=CreateSourceMesh();
            var node=new GameObject("R23 hidden source goggle sweep");node.layer=2;node.transform.SetParent(transform,false);
            proxy=node.AddComponent<Rigidbody>();proxy.isKinematic=true;proxy.useGravity=false;
            hull=node.AddComponent<MeshCollider>();hull.sharedMesh=mesh;hull.convex=true;hull.contactOffset=Margin;hull.enabled=false;
        }
        public static Mesh CreateSourceMesh()
        {
            var source=SourceHeadConvex.Instance.CopyVertices();var vertices=new Vector3[source.Length];
            for(int i=0;i<source.Length;i++)vertices[i]=U(source[i]);
            var triangles=new List<int>();
            for(int ring=0;ring<6;ring++)for(int segment=0;segment<24;segment++)
            {int a=ring*24+segment,b=ring*24+(segment+1)%24,c=a+24,d=b+24;triangles.Add(a);triangles.Add(b);triangles.Add(c);if(ring<5){triangles.Add(b);triangles.Add(d);triangles.Add(c);}}
            for(int i=1;i<23;i++){triangles.Add(0);triangles.Add(i+1);triangles.Add(i);}
            var result=new Mesh();result.name="R23 exact 168-vertex goggle hull";result.vertices=vertices;result.triangles=triangles.ToArray();result.RecalculateBounds();return result;
        }
        Func<Collider,bool> sourceGhostActorFilter;
        public void SetSourceGhostActorFilter(Func<Collider,bool> actualActorLayer4){sourceGhostActorFilter=actualActorLayer4;}
        bool Relevant(Collider c)
        {return c!=null&&c.gameObject.layer!=19&&c.gameObject.layer!=22&&c!=hull&&(sourceGhostActorFilter==null||!sourceGhostActorFilter(c))&&c.enabled&&!c.isTrigger&&c.gameObject.activeInHierarchy&&c.transform!=player&&!c.transform.IsChildOf(player);}
        void Pose(Vector3 point)
        {hull.enabled=true;proxy.position=point;proxy.rotation=Quaternion.Euler(0,player.eulerAngles.y,0);Physics.SyncTransforms();}
        void EndQuery(){if(hull!=null)hull.enabled=false;}
        bool StartingClear(Vector3 point,Vector3 separatingMotion)
        {
            int count=Physics.OverlapSphereNonAlloc(point,.121f,overlaps,Physics.AllLayers,QueryTriggerInteraction.Ignore);
            if(count==overlaps.Length)return false;
            for(int i=0;i<count;i++)if(Relevant(overlaps[i]))
            {
                Vector3 away;float depth;var c=overlaps[i];
                if(Physics.ComputePenetration(hull,point,proxy.rotation,c,c.transform.position,c.transform.rotation,out away,out depth)&&depth>.0001f&&Vector3.Dot(separatingMotion,away)<=.000001f)return false;
            }
            return true;
        }
        public bool Clear(Vector3 point,Vector3 motion)
        {
            if(!Ready||!Finite(point)||!Finite(motion))return false;
            try
            {
                Pose(point);if(!StartingClear(point,motion))return false;
                if(motion.sqrMagnitude<.0000000001f)return true;
                RaycastHit hit;return !Nearest(point,motion,out hit)||hit.distance>=motion.magnitude-Margin;
            }
            finally{EndQuery();}
        }
        bool Nearest(Vector3 point,Vector3 motion,out RaycastHit closest)
        {
            Pose(point);closest=new RaycastHit();float nearest=float.PositiveInfinity,length=motion.magnitude;
            if(length<.000001f)return false;
            var hits=proxy.SweepTestAll(motion/length,length+Margin,QueryTriggerInteraction.Ignore);
            foreach(var hit in hits)if(Relevant(hit.collider)&&hit.distance<nearest)
            {
                // The source retries upward-facing lower-rim contacts while moving
                // away from support. Side walls and ceilings retain their blocking.
                if(hit.distance<=Margin&&Vector3.Dot(motion,hit.normal)>0)continue;
                if(Vector3.Dot(motion,hit.normal)>=-.000001f)continue;
                nearest=hit.distance;closest=hit;
            }
            return !float.IsPositiveInfinity(nearest);
        }
        public Vector3 Sweep(Vector3 point,Vector3 motion,bool slide,out bool ceiling)
        {
            ceiling=false;if(!Ready||!Finite(point)||!Finite(motion))return Vector3.zero;
            Vector3 total=Vector3.zero,remaining=motion;
            try
            {
                Pose(point);if(!StartingClear(point,motion))return Vector3.zero;
                for(int pass=0;pass<(slide?6:1);pass++)
                {
                    float length=remaining.magnitude;if(length<.000001f)break;RaycastHit hit;
                    if(!Nearest(point+total,remaining,out hit)){total+=remaining;break;}
                    Vector3 safe=remaining/length*Mathf.Max(0,Mathf.Min(length,hit.distance-Margin));total+=safe;remaining-=safe;
                    if(hit.normal.y<-.1f&&remaining.y>0)ceiling=true;
                    if(!slide)break;remaining=Vector3.ProjectOnPlane(remaining,hit.normal);
                }
                return total;
            }
            finally{EndQuery();}
        }
        public bool Ray(Vector3 from,Vector3 to,out RaycastHit closest)
        {return RayQuery(from,to,false,out closest);}
        bool RayQuery(Vector3 from,Vector3 to,bool footProbe,out RaycastHit closest)
        {
            closest=new RaycastHit();if(!Ready||!Finite(from)||!Finite(to))return false;
            float nearest=float.PositiveInfinity;Vector3 motion=to-from;float length=motion.magnitude;if(length<.000001f)return false;
            foreach(var hit in Physics.RaycastAll(from,motion/length,length,Physics.AllLayers,QueryTriggerInteraction.Ignore))
                if((Relevant(hit.collider)||footProbe&&hit.collider!=null&&hit.collider.gameObject.layer==22&&hit.collider.enabled&&!hit.collider.isTrigger&&hit.collider.gameObject.activeInHierarchy)&&hit.distance<nearest){nearest=hit.distance;closest=hit;}
            return !float.IsPositiveInfinity(nearest);
        }
        public bool Support(Vector3 feet,Vector3 eye,out RaycastHit floor)
        {return RayQuery(eye+Vector3.up*.015f,new Vector3(eye.x,Mathf.Min(feet.y-3,eye.y-3),eye.z),true,out floor)&&floor.point.y<=eye.y+.0025f&&floor.normal.y>=FloorNormal;}
        public Vector3 LandingMotion(Vector3 feet,Vector3 eye,Vector3 requested)
        {
            RaycastHit floor;if(requested.y<0&&Support(feet,eye,out floor))
            {float gap=floor.point.y-feet.y;if(gap<=.005f&&gap>=requested.y)requested.y=Mathf.Min(0,gap);}
            return requested;
        }
        // Source _resolve_support: sweep the head on elevation, not an invented
        // torso. A refused rise returns no correction and no supported state.
        public bool ResolveSupport(Vector3 feet,Vector3 eye,Vector3 velocity,float seconds,float maximumRise,bool allowDropSnap,out Vector3 correction)
        {
            correction=Vector3.zero;RaycastHit floor;if(!Support(feet,eye,out floor))return false;
            float rise=floor.point.y-feet.y,dropAllowance=.03f;
            if(floor.normal.y<.999f)dropAllowance+=Vector3.ProjectOnPlane(velocity,Vector3.up).magnitude*seconds*Mathf.Tan(65*Mathf.Deg2Rad);
            if(rise>maximumRise+.005f||(rise<-dropAllowance&&!allowDropSnap)||rise<-.27f||(rise<-.01f&&velocity.y>.05f))return false;
            if(Mathf.Abs(rise)<.0001f)return true;
            bool ceiling;Vector3 safe=Sweep(eye,Vector3.up*rise,false,out ceiling);
            if(Mathf.Abs(safe.y-rise)>.015f)return false;correction=safe;return true;
        }
        void OnDestroy(){if(proxy!=null)Destroy(proxy.gameObject);if(mesh!=null)Destroy(mesh);}
        static bool Finite(Vector3 value){return !Single.IsNaN(value.x)&&!Single.IsInfinity(value.x)&&!Single.IsNaN(value.y)&&!Single.IsInfinity(value.y)&&!Single.IsNaN(value.z)&&!Single.IsInfinity(value.z);}
        static Vector3 U(Float3 value){return new Vector3(value.X,value.Y,value.Z);}
    }
}
#endif

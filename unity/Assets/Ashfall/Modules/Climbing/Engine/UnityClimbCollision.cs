#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Core;
using UnityEngine;
namespace Ashfall.Modules.Climbing.Engine
{
    // Central semantic registry resolves source metadata; unknown grabs are refused.
    public interface IClimbSurfaceMetadata
    { bool TryResolve(Collider collider,out bool archiveGeometry,out bool authoredClimbable); }
    // EXPERIMENTAL BOX QUARANTINE: not a source-convex or production collider binding.
    public sealed class UnityClimbCollision : IClimbCollision
    {
        readonly ClimbCollisionAdapter adapter;
        public UnityClimbCollision(PhysicsScene scene,Transform actorRoot,IHeadBoxSource liveApprovedShape,
            CollisionLayers layers,IClimbSurfaceMetadata metadata)
        {
            if(!scene.IsValid()||actorRoot==null||metadata==null||!layers.Valid ||
                LayerMask.LayerToName(8)!="WorldStructure"||LayerMask.LayerToName(9)!="WorldInteraction"||
                LayerMask.LayerToName(10)!="PlayerActor"||LayerMask.LayerToName(11)!="WorldDecoration")
                throw new ArgumentException("Central collision allocation/actor/metadata required");
            adapter=new ClimbCollisionAdapter(new SceneQueries(scene,actorRoot,metadata),liveApprovedShape,layers);
        }
        public bool TryGrab(Float3 p,float r,out SurfaceHit h) { return adapter.TryGrab(p,r,out h); }
        public bool Raycast(Float3 a,Float3 b,QueryKind k,out SurfaceHit h) { return adapter.Raycast(a,b,k,out h); }
        public bool HeadMotionClear(Float3 h,Float3 d) { return adapter.HeadMotionClear(h,d); }
        public MotionResult SweepHead(Float3 h,Float3 d,float s,bool slide) { return adapter.SweepHead(h,d,s,slide); }
        sealed class SceneQueries:IClimbQueryScene
        {
            readonly PhysicsScene scene; readonly Transform actor; readonly IClimbSurfaceMetadata metadata;
            readonly Collider[] overlaps=new Collider[256]; readonly RaycastHit[] hits=new RaycastHit[256];
            public SceneQueries(PhysicsScene s,Transform a,IClimbSurfaceMetadata m) { scene=s;actor=a;metadata=m; }
            static Vector3 U(Float3 p) { return new Vector3(p.X,p.Y,p.Z); }
            static Float3 F(Vector3 p) { return new Float3(p.x,p.y,p.z); }
            bool Excluded(Collider c) { return c==null||c.transform==actor||c.transform.IsChildOf(actor); }
            static Quaternion Rotation(ClimbQuery q) { return new Quaternion(q.Shape.Qx,q.Shape.Qy,q.Shape.Qz,q.Shape.Qw); }
            public bool Overlap(ClimbQuery q,out bool blocked)
            {
                blocked=true; if(actor==null||!scene.IsValid()) return false; int count=scene.OverlapBox(U(q.Origin),U(q.Shape.HalfExtents),overlaps,Rotation(q),q.Mask,QueryTriggerInteraction.Ignore);
                if(count>=overlaps.Length) return false;
                for(int i=0;i<count;i++) if(!Excluded(overlaps[i])) return true;
                blocked=false; return true;
            }
            public bool Ray(ClimbQuery q,out bool hit,out ClimbQueryHit nearest)
            { if(actor==null||!scene.IsValid()) {hit=false;nearest=new ClimbQueryHit();return false;} int count=scene.Raycast(U(q.Origin),U(q.Motion).normalized,hits,U(q.Motion).magnitude,q.Mask,QueryTriggerInteraction.Ignore); return Select(count,out hit,out nearest); }
            public bool Cast(ClimbQuery q,out bool hit,out ClimbQueryHit nearest)
            { if(actor==null||!scene.IsValid()) {hit=false;nearest=new ClimbQueryHit();return false;} int count=scene.BoxCast(U(q.Origin),U(q.Shape.HalfExtents),U(q.Motion).normalized,hits,Rotation(q),U(q.Motion).magnitude,q.Mask,QueryTriggerInteraction.Ignore); return Select(count,out hit,out nearest); }
            bool Select(int count,out bool found,out ClimbQueryHit nearest)
            {
                found=false; nearest=new ClimbQueryHit(); if(count>=hits.Length) return false;
                for(int i=0;i<count;i++)
                {
                    var h=hits[i]; if(Excluded(h.collider)||found&&h.distance>=nearest.Distance) continue;
                    bool archive,climbable;
                    if(!metadata.TryResolve(h.collider,out archive,out climbable)) { archive=true;climbable=false; }
                    nearest=new ClimbQueryHit {Distance=h.distance,Surface=new SurfaceHit {Point=F(h.point),Normal=F(h.normal),ArchiveGeometry=archive,AuthoredClimbable=climbable}}; found=true;
                }
                return true;
            }
            public bool Grab(ClimbQuery q,out bool found,out ClimbQueryHit nearest)
            {
                found=false; nearest=new ClimbQueryHit();
                if(actor==null||!scene.IsValid()) return false;
                int count=scene.OverlapSphere(U(q.Origin),q.Radius,overlaps,q.Mask,QueryTriggerInteraction.Ignore);
                if(count>=overlaps.Length) return false;
                for(int i=0;i<count;i++)
                {
                    var c=overlaps[i]; if(Excluded(c)) continue; bool archive,climbable;
                    if(!metadata.TryResolve(c,out archive,out climbable)||archive&&!climbable) continue;
                    var p=c.ClosestPoint(U(q.Origin)); float distance=Vector3.Distance(p,U(q.Origin));
                    if(found&&distance>=nearest.Distance) continue;
                    // Contact overlap can be interior; a normal is not used by XR grab eligibility.
                    nearest=new ClimbQueryHit {Distance=distance,Surface=new SurfaceHit {Point=F(p),Normal=F((U(q.Origin)-p).sqrMagnitude>1e-10f?(U(q.Origin)-p).normalized:Vector3.up),ArchiveGeometry=archive,AuthoredClimbable=climbable}}; found=true;
                }
                return true;
            }
        }
    }
}
#endif

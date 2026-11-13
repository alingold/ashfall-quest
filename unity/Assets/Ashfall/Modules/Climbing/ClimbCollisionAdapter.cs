using System;
using Ashfall.Core;
namespace Ashfall.Modules.Climbing
{
    // EXPERIMENTAL BOX QUARANTINE: no source-convex equivalence or production approval.
    // Read-only geometry translation and bounded swept/slide solver. Never moves a Transform.
    public sealed class ClimbCollisionAdapter : IClimbCollision
    {
        readonly IClimbQueryScene scene; readonly IHeadBoxSource shapes; readonly CollisionLayers layers;
        const float Skin=.001f;
        public ClimbCollisionAdapter(IClimbQueryScene scene,IHeadBoxSource shapes,CollisionLayers layers)
        {
            if(scene==null||shapes==null||!layers.Valid) throw new ArgumentException("Unbound climb geometry/layers");
            this.scene=scene; this.shapes=shapes; this.layers=layers;
        }
        bool Query(Float3 head,Float3 motion,out ClimbQuery q)
        {
            q=new ClimbQuery(); HeadBox b;
            if(!V.WorldPoint(head)||!motion.IsFinite||V.Length(motion)>4||!shapes.TryRead(out b)||!b.Approved||
               !V.Within(b.CenterOffset,new Float3(),1)||!b.HalfExtents.IsFinite||
               b.HalfExtents.X<=0||b.HalfExtents.Y<=0||b.HalfExtents.Z<=0||V.Length(b.HalfExtents)>1) return false;
            double norm=(double)b.Qx*b.Qx+(double)b.Qy*b.Qy+(double)b.Qz*b.Qz+(double)b.Qw*b.Qw;
            if(double.IsNaN(norm)||double.IsInfinity(norm)||Math.Abs(norm-1)>.0001) return false;
            q=new ClimbQuery {Origin=V.Add(head,b.CenterOffset),Motion=motion,Shape=b,Mask=layers.HeadMask}; return true;
        }
        static bool Valid(ClimbQueryHit h,float length)
        { return V.Finite(h.Distance)&&h.Distance>=0&&h.Distance<=length&&h.Surface.Point.IsFinite&&h.Surface.Normal.IsFinite&&Math.Abs(V.Length(h.Surface.Normal)-1)<.01f; }
        public bool Raycast(Float3 from,Float3 to,QueryKind kind,out SurfaceHit hit)
        {
            hit=new SurfaceHit(); if(!V.WorldPoint(from)||!V.WorldPoint(to)|| (kind!=QueryKind.Structure&&kind!=QueryKind.Support)) return false;
            var q=new ClimbQuery {Origin=from,Motion=V.Sub(to,from),Mask=kind==QueryKind.Structure?layers.StructureMask:layers.SupportMask};
            float length=V.Length(q.Motion); bool found; ClimbQueryHit h;
            if(length<=0||length>10||!scene.Ray(q,out found,out h)||!found||!Valid(h,length)) return false;
            hit=h.Surface; return true;
        }
        public bool TryGrab(Float3 point,float radius,out SurfaceHit hit)
        {
            hit=new SurfaceHit(); bool found; ClimbQueryHit h;
            if(!V.WorldPoint(point)||!V.Finite(radius)||radius<=0||radius>.18f) return false;
            var q=new ClimbQuery {Origin=point,Radius=radius,Mask=layers.StructureMask};
            if(!scene.Grab(q,out found,out h)||!found||!Valid(h,radius)||h.Surface.ArchiveGeometry&&!h.Surface.AuthoredClimbable) return false;
            hit=h.Surface; return true;
        }
        public bool HeadMotionClear(Float3 head,Float3 motion)
        {
            ClimbQuery q; bool blocked;
            if(!Query(head,motion,out q)||!scene.Overlap(q,out blocked)||blocked) return false;
            var total=new Float3();var remaining=motion;
            for(int retry=0;retry<=4;retry++)
            {
                float length=V.Length(remaining);if(length<.000001f)return true;
                bool found;ClimbQueryHit h;
                if(!Query(V.Add(head,total),remaining,out q)||!scene.Overlap(q,out blocked)||blocked||!scene.Cast(q,out found,out h))return false;
                if(!found)return true;if(!Valid(h,length))return false;
                var safe=V.Mul(remaining,Math.Max(0,h.Distance-Skin)/length);
                total=V.Add(total,safe);remaining=V.Sub(remaining,safe);
                if(V.Length(remaining)<=.001f)return true;
                if(motion.Y<=0||h.Surface.Normal.Y<.70710678f)return false;
            }
            return false;
        }
        public MotionResult SweepHead(Float3 head,Float3 motion,float seconds,bool slide)
        {
            Float3 total=new Float3(), remaining=motion; ClimbQuery q; bool blocked;
            if(!V.Finite(seconds)||seconds<=0||seconds>1||!Query(head,motion,out q)||!scene.Overlap(q,out blocked)||blocked) return new MotionResult();
            for(int i=0;i<(slide?4:1);i++)
            {
                float length=V.Length(remaining); if(length<.000001f) break;
                if(!Query(V.Add(head,total),remaining,out q)) break;
                bool found; ClimbQueryHit h; if(!scene.Cast(q,out found,out h)) break;
                // Same source bounded retry semantics; every retry still queries all geometry.
                // This does not authorize ignoring a floor or changing the box binding.
                for(int recovery=0;recovery<4&&found&&Valid(h,length)&&motion.Y>0&&h.Surface.Normal.Y>=.70710678f;recovery++)
                {
                    var progress=V.Mul(remaining,Math.Max(0,h.Distance-Skin)/length);
                    total=V.Add(total,progress);remaining=V.Sub(remaining,progress);length=V.Length(remaining);
                    if(length<.0001f) break;
                    if(!Query(V.Add(head,total),remaining,out q)||!scene.Overlap(q,out blocked)||blocked||!scene.Cast(q,out found,out h))
                        return Result(total,seconds);
                }
                if(!found) { total=V.Add(total,remaining); break; }
                if(!Valid(h,length)) break;
                Float3 safe=V.Mul(remaining,Math.Max(0,h.Distance-Skin)/length); total=V.Add(total,safe);
                if(!slide) break;
                remaining=V.Sub(remaining,safe); var n=h.Surface.Normal;
                float into=remaining.X*n.X+remaining.Y*n.Y+remaining.Z*n.Z;
                if(into>=0) break; remaining=V.Sub(remaining,V.Mul(n,into));
            }
            return Result(total,seconds);
        }
        static MotionResult Result(Float3 total,float seconds)
        {
            Float3 velocity=new Float3((float)((double)total.X/seconds),(float)((double)total.Y/seconds),(float)((double)total.Z/seconds));
            return velocity.IsFinite ? new MotionResult(total,velocity) : new MotionResult();
        }
    }
}

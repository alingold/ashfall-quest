using System;
using Ashfall.Core;
namespace Ashfall.Modules.Climbing
{
    // Usable source-convex candidate port; no engine binding or pose/authority writes.
    public sealed class ConvexClimbCollision : IClimbCollision
    {
        public const int RecoveryRetries=4;
        public const float RecoveryRest=.0001f, MantleTolerance=.001f, SupportTolerance=.015f;
        readonly IConvexClimbScene scene;readonly IConvexHeadSource source;readonly CollisionLayers layers;
        readonly float floor;
        public ConvexClimbCollision(IConvexClimbScene scene,IConvexHeadSource source,CollisionLayers layers,float floorNormalY)
        {
            if(scene==null||source==null||!layers.Valid||!V.Finite(floorNormalY)||floorNormalY<=0||floorNormalY>1)
                throw new ArgumentException("Unbound convex climb scene/pose/layers/floor");
            this.scene=scene;this.source=source;this.layers=layers;floor=floorNormalY;
        }
        static bool Valid(ClimbQueryHit h,float length)
        { return V.Finite(h.Distance)&&h.Distance>=0&&h.Distance<=length&&V.WorldPoint(h.Surface.Point)&&h.Surface.Normal.IsFinite&&Math.Abs(V.Length(h.Surface.Normal)-1)<.01; }
        bool Stable(long revision,ConvexHeadPose p)
        {
            ConvexHeadPose now;
            return scene.Revision==revision&&source.TryRead(out now)&&SourceHeadConvex.ValidPose(now)&&
                now.WorldOffset.X==p.WorldOffset.X&&now.WorldOffset.Y==p.WorldOffset.Y&&now.WorldOffset.Z==p.WorldOffset.Z&&
                now.Qx==p.Qx&&now.Qy==p.Qy&&now.Qz==p.Qz&&now.Qw==p.Qw;
        }
        public ConvexSweepResult Solve(Float3 head,Float3 motion,bool slide,float tolerance)
        {
            var result=new ConvexSweepResult();ConvexHeadPose pose;
            if(!V.WorldPoint(head)||!motion.IsFinite||V.Length(motion)>4||!V.Finite(tolerance)||
                (tolerance!=MantleTolerance&&tolerance!=SupportTolerance)||!source.TryRead(out pose)||!SourceHeadConvex.ValidPose(pose))return result;
            long revision=scene.Revision;
            var q=new ConvexHeadQuery{Shape=SourceHeadConvex.Instance,Pose=pose,Origin=V.Add(head,pose.WorldOffset),Motion=motion,Mask=layers.HeadMask};
            bool penetrating;
            if(!V.WorldPoint(q.Origin)||!scene.Penetrating(q,out penetrating)||penetrating||!Stable(revision,pose))return result;
            result.Available=true;var remaining=motion;
            for(int segment=0;segment<(slide?4:1);segment++)
            {
                bool contact=false;var normal=new Float3();
                for(int retry=0;retry<=RecoveryRetries;retry++)
                {
                    float length=V.Length(remaining);
                    if(length<.000001f)break;
                    q.Origin=V.Add(V.Add(head,pose.WorldOffset),result.Displacement);q.Motion=remaining;q.Recovery=retry>0;
                    bool hit;ClimbQueryHit h;
                    if(!scene.Penetrating(q,out penetrating)||penetrating||!Stable(revision,pose)||
                        !scene.Cast(q,out hit,out h)||!Stable(revision,pose)) {result.Available=false;return result;}
                    result.Casts++;
                    if(!hit){result.Displacement=V.Add(result.Displacement,remaining);remaining=new Float3();contact=false;break;}
                    if(!Valid(h,length)){result.Available=false;return result;}
                    var safe=V.Mul(remaining,h.Distance/length);
                    result.Displacement=V.Add(result.Displacement,safe);remaining=V.Sub(remaining,safe);
                    normal=h.Surface.Normal;contact=true;
                    // Only retry an upward motion against a walkable floor; never slide through it.
                    if(motion.Y<=0||normal.Y<floor||V.Length(remaining)<RecoveryRest)break;
                }
                if(!contact||!slide)break;
                float into=remaining.X*normal.X+remaining.Y*normal.Y+remaining.Z*normal.Z;
                if(into>=0)break;
                remaining=V.Sub(remaining,V.Mul(normal,into));
            }
            result.Complete=Stable(revision,pose)&&V.Length(V.Sub(motion,result.Displacement))<=tolerance;
            return result;
        }
        public bool HeadMotionClear(Float3 head,Float3 motion)
        { var r=Solve(head,motion,false,MantleTolerance);return r.Available&&r.Complete; }
        public MotionResult SweepHead(Float3 head,Float3 motion,float seconds,bool slide)
        {
            if(!V.Finite(seconds)||seconds<=0||seconds>1)return new MotionResult();
            var r=Solve(head,motion,slide,MantleTolerance);
            var velocity=new Float3((float)((double)r.Displacement.X/seconds),(float)((double)r.Displacement.Y/seconds),(float)((double)r.Displacement.Z/seconds));
            return velocity.IsFinite?new MotionResult(r.Displacement,velocity):new MotionResult();
        }
        public bool Raycast(Float3 from,Float3 to,QueryKind kind,out SurfaceHit hit)
        {
            hit=new SurfaceHit();if(!V.WorldPoint(from)||!V.WorldPoint(to)||(kind!=QueryKind.Structure&&kind!=QueryKind.Support))return false;
            var q=new ClimbQuery{Origin=from,Motion=V.Sub(to,from),Mask=kind==QueryKind.Structure?layers.StructureMask:layers.SupportMask};
            float length=V.Length(q.Motion);bool found;ClimbQueryHit h;long rev=scene.Revision;
            if(length<=0||length>10||!scene.Ray(q,out found,out h)||scene.Revision!=rev||!found||!Valid(h,length))return false;
            hit=h.Surface;return true;
        }
        public bool TryGrab(Float3 point,float radius,out SurfaceHit hit)
        {
            hit=new SurfaceHit();if(!V.WorldPoint(point)||!V.Finite(radius)||radius<=0||radius>.18f)return false;
            bool found;ClimbQueryHit h;long rev=scene.Revision;
            if(!scene.Grab(new ClimbQuery{Origin=point,Radius=radius,Mask=layers.StructureMask},out found,out h)||
                scene.Revision!=rev||!found||!Valid(h,radius)||h.Surface.ArchiveGeometry&&!h.Surface.AuthoredClimbable)return false;
            hit=h.Surface;return true;
        }
    }
}

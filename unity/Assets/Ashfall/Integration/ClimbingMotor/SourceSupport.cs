using System;
using Ashfall.Core;
using Ashfall.Modules.Climbing;
namespace Ashfall.Integration.ClimbingMotor
{
    public sealed class SourceSupport:IClimbSupport
    {
        readonly ConvexClimbCollision collision;readonly float floor;
        public SourceSupport(ConvexClimbCollision collision,float floorNormalY)
        {if(collision==null||floorNormalY<=0||floorNormalY>1||Single.IsNaN(floorNormalY))throw new ArgumentException("support binding");this.collision=collision;floor=floorNormalY;}
        public bool TryComplete(Float3 root,Float3 head,Float3 proposed,Float3 velocity,float seconds,out Float3 completion)
        {
            completion=new Float3();root=Add(root,proposed);head=Add(head,proposed);
            if(!velocity.IsFinite||Single.IsNaN(seconds)||seconds<=0||seconds>.1f)return false;
            SurfaceHit hit;
            if(!collision.Raycast(Add(head,new Float3(0,.015f,0)),new Float3(head.X,Math.Min(root.Y-3,head.Y-3),head.Z),QueryKind.Support,out hit)||
                hit.Normal.Y<floor||hit.Point.Y>head.Y+.0025f)return false;
            float rise=hit.Point.Y-root.Y;
            double allowance=.03;
            if(hit.Normal.Y<.999f)allowance+=Math.Sqrt((double)velocity.X*velocity.X+(double)velocity.Z*velocity.Z)*seconds*Math.Sqrt(1-floor*floor)/floor;
            // Source final mantle completion: _resolve_support(.02,false).
            if(rise<-.27f||rise> .025f||rise< -allowance||(rise<-.01f&&velocity.Y>.05f))return false;
            var solved=collision.Solve(head,new Float3(0,rise,0),false,ConvexClimbCollision.SupportTolerance);
            if(!solved.Available||!solved.Complete)return false;completion=solved.Displacement;return true;
        }
        static Float3 Add(Float3 a,Float3 b){return new Float3(a.X+b.X,a.Y+b.Y,a.Z+b.Z);}
    }
}

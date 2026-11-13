using System;
namespace Ashfall.Modules.Climbing
{
    // Trusted read-only host port. Distance is a LOWER bound from the entire
    // convex hull of the 168 published vertices to ALL filtered world geometry.
    // No hit/closest-vertex/endpoint boolean can implement this contract.
    // Include native representation, cooking and arithmetic error in the bound.
    // false includes unsupported nonconvex geometry, stale bindings and overflow.
    public interface ISourceHullDistance
    {
        bool Approved {get;} long Revision {get;}
        bool TryLowerBound(SourceHeadTrajectory trajectory,double phase,out double metres);
    }
    public sealed class ContinuousHeadClearance
    {
        readonly ISourceHullDistance query;
        public ContinuousHeadClearance(ISourceHullDistance query){if(query==null)throw new ArgumentNullException("query");this.query=query;}
        public bool Clear(SourceHeadTrajectory path,double margin)
        {
            if(path==null||Double.IsNaN(margin)||Double.IsInfinity(margin)||margin<0||margin>.1||!query.Approved)return false;
            long revision=query.Revision;int budget=8192;
            // Split at the piecewise translation/yaw join. Every accepted interval
            // has a whole-hull distance certificate covering its interior.
            bool clear=Interval(path,0,1,margin,revision,0,ref budget)&&Interval(path,1,2,margin,revision,0,ref budget);
            return clear&&query.Approved&&query.Revision==revision;
        }
        bool Interval(SourceHeadTrajectory path,double a,double b,double margin,long revision,int depth,ref int budget)
        {
            if(--budget<0||!query.Approved||query.Revision!=revision)return false;
            double m=(a+b)*.5,d;
            if(!query.TryLowerBound(path,m,out d)||Double.IsNaN(d)||Double.IsInfinity(d)||d<0||query.Revision!=revision)return false;
            double travel=Math.Max(path.TravelBound(a,m),path.TravelBound(m,b));
            if(d>margin+travel)return true;
            if(depth>=20||d<=margin)return false;
            return Interval(path,a,m,margin,revision,depth+1,ref budget)&&Interval(path,m,b,margin,revision,depth+1,ref budget);
        }
    }
}

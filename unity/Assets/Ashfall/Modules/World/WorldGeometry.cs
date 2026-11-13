using System;
using System.Collections.Generic;
using Ashfall.Core;
namespace Ashfall.Modules.World
{
    // Deterministic segment/triangle and authored-volume queries over an immutable plan snapshot.
    // This validates exported geometry; it is not Unity physics cooking or actor authority.
    public sealed class WorldGeometry
    {
        readonly LoadedMap map; readonly SemanticRegistry registry; readonly WorldHitRouting routing;
        public WorldGeometry(WorldBuildPlan plan) { if(plan==null)throw new ArgumentNullException("plan");map=plan.Snapshot();registry=plan.Registry();routing=new WorldHitRouting(plan);foreach(var s in map.Surfaces)foreach(var v in s.Vertices)if(Math.Abs(v.X)>1000000||Math.Abs(v.Y)>1000000||Math.Abs(v.Z)>1000000)throw new System.IO.InvalidDataException("Geometry query coordinate domain exceeded: "+s.Id); }
        static Float3 Sub(Float3 a,Float3 b){return new Float3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);}
        static double Dot(Float3 a,Float3 b){return (double)a.X*b.X+(double)a.Y*b.Y+(double)a.Z*b.Z;}
        static Float3 Cross(Float3 a,Float3 b){return new Float3(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);}
        static bool Triangle(Float3 origin,Float3 delta,Float3 a,Float3 b,Float3 c,out double t)
        {
            t=0;var e=Sub(b,a);var f=Sub(c,a);var p=Cross(delta,f);var det=Dot(e,p);if(Math.Abs(det)<1e-12)return false;
            var s=Sub(origin,a);var u=Dot(s,p)/det;if(u<0||u>1)return false;var q=Cross(s,e);var v=Dot(delta,q)/det;if(v<0||u+v>1)return false;t=Dot(f,q)/det;return t>=0&&t<=1;
        }
        static bool Box(Float3 origin,Float3 delta,AuthoredBox b,out double hit)
        {
            double lo=0,hi=1;float[] o={origin.X,origin.Y,origin.Z},d={delta.X,delta.Y,delta.Z};
            for(int i=0;i<3;i++){double min=b.center[i]-(double)b.size[i]/2,max=b.center[i]+(double)b.size[i]/2;if(d[i]==0){if(o[i]<min||o[i]>max){hit=0;return false;}continue;}double a=(min-o[i])/d[i],z=(max-o[i])/d[i];if(a>z){double tmp=a;a=z;z=tmp;}lo=Math.Max(lo,a);hi=Math.Min(hi,z);if(lo>hi){hit=0;return false;}}
            hit=lo;return true;
        }
        public bool HasLineOfSight(string owner,Float3 origin,Float3 target,float range)
        {
            return Query(owner,origin,target,range,null,null);
        }
        internal bool Query(string owner,Float3 origin,Float3 target,float range,string actor,WorldQueryHit[] external)
        {
            if(String.IsNullOrWhiteSpace(owner)||!origin.IsFinite||!target.IsFinite||Math.Abs(origin.X)>1000000||Math.Abs(origin.Y)>1000000||Math.Abs(origin.Z)>1000000||Math.Abs(target.X)>1000000||Math.Abs(target.Y)>1000000||Math.Abs(target.Z)>1000000||Single.IsNaN(range)||Single.IsInfinity(range)||range<=0)return false;
            var delta=Sub(target,origin);double squared=Dot(delta,delta);if(squared<=0||squared>(double)range*range)return false;
            double length=Math.Sqrt(squared);var hits=new List<WorldNearestHit>();
            foreach(var s in map.Surfaces)
            {
                var role=registry.Resolve(s.Id);if(role.kind=="decoration")continue;
                if(role.kind=="structure")for(int i=0;i<s.Indices.Length;i+=3){double t;if(Triangle(origin,delta,s.Vertices[s.Indices[i]],s.Vertices[s.Indices[i+1]],s.Vertices[s.Indices[i+2]],out t))hits.Add(new WorldNearestHit(t*length,null));}
                else foreach(var b in role.boxes){double t;if(Box(origin,delta,b,out t))hits.Add(new WorldNearestHit(t*length,role.kind=="interaction"?role.ownerId:null));}
            }
            foreach(var pad in map.Pads){var role=registry.Resolve(pad.Id);foreach(var b in role.boxes){double t;if(Box(origin,delta,b,out t))hits.Add(new WorldNearestHit(t*length,role.ownerId));}}
            if(external!=null && !routing.Append(actor,(float)length,external,hits))return false;
            return WorldNearestPolicy.Allows(owner,hits);
        }
        public bool TriggerContains(string sourceId,string owner,Float3 point)
        {
            if(!point.IsFinite||!registry.Route(sourceId,owner,true))return false;
            foreach(var box in registry.Resolve(sourceId).boxes){double t;if(Box(point,new Float3(0,0,0),box,out t))return true;}return false;
        }
    }
}

using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Construction;

namespace Ashfall.Integration.ConstructionGeometry
{
    // Finite triangle-soup candidate, NOT a Unity physics adapter or content approval.
    // Coordinates/winding must already be converted exactly once by the importer.
    public sealed class StructuralWorld : IConstructionWorld
    {
        private readonly SourceTriangle[] surfaces;
        private readonly IConstructionOccupants occupants;
        private readonly bool archive;
        private readonly FloorBurialPolicy floorPolicy;
        public StructuralWorld(CollisionAllocation allocation,IEnumerable<SourceTriangle> input,IConstructionOccupants occupants,bool archive,FloorBurialPolicy floorPolicy)
        {
            if(allocation==null||input==null||occupants==null)throw new ArgumentException("geometry_dependencies");
            if(floorPolicy!=FloorBurialPolicy.LocalAllSamples025&&floorPolicy!=FloorBurialPolicy.RemoteAnySamples150)throw new ArgumentException("floor_policy");
            this.occupants=occupants;this.archive=archive;this.floorPolicy=floorPolicy;
            var list=new List<SourceTriangle>();var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(SourceTriangle t in input)
            {
                if(t==null||String.IsNullOrEmpty(t.SourceId)||String.IsNullOrEmpty(t.OwnerId)||String.IsNullOrEmpty(t.BodyId)||!Hash(t.SourceHash)||!ids.Add(t.SourceId))throw new ArgumentException("source_identity");
                if(!Enum.IsDefined(typeof(SurfaceRole),t.Role)||t.Role==SurfaceRole.Unknown)throw new ArgumentException("unclassified:"+t.SourceId);
                int layer=t.Role==SurfaceRole.Decoration?11:t.Role==SurfaceRole.Loot?9:8;
                if(t.Layer!=layer)throw new ArgumentException("source_layer:"+t.SourceId);
                if(!Point(t.A)||!Point(t.B)||!Point(t.C)||Dot(Cross(Sub(t.B,t.A),Sub(t.C,t.A)),Cross(Sub(t.B,t.A),Sub(t.C,t.A)))<1e-16)throw new ArgumentException("triangle:"+t.SourceId);
                list.Add(t);if(list.Count>100000)throw new ArgumentException("surface_budget");
            }
            surfaces=list.ToArray();
        }
        private static bool Hash(string s){if(s==null||s.Length!=64)return false;foreach(char c in s)if(!Uri.IsHexDigit(c))return false;return true;}
        private static bool Point(Float3 p){return p.IsFinite&&Math.Abs(p.X)<=100000&&Math.Abs(p.Y)<=100000&&Math.Abs(p.Z)<=100000;}
        private static bool Static(SourceTriangle t){return t.Role==SurfaceRole.Structure||t.Role==SurfaceRole.TerrainSurface||t.Role==SurfaceRole.ConstructionTerrain;}
        private static void PanelValid(Panel p){if(p==null||!Point(p.Center)||p.Axis<0||p.Axis>2||(p.Kind!="wall"&&p.Kind!="floor")||(p.Kind=="floor"&&p.Axis!=1)||(p.Kind=="wall"&&p.Axis==1))throw new ArgumentException("panel");}
        public bool HasStaticContact(Panel p,float tolerance)
        {
            PanelValid(p);if(float.IsNaN(tolerance)||tolerance<0||tolerance>.1f)throw new ArgumentException("contact_tolerance");
            Float3 h=Add(Geometry.HalfSize(p),new Float3(tolerance,tolerance,tolerance));
            foreach(var t in surfaces)if(Static(t)&&Intersects(t,p.Center,h))return true;
            return false;
        }
        public string Clearance(Panel p,string actorId)
        {
            PanelValid(p);ProtectedBox[] players;Float3[] objectives;
            if(!Snapshot(out players,out objectives))return "clearance_unavailable";
            foreach(var b in players)if(Overlap(p.Center,Geometry.HalfSize(p),b.Center,b.HalfSize))return "player_clearance";
            foreach(var v in objectives)if(Geometry.Contains(p,v,.4f))return "objective_clearance";
            return "";
        }
        private bool Snapshot(out ProtectedBox[] players,out Float3[] objectives)
        {
            if(!occupants.TrySnapshot(out players,out objectives)||players==null||objectives==null)return false;
            foreach(var b in players)if(b==null||String.IsNullOrEmpty(b.Id)||!Point(b.Center)||!Point(b.HalfSize)||b.HalfSize.X<=0||b.HalfSize.Y<=0||b.HalfSize.Z<=0)return false;
            foreach(var v in objectives)if(!Point(v))return false;
            return true;
        }
        public bool HasLineOfSight(string actorId,Float3 origin,Float3 target)
        {
            if(String.IsNullOrEmpty(actorId)||!Point(origin)||!Point(target)||Dot(Sub(target,origin),Sub(target,origin))>(Geometry.Range+Geometry.Side)*(Geometry.Range+Geometry.Side))return false;
            // Closed segment: a structure at the endpoint still occludes. No owner-wide exemption.
            foreach(var t in surfaces)if(Static(t)||t.Role==SurfaceRole.Construction){double d;Float3 n;if(Ray(t,origin,target,out d,out n))return false;}
            return true;
        }
        public bool IsBuried(Panel p)
        {
            PanelValid(p);
            if(p.Kind=="wall")
            {
                foreach(float edge in new float[]{-.501f,.501f})foreach(float depth in new float[]{-.041f,.041f})
                {
                    Float3 corner=Add(p.Center,p.Axis==0?new Float3(depth,Geometry.Side/2,edge*Geometry.Side):new Float3(edge*Geometry.Side,Geometry.Side/2,depth));
                    float height;bool found=WallGround(corner,corner.Y+Geometry.Range+Geometry.Side+1,corner.Y-.001f,out height);
                    if(!found)return false;
                }
                return true;
            }
            int count=0;float min=Single.PositiveInfinity;
            // Same nine seam-inclusive local/host ground sample positions.
            var offsets=new List<Float3>();offsets.Add(new Float3());
            foreach(float x in new float[]{-.501f,.501f})foreach(float z in new float[]{-.501f,.501f})offsets.Add(new Float3(x*Geometry.Side,0,z*Geometry.Side));
            offsets.Add(new Float3(Geometry.Side*.501f,0,0));offsets.Add(new Float3(-Geometry.Side*.501f,0,0));offsets.Add(new Float3(0,0,Geometry.Side*.501f));offsets.Add(new Float3(0,0,-Geometry.Side*.501f));
            foreach(var o in offsets){float h;if(FloorGround(Add(p.Center,o),p.Center.Y+Geometry.Side+.25f,p.Center.Y-Geometry.Side*3,out h)){count++;min=Math.Min(min,h);}}
            return floorPolicy==FloorBurialPolicy.LocalAllSamples025?count==9&&min>=p.Center.Y-.025f:count>0&&min>=p.Center.Y-.15f;
        }
        private bool WallGround(Float3 spot,float top,float bottom,out float height)
        {
            height=0;
            var skipped=new HashSet<string>(StringComparer.Ordinal);
            Float3 from=new Float3(spot.X,top,spot.Z),to=new Float3(spot.X,bottom,spot.Z);
            // Match the source's 32 nearest-hit queries and whole-RID exclusion.
            // Filtering triangles by normal alone would incorrectly retain another
            // face of the same rejected body and ignore the source traversal budget.
            for(int pass=0;pass<32;pass++)
            {
                double nearest=Double.PositiveInfinity;SourceTriangle hit=null;Float3 normal=new Float3();
                foreach(var t in surfaces)
                {
                    if((!Static(t)&&t.Role!=SurfaceRole.Construction)||skipped.Contains(t.BodyId))continue;
                    double d;Float3 n;
                    if(Ray(t,from,to,out d,out n)&&d<nearest){nearest=d;hit=t;normal=n;}
                }
                if(hit==null)return false;
                if((hit.Role==SurfaceRole.TerrainSurface||(!archive&&hit.Role==SurfaceRole.ConstructionTerrain))&&normal.Y>.01f)
                {height=(float)(top+(bottom-top)*nearest);return true;}
                skipped.Add(hit.BodyId);
            }
            return false;
        }
        private bool FloorGround(Float3 spot,float top,float bottom,out float height)
        {
            double nearest=Double.PositiveInfinity;Float3 normal=new Float3();height=0;
            foreach(var t in surfaces)
            {
                // Ground rays include constructed bodies on source mask1; those
                // bodies still cannot become permanent map support anchors.
                if(!Static(t)&&t.Role!=SurfaceRole.Construction)continue;
                double d;Float3 n;
                if(Ray(t,new Float3(spot.X,top,spot.Z),new Float3(spot.X,bottom,spot.Z),out d,out n)&&d<nearest){nearest=d;normal=n;}
            }
            // Floor policy intentionally inspects only the closest static hit.
            if(Double.IsInfinity(nearest)||normal.Y<.25f)return false;
            height=(float)(top+(bottom-top)*nearest);return true;
        }
        private static bool Overlap(Float3 a,Float3 ah,Float3 b,Float3 bh){var d=Sub(a,b);return Math.Abs(d.X)<=ah.X+bh.X&&Math.Abs(d.Y)<=ah.Y+bh.Y&&Math.Abs(d.Z)<=ah.Z+bh.Z;}
        private static Float3 Add(Float3 a,Float3 b){return new Float3(a.X+b.X,a.Y+b.Y,a.Z+b.Z);}
        private static Float3 Sub(Float3 a,Float3 b){return new Float3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);}
        private static Float3 Cross(Float3 a,Float3 b){return new Float3(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);}
        private static double Dot(Float3 a,Float3 b){return (double)a.X*b.X+(double)a.Y*b.Y+(double)a.Z*b.Z;}
        private static bool Axis(Float3 axis,Float3 a,Float3 b,Float3 c,Float3 h)
        {
            double x=Dot(axis,a),y=Dot(axis,b),z=Dot(axis,c),r=Math.Abs(axis.X)*h.X+Math.Abs(axis.Y)*h.Y+Math.Abs(axis.Z)*h.Z;
            return Math.Min(x,Math.Min(y,z))<=r+1e-9&&Math.Max(x,Math.Max(y,z))>=-r-1e-9;
        }
        private static bool Intersects(SourceTriangle t,Float3 center,Float3 half)
        {
            Float3 a=Sub(t.A,center),b=Sub(t.B,center),c=Sub(t.C,center);
            var axes=new Float3[]{new Float3(1,0,0),new Float3(0,1,0),new Float3(0,0,1)};
            foreach(var axis in axes)if(!Axis(axis,a,b,c,half))return false;
            var edges=new Float3[]{Sub(b,a),Sub(c,b),Sub(a,c)};
            if(!Axis(Cross(edges[0],edges[1]),a,b,c,half))return false;
            foreach(var e in edges)foreach(var axis in axes)if(!Axis(Cross(e,axis),a,b,c,half))return false;
            return true;
        }
        private static bool Ray(SourceTriangle t,Float3 from,Float3 to,out double fraction,out Float3 normal)
        {
            fraction=0;var e1=Sub(t.B,t.A);var e2=Sub(t.C,t.A);normal=Cross(e1,e2);
            double length=Math.Sqrt(Dot(normal,normal));normal=new Float3((float)(normal.X/length),(float)(normal.Y/length),(float)(normal.Z/length));
            var direction=Sub(to,from);var p=Cross(direction,e2);double det=Dot(e1,p);
            if(Math.Abs(det)<1e-12)
            {
                if(Math.Abs(Dot(normal,Sub(from,t.A)))>1e-6)return false;
                // Coplanar segment: clip its barycentric coordinates to the triangle.
                double aa=Dot(e1,e1),ab=Dot(e1,e2),bb=Dot(e2,e2),den=aa*bb-ab*ab;
                var f=Sub(from,t.A);var g=Sub(to,t.A);
                double u0=(bb*Dot(f,e1)-ab*Dot(f,e2))/den,v0=(aa*Dot(f,e2)-ab*Dot(f,e1))/den;
                double u1=(bb*Dot(g,e1)-ab*Dot(g,e2))/den,v1=(aa*Dot(g,e2)-ab*Dot(g,e1))/den;
                double lo=0,hi=1;
                if(!Clip(u0,u1,ref lo,ref hi)||!Clip(v0,v1,ref lo,ref hi)||!Clip(1-u0-v0,1-u1-v1,ref lo,ref hi))return false;
                fraction=lo;return true;
            }
            var s=Sub(from,t.A);double u=Dot(s,p)/det;if(u< -1e-8||u>1+1e-8)return false;
            var q=Cross(s,e1);double v=Dot(direction,q)/det;if(v< -1e-8||u+v>1+1e-8)return false;
            fraction=Dot(e2,q)/det;return fraction>=-1e-8&&fraction<=1+1e-8;
        }
        private static bool Clip(double from,double to,ref double lo,ref double hi)
        {
            double delta=to-from;
            if(Math.Abs(delta)<1e-14)return from>=-1e-8;
            double crossing=(-1e-8-from)/delta;if(delta>0)lo=Math.Max(lo,crossing);else hi=Math.Min(hi,crossing);
            return lo<=hi;
        }
    }
}

using System;
using Ashfall.Core;
using Ashfall.Integration.Playable.HandsWeapons;
static class Cases
{
    static int checks;
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
    static bool Near(float a,float b){return Math.Abs(a-b)<.00001f;}
    static R23ImpactSurface Surface(object id){return new R23ImpactSurface{Source=id,Static=true,AllowedLayer=true};}
    sealed class Plane:IR23ImpactProjection
    {
        public R23ImpactSurface Source=Surface("wall"),Other=Surface("neighbor");public int Rays;public bool Missing,Wall,Wrong,Thin;public float Slope,Height,Dot=1;
        public R23ImpactSample Cast(Float3 a,Float3 b){Rays++;if(Missing)return new R23ImpactSample();
            float t=Wall?(0-a.Z)/(b.Z-a.Z):((Height+Slope*a.X)-a.Y)/((b.Y-a.Y)-Slope*(b.X-a.X));
            var p=new Float3(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t,a.Z+(b.Z-a.Z)*t);
            var n=Wall?new Float3(0,0,1):new Float3((float)Math.Sqrt(Math.Max(0,1-Dot*Dot)),Dot,0);
            if(Slope!=0){float size=(float)Math.Sqrt(1+Slope*Slope);n=new Float3(-Slope/size,1/size,0);}
            return new R23ImpactSample{Found=true,Point=p,Normal=n,Surface=Wrong||Thin&&Math.Abs(p.X)>.01f?Other:Source};
        }
    }
    static bool Build(Plane plane,out R23ImpactPatch patch,R23ImpactSurface? surface=null,Float3? point=null,Float3? normal=null,bool pellet=false,long sequence=0){return R23ImpactMarksKernel.TryBuild(point??new Float3(),normal??new Float3(0,1,0),surface??plane.Source,pellet,sequence,plane,out patch);}
    static int Main(){try{
        Check(R23ImpactMarksKernel.Capacity==512&&R23ImpactMarksKernel.Lifetime==90&&R23ImpactMarksKernel.FadeTime==10&&R23ImpactMarksKernel.SurfaceOffset==.003f,"original fixed512 pool,90s lifetime,10s fade and3mm surface offset");
        var plane=new Plane();R23ImpactPatch patch;
        Check(Build(plane,out patch)&&patch.Rays==9&&patch.Points.Length==9&&R23ImpactMarksKernel.Indices.Length==24,"accepted flat surface uses exactly9 verification rays and8 triangles");
        bool anchored=true;foreach(var p in patch.Points)anchored&=Near(p.Y,.003f);Check(anchored&&Near(patch.Origin.Y,.003f),"all flat-patch vertices lie3mm above the actual collider, not a free-floating plane");
        Check(Near(patch.Points[0].X,-patch.Diameter*.5f)&&Near(patch.Points[0].Z,patch.Diameter*.5f)&&patch.U[0]==0&&patch.V[0]==1,"exact source floor tangent/bitangent and top-down UV survive Unity Z reflection");
        var wall=new Plane{Wall=true};Check(Build(wall,out patch,normal:new Float3(0,0,1))&&Near(patch.Points[0].X,patch.Diameter*.5f)&&Near(patch.Points[0].Y,-patch.Diameter*.5f)&&Near(patch.Points[0].Z,.003f),"wall patch reflects source helper-cross-normal basis and offset");
        Check(Build(plane,out patch,sequence:1)&&Near(patch.Angle,137*(float)Math.PI/180)&&Near(patch.Diameter,.085f*.95f),"accepted sequence1 gives original137degree spin and95percent diameter");
        Check(Near(R23ImpactMarksKernel.Diameter(0,false),.0765f)&&Near(R23ImpactMarksKernel.Diameter(4,false),.0935f)&&Near(R23ImpactMarksKernel.Diameter(5,false),.0765f)&&Near(R23ImpactMarksKernel.Diameter(0,true),.0405f),"source deterministic90..110percent diameter cycle and smaller pellet footprint");
        var facets=new Plane{Slope=.15f};Check(Build(facets,out patch)&&patch.Points[0].Y!=patch.Points[8].Y,"shallow facets conform each vertex instead of rejecting irregular rock or floating a flat decal");
        var missing=new Plane{Missing=true};Check(!Build(missing,out patch),"missing surface projection rejects entire patch without phantom geometry");
        var other=new Plane{Wrong=true};Check(!Build(other,out patch),"strict static surface refuses a neighbor collider across an unsupported edge");
        var painted=other.Source;painted.Painted=true;Check(Build(other,out patch,painted),"actual painted-floor flag allows a verified neighboring static slab");
        var neighbor=other.Other;neighbor.HitReceiver=true;other.Other=neighbor;Check(!Build(other,out patch,painted),"paint-neighbor exception refuses construction/damage hit receivers");neighbor.HitReceiver=false;neighbor.DamageReceiver=true;other.Other=neighbor;Check(!Build(other,out patch,painted),"paint-neighbor exception refuses damageable actors; no blood invented");
        neighbor.DamageReceiver=false;other.Other=neighbor;other.Wall=true;Check(!Build(other,out patch,painted,normal:new Float3(0,0,1)),"paint-neighbor exception is limited to near-horizontal source surfaces");
        var thin=new Plane{Thin=true};Check(!Build(thin,out patch),"ordinary unflagged narrow surface rejects a floating fracture rim");var rendered=thin.Source;rendered.Rendered=true;
        Check(Build(thin,out patch,rendered)&&patch.Rays>9&&patch.Rays<=81,"source rendered-rib flag clips footprint with8 bounded bisections, at most81 rays");
        anchored=true;foreach(var p in patch.Points)anchored&=Math.Abs(p.X)<=.01001f;Check(anchored&&patch.U[0]>0&&patch.V[0]<1,"clipped vertices and UVs remain within the verified narrow rib");
        var steep=new Plane{Dot=.69f};Check(!Build(steep,out patch),"normal discontinuity below source0.7 dot rejects patch");steep.Dot=.7f;Check(Build(steep,out patch),"source0.7 normal-dot boundary remains accepted");
        var far=new Plane{Height=.2f};Check(!Build(far,out patch),"projection farther than footprint diameter rejects displaced geometry");
        foreach(string type in new[]{"dynamic","damageable","layer","proxy"}){var surface=plane.Source;if(type=="dynamic")surface.Static=false;if(type=="damageable")surface.DamageReceiver=true;if(type=="layer")surface.AllowedLayer=false;if(type=="proxy")surface.Excluded=true;int before=plane.Rays;Check(!Build(plane,out patch,surface)&&plane.Rays==before,"ineligible "+type+" surface rejects before any footprint rays");}
        Check(!Build(plane,out patch,point:new Float3(float.NaN,0,0))&&!Build(plane,out patch,normal:new Float3(0,.6f,0)),"invalid point/short normal never reaches native physics");
        var state=new R23ImpactMarksState();Check(state.Count==0&&state.Cursor==0&&state.Sequence==0&&state.Age(0)<0,"new source pool has no fabricated impacts");
        var source=new object();for(int i=0;i<512;i++)state.Commit(source);Check(state.Count==512&&state.Cursor==0&&state.Sequence==512,"source accepts coincident impacts without spacing rejection and stays bounded512");
        var replacement=new object();Check(state.Commit(replacement)==0&&state.Count==512&&state.Source(0)==replacement&&state.Cursor==1,"513th impact reuses oldest pool slot without increasing scene capacity");
        state.Advance(80,delegate{return true;});Check(state.Age(0)==80&&R23ImpactMarksKernel.Alpha(state.Age(0))==1,"source holes remain opaque through80 seconds");state.Advance(5,delegate{return true;});Check(Near(R23ImpactMarksKernel.Alpha(state.Age(0)),.5f),"source final10-second fade is50percent at85 seconds");
        state.Paused=true;state.Advance(100,delegate{return false;});Check(state.Count==512&&state.Age(0)==85,"source pause freezes aging and collider retirement checks");state.Paused=false;state.Advance(-4,delegate{return true;});Check(state.Age(0)==85,"negative process delta cannot reverse source age");state.Advance(5,delegate{return true;});Check(state.Count==0&&state.Age(0)<0,"marks expire at90 seconds rather than becoming permanent");
        state.Commit(source);state.Advance(0,delegate{return false;});Check(state.Count==0,"destroyed/disabled collider retires its mark on next unpaused update");
        state.Commit(source);state.Reject();long sequence=state.Sequence,accepted=state.Accepted;state.Clear();Check(state.Count==0&&state.Cursor==0&&state.Sequence==sequence&&state.Accepted==accepted&&state.Rejected==1,"source clear resets visibility/cursor while preserving fracture sequence and counters");
        float r,g,b,a;R23ImpactMarksKernel.TexturePixel(31,31,out r,out g,out b,out a);Check(Near(r,.025f)&&Near(g,.0235f)&&Near(b,.0215f)&&a==1,"source procedural center is dark neutral fracture, not blood");R23ImpactMarksKernel.TexturePixel(0,0,out r,out g,out b,out a);Check(a==0,"source fracture texture transparent corner avoids rectangular stamps");R23ImpactMarksKernel.TexturePixel(48,32,out r,out g,out b,out a);Check(r>.5f&&g<r&&b<g&&a==1,"source pale fractured rim retains its authored warm tone");
        bool pixels=true;for(int y=0;y<64;y++)for(int x=0;x<64;x++){R23ImpactMarksKernel.TexturePixel(x,y,out r,out g,out b,out a);pixels&=r>=0&&r<=1&&g>=0&&g<=1&&b>=0&&b<=1&&a>=0&&a<=1;}Check(pixels,"all4096 deterministic source texture texels are finite RGBA range");
        Console.WriteLine(checks+" focused original impact-mark cases passed");return 0;
    }catch(Exception e){Console.WriteLine(e);return 1;}}
}

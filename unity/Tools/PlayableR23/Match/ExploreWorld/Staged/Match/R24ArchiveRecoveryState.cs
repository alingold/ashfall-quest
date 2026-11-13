using System;
using System.Collections;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Integration.Playable.World;
namespace Ashfall.Integration.Playable.Match
{
    public sealed class R24ArchiveBox
    {
        public readonly Float3 Position,Size;
        public R24ArchiveBox(Float3 p,Float3 size){if(!p.IsFinite||!size.IsFinite||size.X<0||size.Y<0||size.Z<0)throw new ArgumentException("Actual source AABB");Position=p;Size=size;}
        public Float3 Center{get{return new Float3(Position.X+Size.X*.5f,Position.Y+Size.Y*.5f,Position.Z+Size.Z*.5f);}}
        public float Top{get{return Position.Y+Size.Y;}}
        public bool Contains(Float3 p,float grow){return p.X>=Position.X-grow&&p.Y>=Position.Y-grow&&p.Z>=Position.Z-grow&&p.X<Position.X+Size.X+grow&&p.Y<Position.Y+Size.Y+grow&&p.Z<Position.Z+Size.Z+grow;}
        public bool Intersects(Float3 p,Float3 size){return Position.X<p.X+size.X&&Position.Y<p.Y+size.Y&&Position.Z<p.Z+size.Z&&Position.X+Size.X>p.X&&Position.Y+Size.Y>p.Y&&Position.Z+Size.Z>p.Z;}
    }
    public sealed class R24ArchiveHazard
    {
        public readonly Float3 Minimum,Maximum;readonly double[] planes;
        public R24ArchiveHazard(Float3 low,Float3 high,double[] sourcePlanes){if(!low.IsFinite||!high.IsFinite||sourcePlanes==null||sourcePlanes.Length<16||sourcePlanes.Length%4!=0)throw new ArgumentException("Actual source convex hazard planes");Minimum=low;Maximum=high;planes=(double[])sourcePlanes.Clone();}
        public bool Contains(Float3 p){if(p.X<Minimum.X||p.Y<Minimum.Y||p.Z<Minimum.Z||p.X>Maximum.X||p.Y>Maximum.Y||p.Z>Maximum.Z)return false;for(int i=0;i<planes.Length;i+=4)if((double)planes[i]*p.X+(double)planes[i+1]*p.Y+(double)planes[i+2]*p.Z-planes[i+3]>.00001)return false;return true;}
    }
    public sealed class R24ArchiveRecoveryInfo
    {
        public readonly string Id,SourceSha256,ManifestSha256,CacheFingerprint,AdapterSha256;
        public readonly Float3 BoundsMin,BoundsMax,SolidMin,SolidMax,Center;
        internal readonly Float3[] Recorded,Deathmatch,Warmup;
        internal readonly R24ArchiveBox[] SpawnFloors,Panels;internal readonly R24ArchiveHazard[] Hazards;
        public R24ArchiveRecoveryInfo(string id,string source,string manifest,string cache,string adapter,Float3 low,Float3 high,Float3 solidLow,Float3 solidHigh,Float3 center,Float3[] recorded,Float3[] deathmatch,Float3[] warmup,R24ArchiveBox[] floors,R24ArchiveBox[] panels,R24ArchiveHazard[] hazards)
        {
            if(String.IsNullOrEmpty(id)||!id.StartsWith("archive:",StringComparison.Ordinal)||!low.IsFinite||!high.IsFinite||!solidLow.IsFinite||!solidHigh.IsFinite||!center.IsFinite||high.X<low.X||high.Z<low.Z)throw new ArgumentException("Actual archive map metadata");
            Id=id;SourceSha256=source;ManifestSha256=manifest;CacheFingerprint=cache;AdapterSha256=adapter;BoundsMin=low;BoundsMax=high;SolidMin=solidLow;SolidMax=solidHigh;Center=center;
            Recorded=Copy(recorded);Deathmatch=Copy(deathmatch);Warmup=Copy(warmup);SpawnFloors=(R24ArchiveBox[])floors.Clone();Panels=(R24ArchiveBox[])panels.Clone();Hazards=(R24ArchiveHazard[])hazards.Clone();
        }
        static Float3[] Copy(Float3[] p){if(p==null)throw new ArgumentNullException("source points");foreach(var v in p)if(!v.IsFinite)throw new ArgumentException("Finite source points");return(Float3[])p.Clone();}
        public bool Outside(Float3 p,float launchCeilingLocal,bool actualLaunchExemption)
        {
            if(actualLaunchExemption)return false;if(!p.IsFinite)return true;
            return p.Y<SolidMin.Y-25||p.Y>Math.Max(SolidMax.Y+100,launchCeilingLocal)||Id!="archive:alfa-aglae-souza"&&(p.X<BoundsMin.X-10||p.X>BoundsMax.X+10||p.Z<BoundsMin.Z-10||p.Z>BoundsMax.Z+10);
        }
        public bool PanelBlocks(Float3 floor,float clearance){var at=new Float3(floor.X-.3f,floor.Y+.02f,floor.Z-.3f);var size=new Float3(.6f,clearance,.6f);foreach(var p in Panels)if(p.Intersects(at,size))return true;return false;}
        public bool Hazard(Float3 point){foreach(var hazard in Hazards)if(hazard.Contains(point))return true;return false;}
    }
    public interface IR24ArchiveRecoveryWorld
    {
        bool Floor(Float3 from,float depth,out Float3 point);
        bool Outside(Float3 point);
        bool Room(Float3 floor,bool sourceInitialPanelCheck);
    }
    public sealed class R24ArchiveSpawnPlan
    {
        readonly R24ArchiveRecoveryInfo info;readonly IR24ArchiveRecoveryWorld world;
        readonly List<Float3> spawns=new List<Float3>(),loot=new List<Float3>(),warmupSites=new List<Float3>();
        public bool Ready{get;private set;}public string Failure{get;private set;}
        public Float3 SourceSpawn{get;private set;}public Float3 SourceWarmup{get;private set;}public Float3 MagnumCenter{get;private set;}public float MagnumFallLevel{get;private set;}
        public Float3[] Spawns{get{return spawns.ToArray();}}public Float3[] Loot{get{return loot.ToArray();}}
        public Float3[] WarmupSites{get{return warmupSites.ToArray();}}
        public bool ContainsWarmup(Float3 point)
        {if(!Ready||!point.IsFinite)return false;foreach(var site in warmupSites){float x=point.X-site.X,z=point.Z-site.Z;if(Math.Abs(point.Y-site.Y)<=4&&x*x+z*z<=8*8)return true;}return false;}
        public R24ArchiveSpawnPlan(R24ArchiveRecoveryInfo metadata,IR24ArchiveRecoveryWorld actualWorld){if(metadata==null||actualWorld==null)throw new ArgumentNullException();info=metadata;world=actualWorld;SourceSpawn=Add(info.Center,0,2,0);MagnumFallLevel=float.NegativeInfinity;}
        static Float3 Add(Float3 p,float x,float y,float z){return new Float3(p.X+x,p.Y+y,p.Z+z);}
        static double Distance2(Float3 a,Float3 b){return(double)(a.X-b.X)*(a.X-b.X)+(double)(a.Y-b.Y)*(a.Y-b.Y)+(double)(a.Z-b.Z)*(a.Z-b.Z);}
        static void Unique(List<Float3> values,Float3 p,float spacing){foreach(var v in values)if(Distance2(v,p)<(double)spacing*spacing)return;values.Add(p);}
        public bool Safe(Float3 p,bool initial){Float3 floor;return p.IsFinite&&!world.Outside(p)&&world.Floor(Add(p,0,.6f,0),1,out floor)&&Math.Abs(floor.Y-p.Y)<.35f&&world.Room(floor,initial);}
        // Stable insertion sort preserves source input order when the GDScript
        // comparator declares ties equal; no geometry-derived ordering is invented.
        static List<Float3> Sorted(IEnumerable<Float3> values,Comparison<Float3> compare){var result=new List<Float3>();foreach(var p in values){int at=result.Count;while(at>0&&compare(result[at-1],p)>0)at--;result.Insert(at,p);}return result;}
        public IEnumerator Build()
        {
            Float3 floor;int work=0;
            foreach(var p in info.Recorded){if(world.Floor(Add(p,0,2,0),5,out floor)&&!world.Outside(floor)&&world.Room(floor,true))Unique(spawns,Add(floor,0,.12f,0),1);if(++work%12==0)yield return null;}
            float minX=Math.Max(info.SolidMin.X,info.BoundsMin.X+.6f),maxX=Math.Min(info.SolidMax.X,info.BoundsMax.X-.6f),minZ=Math.Max(info.SolidMin.Z,info.BoundsMin.Z+.6f),maxZ=Math.Min(info.SolidMax.Z,info.BoundsMax.Z-.6f),top=info.SolidMax.Y+5,bottom=info.SolidMin.Y-5;
            if(maxX<=minX||maxZ<=minZ){Failure="No usable source solid/play-bounds intersection";yield break;}
            for(int z=0;z<19;z++)for(int x=0;x<19;x++){var p=new Float3(minX+(maxX-minX)*(x+.5f)/19,top,minZ+(maxZ-minZ)*(z+.5f)/19);if(world.Floor(p,top-bottom,out floor)&&!world.Outside(floor)&&world.Room(floor,true))loot.Add(Add(floor,0,.6f,0));if(++work%12==0)yield return null;}
            bool recorded=spawns.Count>0;
            if(!recorded)foreach(var p in Sorted(loot,delegate(Float3 a,Float3 b){return Distance2(a,info.Center).CompareTo(Distance2(b,info.Center));})){Unique(spawns,Add(p,0,-.48f,0),2);if(spawns.Count>=16)break;}
            if(loot.Count==0)foreach(var p in spawns)loot.Add(Add(p,0,.48f,0));
            if(spawns.Count==0||loot.Count==0){Failure="No native floor/headroom-validated source archive sites";yield break;}
            var ordered=Sorted(spawns,delegate(Float3 a,Float3 b){return a.Y.CompareTo(b.Y);});float median=ordered[ordered.Count/2].Y;
            var lobby=new List<Float3>();var arena=new List<Float3>();foreach(var p in ordered){if(p.Y>median+20&&Safe(p,true)&&recorded)lobby.Add(p);if(p.Y<=median+20&&Safe(p,true))arena.Add(p);}
            if(arena.Count>0){spawns.Clear();spawns.AddRange(arena);}SourceSpawn=spawns[0];
            SourceWarmup=lobby.Count>0?lobby[0]:Sorted(spawns,delegate(Float3 a,Float3 b){return Distance2(a,info.Center).CompareTo(Distance2(b,info.Center));})[0];
            R24ArchiveBox upper=null;foreach(var box in info.SpawnFloors)if(upper==null||box.Top>upper.Top)upper=box;
            if(upper!=null&&upper.Top>median+20){lobby.Clear();arena.Clear();foreach(var p in ordered){if(upper.Contains(p,.6f)&&Safe(p,true))lobby.Add(p);if(p.Y<upper.Position.Y-5&&Safe(p,true))arena.Add(p);}var center=upper.Center;SourceWarmup=lobby.Count>0?lobby[0]:new Float3(center.X,upper.Top+.12f,center.Z);if(arena.Count>0){spawns.Clear();spawns.AddRange(arena);SourceSpawn=spawns[0];}}
            var native=new List<Float3>();foreach(var hint in info.Deathmatch){if(world.Floor(Add(hint,0,1.5f,0),8,out floor)&&floor.Y>=hint.Y-6&&floor.Y<=hint.Y+1){var p=Add(floor,0,.12f,0);if(Safe(p,true)&&p.X>=info.BoundsMin.X&&p.X<=info.BoundsMax.X&&p.Z>=info.BoundsMin.Z&&p.Z<=info.BoundsMax.Z)Unique(native,p,1);}if(++work%12==0)yield return null;}
            if(native.Count>0){spawns.Clear();spawns.AddRange(native);SourceSpawn=spawns[0];}
            foreach(var hint in info.Warmup)
            {
                if(world.Floor(Add(hint,0,1.5f,0),8,out floor)&&floor.Y>=hint.Y-6&&floor.Y<=hint.Y+1&&world.Room(floor,true)){var point=Add(floor,0,.12f,0);warmupSites.Add(point);if(warmupSites.Count==1)SourceWarmup=point;}
                if(++work%12==0)yield return null;
            }
            if(R24ArchiveMapPolicy.MagnumOnly(info.Id)){var center=new Float3();foreach(var p in spawns)center=Add(center,p.X,p.Y,p.Z);center=new Float3(center.X/spawns.Count,center.Y/spawns.Count,center.Z/spawns.Count);MagnumFallLevel=center.Y-20;MagnumCenter=Add(world.Floor(Add(center,0,6,0),12,out floor)?floor:SourceSpawn,0,2.5f,0);}
            Ready=true;
        }
    }
}

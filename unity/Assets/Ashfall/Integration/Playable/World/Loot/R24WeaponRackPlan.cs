using System;
using Ashfall.Core;
namespace Ashfall.Integration.Playable.World.Loot
{
    public struct R24RackHit{public Float3 Point,Normal;}
    public interface IR24WeaponRackWorld
    {
        bool Floor(Float3 from,float depth,out Float3 floor);
        bool SafeSpawn(Float3 point);
        bool Ray(Float3 from,Float3 to,int sourceMask,out R24RackHit hit);
    }
    public sealed class R24WeaponBoard
    {
        public readonly string Id;public readonly Float3 Minimum,Size,Normal;public readonly bool HasFloorHint;public readonly float FloorHint;
        public R24WeaponBoard(string id,Float3 minimum,Float3 size,Float3 normal,bool hasHint,float hint)
        {
            if(String.IsNullOrEmpty(id)||!minimum.IsFinite||!size.IsFinite||!normal.IsFinite||size.X<=0||size.Y<=0||size.Z<=0||Math.Abs(normal.Y)>.01||Math.Abs(Length(normal)-1)>.001||Single.IsNaN(hint)||Single.IsInfinity(hint))throw new ArgumentException("Actual source weapon board required");
            Id=id;Minimum=minimum;Size=size;Normal=normal;HasFloorHint=hasHint;FloorHint=hint;
        }
        public Float3 Center{get{return new Float3(Minimum.X+Size.X*.5f,Minimum.Y+Size.Y*.5f,Minimum.Z+Size.Z*.5f);}}
        static double Length(Float3 p){return Math.Sqrt((double)p.X*p.X+(double)p.Y*p.Y+(double)p.Z*p.Z);}
    }
    public sealed class R24WeaponRackPlan
    {
        public readonly R24WeaponBoard Board;public readonly Float3 Center,Normal,Right;public readonly int Columns,Rows;readonly Float3[] positions;
        internal R24WeaponRackPlan(R24WeaponBoard board,Float3 center,Float3 normal,int columns,int rows,Float3[] points){Board=board;Center=center;Normal=normal;Right=new Float3(normal.Z,0,-normal.X);Columns=columns;Rows=rows;positions=points;}
        public int Count{get{return positions.Length;}}public Float3 Position(int slot){return positions[slot];}
        public Float3 BackplateCenter{get{return R24WeaponRackMath.Add(R24WeaponRackMath.Add(Center,new Float3(0,.9f+Rows*.32f,0)),R24WeaponRackMath.Scale(Normal,.08f));}}
    }
    // Frozen weapon_wall_rack_r18: all vectors are original Godot map-local.
    // The adapter alone performs the one reflection and actual native queries.
    public static class R24WeaponRackMath
    {
        static readonly int[] tiered={0,1,2,3,5,6,7,8,9,10,11,12,13,17,18,19,20,21,23,24,25,27};
        public static int[] Catalog{get{return(int[])tiered.Clone();}}
        public static bool TryBuild(string mapId,R24WeaponBoard board,IR24WeaponRackWorld world,out R24WeaponRackPlan plan,out string refusal)
        {
            plan=null;refusal=null;if(board==null||world==null){refusal="source bindings";return false;}
            var n=board.Normal;n=new Float3(n.X,0,n.Z);double length=Math.Sqrt((double)n.X*n.X+(double)n.Z*n.Z);if(length*length<.5){refusal="source horizontal normal";return false;}n=Scale(n,(float)(1/length));
            Float3 center=board.Center,anchor=new Float3();bool found=false;
            double hint=board.HasFloorHint?board.FloorHint:board.Minimum.Y-3.0,start=Math.Max(board.Minimum.Y-.03,hint+1);
            foreach(int sign in mapId=="archive:chill-gods-pitdevil"?new[]{1}:new[]{1,-1})
            {
                var side=Scale(n,sign);
                foreach(float distance in new[]{.85f,1.3f,2f})
                {
                    var point=Add(center,Scale(side,distance));point=new Float3(point.X,(float)start,point.Z);Float3 floor;R24RackHit hit;
                    if(!world.Floor(point,(float)Math.Max(16,start-hint+5),out floor)||!world.SafeSpawn(Add(floor,new Float3(0,.12f,0))))continue;
                    var to=Add(new Float3(center.X,floor.Y+1.4f,center.Z),Scale(side,.36f));
                    if(world.Ray(Add(floor,new Float3(0,1.4f,0)),to,1,out hit))continue;
                    anchor=floor;n=side;found=true;break;
                }
                if(found)break;
            }
            if(!found){refusal="source anchor access";return false;}
            center=new Float3(center.X,anchor.Y,center.Z);double width=Math.Max(7.6,Math.Min(18,Math.Max(board.Size.X,board.Size.Z)));
            int columns=Math.Max(5,Math.Min(11,(int)Math.Floor(width/1.55))),rows=(tiered.Length+columns-1)/columns;double step=Math.Min(.55,1.5/Math.Max(1,rows-1));var right=new Float3(n.Z,0,-n.X);var positions=new Float3[tiered.Length];
            for(int slot=0;slot<tiered.Length;slot++)
            {
                var desired=Add(Add(Add(center,Scale(right,(float)(((slot%columns)-(columns-1)*.5)*1.55))),new Float3(0,(float)(.7+(rows-1-slot/columns)*step),0)),Scale(n,.38f));R24RackHit hit;
                if(world.Ray(Add(desired,Scale(n,1.8f)),Add(desired,Scale(n,-2.5f)),1,out hit)&&Math.Abs(Dot(hit.Normal,n))>.75)desired=Add(hit.Point,Scale(n,.36f));
                Float3 approach;if(!world.Floor(Add(Add(desired,Scale(n,.8f)),new Float3(0,.5f,0)),5,out approach)||Math.Abs(approach.Y-anchor.Y)>.35||!world.SafeSpawn(Add(approach,new Float3(0,.12f,0)))||world.Ray(Add(desired,Scale(n,.8f)),desired,9,out hit)||world.Ray(Add(approach,new Float3(0,1.5f,0)),desired,9,out hit))
                {refusal="source slot access "+slot;return false;}
                positions[slot]=desired;
            }
            plan=new R24WeaponRackPlan(board,center,n,columns,rows,positions);return true;
        }
        public static Float3 Add(Float3 a,Float3 b){return new Float3(a.X+b.X,a.Y+b.Y,a.Z+b.Z);}
        public static Float3 Sub(Float3 a,Float3 b){return new Float3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);}
        public static Float3 Scale(Float3 a,float k){return new Float3(a.X*k,a.Y*k,a.Z*k);}
        public static double Dot(Float3 a,Float3 b){return(double)a.X*b.X+(double)a.Y*b.Y+(double)a.Z*b.Z;}
    }
}

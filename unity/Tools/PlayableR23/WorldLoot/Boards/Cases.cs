using System;
using Ashfall.Core;
using Ashfall.Integration.Playable.World.Loot;
sealed class Cases
{
    static int count;static void Need(bool value,string why){count++;if(!value)throw new Exception(why);}
    sealed class World:IR24WeaponRackWorld
    {
        public bool noFloor,unsafeFloor,blockAccess,wallSnap;public int slotFailure=-1,sideQueries,slotFloors;public float anchorX;
        public bool Floor(Float3 from,float depth,out Float3 point){point=new Float3(from.X,0,from.Z);if(depth>5)sideQueries++;else{if(slotFloors++==slotFailure)return false;}return !noFloor;}
        public bool SafeSpawn(Float3 point){return !unsafeFloor;}
        public bool Ray(Float3 from,Float3 to,int mask,out R24RackHit hit){hit=new R24RackHit();if(mask==9)return blockAccess;if(wallSnap&&Math.Abs(from.Y-to.Y)<.001&&Math.Abs(from.Z-to.Z)>4){hit=new R24RackHit{Point=new Float3(to.X,to.Y,0),Normal=new Float3(0,0,1)};return true;}if(anchorX!=0&&from.Z>0)return true;return false;}
    }
    static R24WeaponBoard Board(float width){return new R24WeaponBoard("source",new Float3(-width*.5f,1,-.1f),new Float3(width,2,.2f),new Float3(0,0,1),true,0);}
    public static void Main()
    {
        var catalog=R24WeaponRackMath.Catalog;Need(catalog.Length==22&&Array.IndexOf(catalog,27)>=0,"22 tiered firearm catalogs including SMAW");foreach(int excluded in new[]{14,15,16,26,28,29})Need(Array.IndexOf(catalog,excluded)<0,"Non-tiered catalog excluded");catalog[0]=99;Need(R24WeaponRackMath.Catalog[0]==0,"Caller cannot mutate source catalog");
        foreach(float width in new[]{2f,7.6f,10f,18f,30f})
        {
            var world=new World();R24WeaponRackPlan plan;string why;Need(R24WeaponRackMath.TryBuild("archive:source",Board(width),world,out plan,out why),"Actual access plan");int columns=Math.Max(5,Math.Min(11,(int)Math.Floor(Math.Max(7.6,Math.Min(18,width))/1.55)));Need(plan.Columns==columns&&plan.Rows==(22+columns-1)/columns&&plan.Count==22,"Source bounded rows and columns");
            Need(plan.Center.Y==0&&plan.Normal.Z==1&&plan.Right.X==1,"Source floor and right handed axes");for(int slot=0;slot<22;slot++){var p=plan.Position(slot);double step=Math.Min(.55,1.5/Math.Max(1,plan.Rows-1));Need(Math.Abs(p.X-((slot%columns)-(columns-1)*.5)*1.55)<.00001&&Math.Abs(p.Y-(.7+(plan.Rows-1-slot/columns)*step))<.00001&&Math.Abs(p.Z-.38)<.00001,"Exact source slot pose");}
        }
        foreach(int fail in new[]{0,10,21}){R24WeaponRackPlan plan;string why;Need(!R24WeaponRackMath.TryBuild("archive:source",Board(10),new World{slotFailure=fail},out plan,out why)&&plan==null&&why=="source slot access "+fail,"Any failed slot refuses whole rack before item creation");}
        foreach(var world in new[]{new World{noFloor=true},new World{unsafeFloor=true},new World{blockAccess=true}}){R24WeaponRackPlan plan;string why;Need(!R24WeaponRackMath.TryBuild("archive:source",Board(10),world,out plan,out why)&&plan==null,"Missing floor/safe access/LOS refuses");}
        {var world=new World{anchorX=1};R24WeaponRackPlan plan;string why;Need(R24WeaponRackMath.TryBuild("archive:source",Board(10),world,out plan,out why)&&plan.Normal.Z==-1&&world.sideQueries==4,"Try all three source distances then other side");world=new World{anchorX=1};Need(!R24WeaponRackMath.TryBuild("archive:chill-gods-pitdevil",Board(10),world,out plan,out why)&&world.sideQueries==3,"Chill never mounts opposite sign side");}
        {R24WeaponRackPlan plan;string why;Need(R24WeaponRackMath.TryBuild("archive:source",Board(10),new World{wallSnap=true},out plan,out why)&&Math.Abs(plan.Position(0).Z-.36)<.00001,"Native wall hit snaps source grip clearance");}
        Console.WriteLine(count+" mounted-rack source/access cases PASS");
    }
}

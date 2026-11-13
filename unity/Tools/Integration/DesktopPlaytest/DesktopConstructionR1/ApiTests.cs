using System;
using Ashfall.Core;
using Ashfall.Modules.Construction;
using Ashfall.Integration.ConstructionGeometry;
using Ashfall.Integration.DesktopPlaytest.Construction;
public static class DesktopConstructionApiTests
{
    sealed class Occupants:IConstructionOccupants
    {
        public bool Block;
        public bool TrySnapshot(out ProtectedBox[] players,out Float3[] objectives)
        {players=Block?new[]{new ProtectedBox("local-builder",new Float3(0,0,0),new Float3(20,20,20))}:new ProtectedBox[0];objectives=new Float3[0];return true;}
    }
    static int count;
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
    public static int Main()
    {
        var occupants=new Occupants();string hash=new string('a',64);
        var world=new StructuralWorld(new CollisionAllocation(8,9,10,11),new[]{
            new SourceTriangle("floor/0","floor","floor",hash,SurfaceRole.TerrainSurface,8,new Float3(-20,0,-20),new Float3(-20,0,20),new Float3(20,0,20)),
            new SourceTriangle("floor/1","floor","floor",hash,SurfaceRole.TerrainSurface,8,new Float3(-20,0,-20),new Float3(20,0,20),new Float3(20,0,-20))},occupants,false,FloorBurialPolicy.LocalAllSamples025);
        var local=new LocalConstruction(world);Check(local.Balance==999,"explicit-local-seed");
        var pose=new BuilderState(new Float3(1,1,-2),new Float3(0,0,1),new Float3(1,1,-2),true,true);
        PlacedPanel[] panels;int cost;
        Check(local.TryPlace(pose,1,false,out panels,out cost).Accepted,"actual-source-preview");
        Check(local.Snapshot().Length==0 && local.Balance==999,"preview-aborts-both");
        Check(cost==1 && panels.Length==1 && panels[0].Shape.Kind=="wall" && panels[0].Health==180,"wall-source-cost-hp");
        Check(local.TryPlace(pose,1,true,out panels,out cost).Accepted,"actual-prepared-place");
        Check(local.Balance==998 && local.Snapshot().Length==1,"canonical-debit-once");
        int before=local.Balance;int size=local.Snapshot().Length;
        Check(!local.TryPlace(pose,-1,true,out panels,out cost).Accepted,"negative-tick-refused");
        Check(local.Balance==before && local.Snapshot().Length==size,"negative-no-cost");
        var bad=new BuilderState(new Float3(1,1,-2),new Float3(),new Float3(1,1,-2),true,true);
        Check(!local.TryPlace(bad,2,true,out panels,out cost).Accepted,"missing-ray-refused");
        Check(local.Balance==before && local.Snapshot().Length==size,"refusal-preserves-both");
        occupants.Block=true;var fresh=new LocalConstruction(world);
        Check(!fresh.TryPlace(pose,2,true,out panels,out cost).Accepted,"actual-player-clearance-refusal");
        Check(fresh.Balance==999 && fresh.Snapshot().Length==0,"clearance-no-cost");
        occupants.Block=false;
        var floor=new BuilderState(new Float3(1,2,-1),new Float3(0,-1,0),new Float3(1,2,-1),true,true);
        Check(!fresh.TryPlace(floor,3,true,out panels,out cost).Accepted,"source-floor-burial-refusal");
        Check(fresh.Balance==999,"burial-no-cost");
        var elevated=new BuilderState(new Float3(1,4,-1),new Float3(0,-1,0),new Float3(1,4,-1),true,true);
        Check(fresh.TryPlace(elevated,4,true,out panels,out cost).Accepted,"supported-raised-floor");
        bool floorFound=false;foreach(var p in panels)if(p.Shape.Kind=="floor" && p.Health==140)floorFound=true;
        Check(floorFound && cost==2 && fresh.Balance==997,"source-floor-plus-lower-support-cost");
        Console.WriteLine("PASS total="+count);return 0;
    }
}

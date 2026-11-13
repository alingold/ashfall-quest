using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Construction;
using Ashfall.Integration.ConstructionGeometry;

sealed class EmptyOccupantsR4:IConstructionOccupants
{ public bool TrySnapshot(out ProtectedBox[] p,out Float3[] o){p=new ProtectedBox[0];o=new Float3[0];return true;} }
sealed class BuilderR4:IConstructionActors
{ public BuilderState State;public bool TryRead(string actor,out BuilderState value){value=State;return true;} }
sealed class GateR4:IAuthorityGate {public RuleResult Validate(CommandContext c){return RuleResult.Accept();}}
static class WallTerrainR4Tests
{
    static int checks,id;static readonly float S=Geometry.Side;static readonly string H=new string('c',64);
    static void Check(bool ok,string name){checks++;if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
    static void Deck(List<SourceTriangle> list,float y,SurfaceRole role,bool downward,string body,string owner="terrain-root")
    {
        Float3 a=new Float3(-20,y,-20),b=new Float3(-20,y,20),c=new Float3(20,y,-20),d=new Float3(20,y,20);
        list.Add(new SourceTriangle("surface-"+(id++),owner,body,H,role,8,a,downward?c:b,downward?b:c));
        list.Add(new SourceTriangle("surface-"+(id++),owner,body,H,role,8,c,downward?d:b,downward?b:d));
    }
    static StructuralWorld World(List<SourceTriangle> surfaces,bool archive=false)
    {return new StructuralWorld(new CollisionAllocation(8,9,10,11),surfaces,new EmptyOccupantsR4(),archive,FloorBurialPolicy.LocalAllSamples025);}
    static Panel Wall(int axis){return new Panel("wall",axis==0?new Float3(S,S/2,S/2):new Float3(S/2,S/2,S),axis,false,1);}
    static List<SourceTriangle> Layered(string lowerBody,string upperBody,SurfaceRole lower=SurfaceRole.TerrainSurface)
    {
        var surfaces=new List<SourceTriangle>();
        Deck(surfaces,0,SurfaceRole.Structure,false,"base","world");
        Deck(surfaces,S+.2f,lower,false,lowerBody);
        Deck(surfaces,S+.4f,SurfaceRole.TerrainSurface,true,upperBody);
        return surfaces;
    }
    static void Main()
    {
        foreach(int axis in new[]{0,2})foreach(bool archive in new[]{false,true})
        {
            var p=Wall(axis);var separate=World(Layered("terrain-lower","terrain-upper"),archive);
            Check(separate.IsBuried(p),"mixed winding distinct bodies same semantic owner axis="+axis+" archive="+archive);
            Check(!World(Layered("one-body","one-body"),archive).IsBuried(p),"rejected whole body excludes lower face axis="+axis+" archive="+archive);
            var reversed=Layered("lower","upper");reversed.Reverse();
            Check(World(reversed,archive).IsBuried(p),"nearest traversal independent of triangle ordering axis="+axis+" archive="+archive);
            var actor=new BuilderR4{State=axis==0?
                new BuilderState(new Float3(S-1,S/2,S/2),new Float3(1,0,0),new Float3(S-1,S/2,S/2),true,true):
                new BuilderState(new Float3(S/2,S/2,S-1),new Float3(0,0,1),new Float3(S/2,S/2,S-1),true,true)};
            var service=new ConstructionService("s",1,new GateR4(),actor,separate,false);
            var placed=service.Place(new ConstructionCommand(new CommandContext("s","a","burial",42,1,true),0));
            Check(!placed.Accepted&&service.Snapshot().Length==0&&service.Charges("a")==999,"actual buried placement rejects with no charge/spawn axis="+axis+" archive="+archive);
        }
        foreach(int skipped in new[]{0,1,30,31,32,33})
        {
            var list=new List<SourceTriangle>();Deck(list,S+.1f,SurfaceRole.TerrainSurface,false,"eligible");
            for(int i=0;i<skipped;i++)Deck(list,S+.3f+i*.02f,SurfaceRole.Structure,true,"roof-"+i);
            foreach(int axis in new[]{0,2})Check(World(list).IsBuried(Wall(axis))==(skipped<32),"32 query budget skipped="+skipped+" axis="+axis);
        }
        Check(World(Layered("lower","upper",SurfaceRole.ConstructionTerrain),false).IsBuried(Wall(0)),"nonarchive construction terrain eligible after rejected hit");
        Check(!World(Layered("lower","upper",SurfaceRole.ConstructionTerrain),true).IsBuried(Wall(0)),"archive construction terrain excluded after rejected hit");
        var floorDecks=new List<SourceTriangle>();Deck(floorDecks,.5f,SurfaceRole.TerrainSurface,false,"lower");Deck(floorDecks,1,SurfaceRole.TerrainSurface,true,"upper");
        Check(!World(floorDecks).IsBuried(new Panel("floor",new Float3(S/2,0,S/2),1,false,1)),"floor retains closest downward normal rejection");
        var empty=new List<SourceTriangle>();Check(!World(empty).IsBuried(Wall(0)),"missing terrain is not buried");
        Deck(empty,-.1f,SurfaceRole.TerrainSurface,false,"under");Check(!World(empty).IsBuried(Wall(0)),"finite wall probe does not reach below top-corner segment");
        var bad=new List<SourceTriangle>{new SourceTriangle("bad","owner","",H,SurfaceRole.TerrainSurface,8,new Float3(),new Float3(0,0,1),new Float3(1,0,0))};
        bool caught=false;try{World(bad);}catch(ArgumentException){caught=true;}Check(caught,"empty explicit body identity refused");
        Console.WriteLine("TOTAL "+checks+" wall-terrain checks passed");
    }
}

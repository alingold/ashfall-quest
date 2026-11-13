using System;
using System.IO;
using Ashfall.Core;
using Ashfall.Modules.World;

// Regression for W-01: the final pose, including lift, is the safety query pose.
class SpawnLiftRegression : ISpawnQuery
{
    public bool HazardAtLift, BlockLift;
    public Float3 LastRoom, LastHazard;
    public bool TryFloor(Float3 candidate,float up,float depth,out Float3 floor,out Float3 normal)
    { floor=new Float3(candidate.X,0,candidate.Z);normal=new Float3(0,1,0);return true; }
    public bool HasStandingRoom(Float3 feet,float radius,float height)
    { LastRoom=feet;return !BlockLift||feet.Y<.1f; }
    public bool IsHazard(Float3 feet)
    { LastHazard=feet;return HazardAtLift&&feet.Y>.1f; }
    static void Check(bool value,string message) {if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
    static void Main(string[] args)
    {
        byte[] bytes=File.ReadAllBytes(args[1]);
        LoadedMap map=WorldLoader.Load(bytes,WorldLoader.Hash(bytes),true,null);
        // Isolate clearance at a floor-level hint; real-map hints have separate coverage.
        map.Spawns[0].Position=new Float3(0,0,0);
        map.Spawns[0].Team="shared";
        var q=new SpawnLiftRegression {BlockLift=true};
        Check(WorldLoader.SafeSpawns(map,"warmup","red",1.8f,q).Length==0,"ceiling in lift interval rejects spawn");
        q.BlockLift=false;q.HazardAtLift=true;
        Check(WorldLoader.SafeSpawns(map,"warmup","red",1.8f,q).Length==0,"hazard at lifted pose rejects spawn");
        q.HazardAtLift=false;
        var result=WorldLoader.SafeSpawns(map,"warmup","red",1.8f,q);
        Check(result.Length==1,"clear lifted pose accepted");
        Check(result[0].Position.Y==q.LastRoom.Y&&q.LastRoom.Y==q.LastHazard.Y,"returned room and hazard poses agree exactly");
        Check(Math.Abs(result[0].Position.Y-.12f)<1e-6f,"lift retained");
    }
}

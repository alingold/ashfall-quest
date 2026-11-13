using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using Ashfall.Core;
using Ashfall.Modules.World;
class WorldTests
{
    static int count;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;}
    static bool Near(float a,float b){return Math.Abs(a-b)<.0001;}
    static byte[] Bytes(MapDocument d){using(var m=new MemoryStream()){new DataContractJsonSerializer(typeof(MapDocument)).WriteObject(m,d);return m.ToArray();}}
    static MapDocument Read(byte[] b){using(var m=new MemoryStream(b))return (MapDocument)new DataContractJsonSerializer(typeof(MapDocument)).ReadObject(m);}
    static LoadedMap Load(MapDocument d){var b=Bytes(d);return WorldLoader.Load(b,WorldLoader.Hash(b),true,null);}
    static void Reject(Action f,string name){bool rejected=false;try{f();}catch(InvalidDataException){rejected=true;}Check(rejected,name);}
    class Physics : ISpawnQuery
    {
        public bool Block,Hazard,Missing,Steep;
        public bool TryFloor(Float3 c,float up,float depth,out Float3 floor,out Float3 normal){floor=new Float3(c.X,c.Y-.5f,c.Z);normal=Steep?new Float3(1,0,0):new Float3(0,1,0);return !Missing;}
        public bool HasStandingRoom(Float3 p,float r,float h){return !Block;}
        public bool IsHazard(Float3 p){return Hazard;}
    }
    static void Main(string[] args)
    {
        var alphaBytes=File.ReadAllBytes(args[0]);var indoorBytes=File.ReadAllBytes(args[1]);
        var alpha=WorldLoader.Load(alphaBytes,WorldLoader.Hash(alphaBytes),true,null);var indoor=WorldLoader.Load(indoorBytes,WorldLoader.Hash(indoorBytes),true,null);
        Check(alpha.Surfaces.Sum(surface=>surface.Indices.Length/3)==25,"real Alpha triangle count");
        Check(indoor.Surfaces.Sum(surface=>surface.Indices.Length/3)==64,"real indoor triangle count");
        Check(indoor.Indoor&&!indoor.Capsules,"indoor no capsules");
        Check(alpha.Pads.Length==2&&alpha.Pads.Any(p=>Math.Abs(p.Normal.Z)>.01),"real tilted pad metadata");
        var raw=Read(alphaBytes);var s=raw.surfaces[0];var actual=alpha.Surfaces[0];
        Check(actual.Indices[1]==s.indices[2]&&actual.Indices[2]==s.indices[1],"actual positive-basis reflection winding");
        Check(actual.Uv.SequenceEqual(s.uv)&&actual.MaterialPath==s.materialPath,"UV and material preserved");
        Check(actual.SemanticOwner==s.semanticOwner&&actual.ColliderRole=="structure","semantic collider owner");
        Reject(()=>WorldLoader.Load(alphaBytes,new string('0',64),true,null),"corrupt hash rejected");
        Reject(()=>WorldLoader.Load(alphaBytes,WorldLoader.Hash(alphaBytes),false,null),"unapproved production rejected");
        var bad=Read(indoorBytes);bad.capsules=true;Reject(()=>Load(bad),"roof capsule forbidden");
        bad=Read(alphaBytes);bad.surfaces[0].indices[0]=Int32.MaxValue;Reject(()=>Load(bad),"index overflow rejected");
        bad=Read(alphaBytes);bad.surfaces[0].matrix=new float[16];Reject(()=>Load(bad),"singular rejected");
        // Analytic oblique normal and nonuniform mirrored transform: independent expected coordinates.
        var d=Read(alphaBytes);s=d.surfaces[0];d.surfaces=new[]{s};
        s.matrix=new float[]{-2,0,0,0,0,3,0,0,0,0,4,0,10,20,30,1};
        s.vertices=new float[]{1,2,3, 0,0,0, 1,0,0};s.normals=new float[]{1,1,0,1,1,0,1,1,0};s.uv=new float[]{0,0,1,0,0,1};s.indices=new[]{0,1,2};
        actual=Load(d).Surfaces[0];
        Check(Near(actual.Vertices[0].X,8)&&Near(actual.Vertices[0].Y,26)&&Near(actual.Vertices[0].Z,-42),"mirrored scaled translated point");
        Check(actual.Indices.SequenceEqual(new[]{0,1,2}),"negative source determinant winding cancellation");
        Check(Near(actual.Normals[0].X,-.8320503f)&&Near(actual.Normals[0].Y,.5547002f),"inverse transpose oblique normal");
        s.matrix=new float[]{0,0,-2,0,0,3,0,0,4,0,0,0,10,20,30,1};
        actual=Load(d).Surfaces[0];Check(Near(actual.Vertices[0].X,22)&&Near(actual.Vertices[0].Y,26)&&Near(actual.Vertices[0].Z,-28),"rotated basis transfer");
        var physics=new Physics();var spawnMode=indoor.Spawns[0].Mode;var sp=WorldLoader.SafeSpawns(indoor,spawnMode,"red",1.8f,physics);
        Check(sp.Length==1&&Near(sp[0].Position.Y,indoor.Spawns[0].Position.Y-.38f),"floor-validated indoor spawn lift");
        physics.Block=true;Check(WorldLoader.SafeSpawns(indoor,spawnMode,"red",1.8f,physics).Length==0,"roof obstruction rejected");
        physics.Block=false;physics.Hazard=true;Check(WorldLoader.SafeSpawns(indoor,spawnMode,"red",1.8f,physics).Length==0,"hazard rejected");
        physics.Hazard=false;physics.Missing=true;Check(WorldLoader.SafeSpawns(indoor,spawnMode,"red",1.8f,physics).Length==0,"void rejected");
        physics.Missing=false;physics.Steep=true;Check(WorldLoader.SafeSpawns(indoor,spawnMode,"red",1.8f,physics).Length==0,"steep ground rejected");
        Check(WorldLoader.SafeSpawns(indoor,"unknown","red",1.8f,physics).Length==0,"mode filtering");
        Console.WriteLine("PASS "+count+" behavioral assertions; engine/device acceptance UNKNOWN");
    }
}

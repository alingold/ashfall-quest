using System;
using System.IO;
using Ashfall.Modules.MapStreaming;
using Ashfall.Modules.World;
class R4BoundaryHoldouts : ITriangleSink, IWorldApproval
{
    public bool Allow=true; public int Triangles; static int checks;
    public bool IsApproved(ContentIdentity id) {return Allow;}
    public void Accept(string id,TriangleBatch b) {Triangles+=b.TriangleCount;}
    static void Check(bool b,string message) {checks++;if(!b)throw new Exception(message);}
    static void Reject(Action a) {bool hit=false;try{a();}catch(InvalidDataException){hit=true;}catch(OverflowException){hit=true;}Check(hit,"fail closed");}
    static void Main(string[] args)
    {
        var raw=File.ReadAllBytes(args[0]);var bytes=File.ReadAllBytes(args[1]);var gate=new R4BoundaryHoldouts();var accepted=ApprovedWorld.Load(bytes,WorldLoader.Hash(bytes),gate);
        using(var m=MapStream.OpenPilot(raw,MapStream.Hash(raw))) {
            Reject(()=>WorldMapAdapter.StreamApproved(m,accepted,gate,gate,1));Check(gate.Triangles==0,"pilot not promoted by accepted World");gate.Allow=false;
            Reject(()=>WorldMapAdapter.StreamApproved(m,accepted,gate,gate,1));Check(gate.Triangles==0,"revocation before output");
        }
        for(int i=2;i<args.Length;i++) {var malformed=File.ReadAllBytes(args[i]);Reject(()=>{using(var m=MapStream.OpenPilot(malformed,MapStream.Hash(malformed)))m.StreamAll(gate,1);});}
        Console.WriteLine("PASS "+checks+" approved-boundary and exact GLB integer/type holdouts");
    }
}

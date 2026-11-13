using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Ashfall.Modules.MapStreaming;
using Ashfall.Modules.World;
class R4Holdouts : ITriangleSink
{
    static int checks; public int Triangles; public double[] Expected;
    static void Check(bool b,string why) {checks++;if(!b)throw new Exception(why);}
    static void Reject(Action a,string why) {bool hit=false;try{a();}catch(InvalidDataException){hit=true;}Check(hit,why);}
    public void Accept(string id,TriangleBatch b)
    {
        Triangles+=b.TriangleCount;Check(b.TriangleCount<=256,"batch");
        if(Expected!=null)for(int i=0;i<b.TriangleCount*3;i++)for(int j=0;j<3;j++)Check(Math.Abs(b.Normals[i*3+j]-Expected[j])<1e-12,"analytic normal component");
    }
    static object Parse(string s) {return MapJson.Parse(Encoding.UTF8.GetBytes(s),4096,128,1000);}
    static void Main(string[] args)
    {
        foreach(string s in new[]{"{\"a\":1,\"a\":2}","{\"a\":0,\"\\u0061\":1}","01","+1","1.","1e","[1,]","{\"a\":1,}","true false","NaN","1e400","1e-400","\"\\uD800\"","\"\\uDC00\"","\"a\n\"","\uFEFF{}"})Reject(()=>Parse(s),"invalid grammar "+s);
        Reject(()=>MapJson.Parse(new byte[]{34,255,34},100,10,10),"UTF8");
        Reject(()=>MapJson.Parse(Encoding.UTF8.GetBytes("[[[0]]]"),100,3,20),"depth");
        Reject(()=>MapJson.Parse(Encoding.UTF8.GetBytes("[0,1,2]"),100,10,3),"nodes");
        Reject(()=>MapJson.Parse(new byte[101],100,10,10),"bytes");
        foreach(string s in new[]{"1.0","1e0","9223372036854775808","-9223372036854775809"})Reject(()=>((MapJson.Number)Parse(s)).AsInteger(),"integer token/range");
        Check(((MapJson.Number)Parse("9223372036854775807")).AsInteger()==Int64.MaxValue,"max");
        Check(((MapJson.Number)Parse("-9223372036854775808")).AsInteger()==Int64.MinValue,"min");
        Check((string)Parse("\"\\uD83D\\uDE00\"")=="\ud83d\ude00","surrogate");
        var rows=(object[])MapJson.Parse(File.ReadAllBytes(args[0]),65536,20,1000);
        foreach(object item in rows) {var row=(Dictionary<string,object>)item;var raw=File.ReadAllBytes((string)row["path"]);if((bool)row["reject"]) {Reject(()=>{using(var m=MapStream.OpenPilot(raw,MapStream.Hash(raw)))m.StreamAll(new R4Holdouts(),256);},"malformed MAPREF");continue;}
            using(var m=MapStream.OpenPilot(raw,MapStream.Hash(raw))) {var n=(object[])row["normal"];var sink=new R4Holdouts {Expected=new[]{((MapJson.Number)n[0]).AsDouble(),((MapJson.Number)n[1]).AsDouble(),((MapJson.Number)n[2]).AsDouble()}};m.StreamAll(sink,1);Check(sink.Triangles==1,"actual decode");}
        }
        string baseDir=Path.GetDirectoryName(args[0]);byte[] mapref=File.ReadAllBytes(Path.Combine(baseDir,"adapter.mapref.json"));
        byte[] worldBytes=File.ReadAllBytes(Path.Combine(baseDir,"world.json"));var world=QuarantinedWorld.LoadPilot(worldBytes,WorldLoader.Hash(worldBytes));
        using(var m=MapStream.OpenPilot(mapref,MapStream.Hash(mapref))) {var sink=new R4Holdouts();WorldMapAdapter.StreamQuarantined(m,world,sink,1);Check(sink.Triangles==1,"composed actual stream mutated sink");Check(world.Snapshot().Surfaces.Length==1,"actual World parsed geometry");Check(m.Provenance.Sha256==world.Identity.SourceSha256,"immutable provenance");
            byte[] wrong=File.ReadAllBytes(Path.Combine(baseDir,"world-wrong.json"));var other=QuarantinedWorld.LoadPilot(wrong,WorldLoader.Hash(wrong));Reject(()=>WorldMapAdapter.StreamQuarantined(m,other,sink,1),"wrong source");Check(sink.Triangles==1,"rejection preserved output");
            Reject(()=>ApprovedWorld.Load(worldBytes,WorldLoader.Hash(worldBytes),null),"quarantine cannot approve");}
        Console.WriteLine("PASS "+checks+" strict parser/transform/World composition holdouts");
    }
}

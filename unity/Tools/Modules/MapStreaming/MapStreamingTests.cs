using System;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using Ashfall.Modules.MapStreaming;
class MapStreamingTests : ITriangleSink
{
    sealed class Sum {public long Triangles;public double[] P=new double[3],N=new double[3],U=new double[2]; public long Zero;}
    Dictionary<string,Sum> sums=new Dictionary<string,Sum>(); int maxBatch;static int checks;
    static void Check(bool v,string why) {checks++;if(!v)throw new Exception(why);}
    public void Accept(string id,TriangleBatch b)
    {
        maxBatch=Math.Max(maxBatch,b.TriangleCount);Sum s;if(!sums.TryGetValue(id,out s)){s=new Sum();sums.Add(id,s);}s.Triangles+=b.TriangleCount;
        for(int i=0;i<b.TriangleCount*3;i++) {double w=i%3+1;for(int j=0;j<3;j++){s.P[j]+=b.Positions[i*3+j]*w;if(b.Normals!=null)s.N[j]+=b.Normals[i*3+j]*w;}if(b.Uv!=null)for(int j=0;j<2;j++)s.U[j]+=b.Uv[i*2+j]*w;if(b.Normals!=null&&b.Normals[i*3]==0&&b.Normals[i*3+1]==0&&b.Normals[i*3+2]==0)s.Zero++;}
    }
    static Dictionary<string,object> D(object o){return (Dictionary<string,object>)o;}
    static object[] A(object o){return (object[])o;}
    static object Parse(string s){return new JavaScriptSerializer{MaxJsonLength=16777216,RecursionLimit=128}.DeserializeObject(s);}
    static void Compare(double[] actual,object expected) {if(expected==null)return;var a=A(expected);for(int i=0;i<a.Length;i++){double e=Convert.ToDouble(a[i]);Check(Math.Abs(actual[i]-e)<=1e-6+Math.Abs(e)*1e-10,"weighted geometry moment mismatch "+actual[i]+" != "+e);}}
    static void Rejected(Action action,string label) {bool reject=false;try{action();}catch(InvalidDataException){reject=true;}Check(reject,label);}
    static void Mutate(byte[] original,Action<Dictionary<string,object>> edit,string label)
    {var d=D(Parse(System.Text.Encoding.UTF8.GetString(original)));edit(d);byte[] bytes=System.Text.Encoding.UTF8.GetBytes(new JavaScriptSerializer{MaxJsonLength=16777216}.Serialize(d));Rejected(()=>{using(var m=MapStream.OpenPilot(bytes,MapStream.Hash(bytes))){}},label);}
    static void Main(string[] args)
    {
        foreach(string path in args) {
            byte[] bytes=File.ReadAllBytes(path);var oracle=A(Parse(File.ReadAllText(path.Replace(".mapref.json",".oracle.json"))));
            var t=new MapStreamingTests();using(var map=MapStream.OpenPilot(bytes,MapStream.Hash(bytes))) {
                Check(map.PrimitiveCount==oracle.Length,"full primitive coverage");Check(map.ApprovalDisposition=="pilot-quarantine","quarantine");Check(map.SourceCacheBytes==65536,"bounded cache");
                map.StreamAll(t,256);Check(t.maxBatch<=256,"batch limit");
                long triangles=0;foreach(object row in oracle){var d=D(row);Sum sum;Check(t.sums.TryGetValue((string)d["id"],out sum),"missing primitive");Check(sum.Triangles==Convert.ToInt64(d["triangles"]),"triangle count");Compare(sum.P,d["positions"]);Compare(sum.N,d["normals"]);Compare(sum.U,d["uv"]);Check(sum.Zero==Convert.ToInt64(d["zeroNormals"]),"zero normals preserved");triangles+=sum.Triangles;}
                Check(t.sums.Count==oracle.Length,"no extra primitive");Rejected(()=>map.StreamAll(t,257),"oversized batch rejected");
                Console.WriteLine("FULL DECODE "+map.MapId+" primitives="+t.sums.Count+" triangles="+triangles+" batchMax="+t.maxBatch);
            }
            Rejected(()=>MapStream.OpenPilot(bytes,new string('0',64)),"mapref hash");
            Mutate(bytes,d=>D(d["source"])["sha256"]=new string('0',64),"source hash");
            Mutate(bytes,d=>D(d["source"])["path"]="C:map.glb","canonical path");
            Mutate(bytes,d=>D(d["source"])["approvalDisposition"]="accepted","cannot forge production approval");
            Mutate(bytes,d=>D(A(d["instances"])[0])["id"]="forged","instance identity");
            Mutate(bytes,d=>A(D(A(d["instances"])[0])["matrix"])[12]=123456,"matrix identity");
            Mutate(bytes,d=>D(A(d["accessorRanges"])[0])["offset"]=0,"range identity");
            Mutate(bytes,d=>D(A(D(d["references"])["accessors"])[0])["count"]=999,"reference metadata");
            Mutate(bytes,d=>d["instances"]=new object[0],"coverage gap");
            Mutate(bytes,d=>{D(d["policy"])["indoor"]=true;D(d["policy"])["capsules"]=true;},"indoor capsules");
        }
        Console.WriteLine("PASS "+checks+" deterministic assertions; engine/hardware/production approval UNKNOWN");
    }
}

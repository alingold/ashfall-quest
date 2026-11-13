using System;
using System.IO;
using Ashfall.Modules.MapStreaming;
using Ashfall.Modules.World;
using Ashfall.Integration.MapNative;

public static class ConsumerTests
{
    sealed class Lease : IQuarantineLease {public bool Valid=true;public bool Matches(SourceProvenance p){return Valid;}}
    sealed class Store : IGeometryStore,ITriangleSink
    {
        public long Triangles,Zero;public int Calls,Released,Withdrawn,FailAt;public ulong Digest=1469598103934665603UL;public bool Bad,MetadataChecked;
        public System.Collections.Generic.Dictionary<string,int> Materials;
        void Mix(float f){var b=BitConverter.GetBytes(f);foreach(byte x in b)Digest=unchecked((Digest^x)*1099511628211UL);}
        public void Add(PackedBatch b)
        {
            Calls++;if(FailAt==Calls)throw new IOException("store failure");
            if(Materials!=null) {
                Need(b.Binding!=null&&Materials[b.Binding.Id]==b.Binding.MaterialIndex,"exact primitive material routing");
                if(!MetadataChecked){var raw=b.Binding.ManifestSnapshot();Need(MapStream.Hash(raw)==b.Binding.ManifestHash,"full material dependency metadata");raw[0]^=1;Need(MapStream.Hash(b.Binding.ManifestSnapshot())==b.Binding.ManifestHash,"immutable metadata snapshot");MetadataChecked=true;}
            }
            Need(b.Indices.Length<=768,"bounded native indices");for(int i=0;i<b.Indices.Length;i++)Need(b.Indices[i]==i,"winding/order index");
            foreach(float f in b.Positions)Mix(f);if(b.Normals!=null){foreach(float f in b.Normals)Mix(f);for(int i=0;i<b.Normals.Length;i+=3)if(b.Normals[i]==0&&b.Normals[i+1]==0&&b.Normals[i+2]==0)Zero++;}if(b.Uv!=null)foreach(float f in b.Uv)Mix(f);
            Triangles+=b.Indices.Length/3;
        }
        public void Accept(string id,TriangleBatch b){Add(new PackedBatch(null,b));}
        public void Validate(){if(Bad)throw new IOException("tamper");}public void Withdraw(){Withdrawn++;}public void Dispose(){Released++;}
    }
    static int assertions;
    static void Need(bool b,string why){assertions++;if(!b)throw new Exception(why);}
    static void Reject(Action a){bool rejected=false;try{a();}catch{rejected=true;}Need(rejected,"expected rejection");}
    public static int Main(string[] args)
    {
        if(args.Length==1){Ashfall.Tools.MapNative.AppearanceFixture.Cpu(args[0]);return 0;}
        if(args.Length==2){var m=File.ReadAllBytes(args[0]);var w=File.ReadAllBytes(args[1]);var world=QuarantinedWorld.LoadPilot(w,WorldLoader.Hash(w));using(var stream=MapStream.OpenPilot(m,MapStream.Hash(m))){var store=new Store();using(var sink=FullGeometry.Stage(stream,world,m,store,new Lease(),100,100,100000)){var owner=new PreviewLifetime();sink.BindPreviewLifetime(owner);owner.Dispose();Need(sink.RetainedBytes==0&&store.Released==1&&store.Withdrawn==1,"bound lifetime synchronously withdraws both owners");Reject(sink.Validate);owner.Dispose();Need(store.Released==1,"terminal binding idempotent");}}Console.WriteLine("PASS actual small stream bound lifetime");return 0;}
        for(int k=0;k<args.Length;k+=4) {
            byte[] manifest=File.ReadAllBytes(args[k]),payload=File.ReadAllBytes(args[k+1]);long triangles=Int64.Parse(args[k+2]),zero=Int64.Parse(args[k+3]);var world=QuarantinedWorld.LoadPilot(payload,WorldLoader.Hash(payload));
            using(var stream=MapStream.OpenPilot(manifest,MapStream.Hash(manifest))) {
                var direct=new Store();stream.StreamAll(direct,256);var store=new Store();var lease=new Lease();
                var parsed=(System.Collections.Generic.Dictionary<string,object>)MapJson.Parse(manifest,16777216,128,1000000);store.Materials=new System.Collections.Generic.Dictionary<string,int>();foreach(object row in (object[])parsed["instances"]){var d=(System.Collections.Generic.Dictionary<string,object>)row;store.Materials.Add((string)d["id"],(int)((MapJson.Number)d["material"]).AsInteger());}
                var sink=FullGeometry.Stage(stream,world,manifest,store,lease,2000000,20000,536870912);
                Need(sink.Triangles==triangles&&store.Triangles==triangles&&store.Zero==zero,"complete source census/zero normals");Need(store.Digest==direct.Digest&&store.Calls==direct.Calls,"all packed attributes/order match actual decoder");Need(sink.RetainedBytes>0,"retained bytes");sink.Validate();
                lease.Valid=false;Reject(sink.Validate);Need(store.Released==1&&store.Withdrawn==1&&sink.RetainedBytes==0,"revocation cleanup");sink.Dispose();Need(store.Released==1,"idempotent disposal");Reject(()=>sink.Accept("unknown",null));
                var budget=new Store();Reject(()=>FullGeometry.Stage(stream,world,manifest,budget,new Lease(),1,1,1));Need(budget.Released==1&&budget.Calls==0,"budget before store allocation");
                var failure=new Store {FailAt=1};Reject(()=>FullGeometry.Stage(stream,world,manifest,failure,new Lease(),2000000,20000,536870912));Need(failure.Released==1,"store exception cleanup");
                var forged=(byte[])manifest.Clone();forged[0]^=1;var mismatch=new Store();Reject(()=>FullGeometry.Stage(stream,world,forged,mismatch,new Lease(),2000000,20000,536870912));Need(mismatch.Calls==0&&mismatch.Released==1,"manifest identity before output");
                var tamper=new Store();var s2=FullGeometry.Stage(stream,world,manifest,tamper,new Lease(),2000000,20000,536870912);tamper.Bad=true;Reject(s2.Validate);Need(tamper.Released==1,"validation withdraws");
                Console.WriteLine("FULL CONSUMER "+stream.MapId+" triangles="+triangles+" zero="+zero+" batches="+store.Calls);
            }
        }
        Console.WriteLine("PASS assertions="+assertions+"; CPU only; native/render/content approval OPEN");return 0;
    }
}

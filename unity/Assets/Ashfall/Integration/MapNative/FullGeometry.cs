using System;
using System.IO;
using System.Collections.Generic;
using Ashfall.Modules.MapStreaming;
using Ashfall.Modules.World;

namespace Ashfall.Integration.MapNative
{
    // Full immutable source metadata, including material/texture/image/sampler references.
    // A binding is provenance, never appearance or gameplay approval.
    public sealed class PrimitiveBinding
    {
        readonly byte[] manifest;
        public readonly string Id, ManifestHash;
        public readonly int MaterialIndex;
        internal PrimitiveBinding(string id,int material,byte[] bytes,string hash)
        {Id=id;MaterialIndex=material;manifest=bytes;ManifestHash=hash;}
        public byte[] ManifestSnapshot(){return (byte[])manifest.Clone();}
    }
    public sealed class PackedBatch
    {
        public readonly float[] Positions,Normals,Uv;
        public readonly int[] Indices;
        public readonly PrimitiveBinding Binding;
        internal PackedBatch(PrimitiveBinding binding,TriangleBatch b)
        {
            Binding=binding;Positions=Convert(b.Positions);Normals=Convert(b.Normals);Uv=Convert(b.Uv);
            Indices=new int[b.TriangleCount*3];for(int i=0;i<Indices.Length;i++)Indices[i]=i;
        }
        static float[] Convert(double[] a)
        {
            if(a==null)return null;var r=new float[a.Length];
            for(int i=0;i<a.Length;i++) {double d=a[i];if(Double.IsNaN(d)||Double.IsInfinity(d)||Math.Abs(d)>Single.MaxValue)throw new InvalidDataException("float geometry range");r[i]=(float)d;if(d!=0&&r[i]==0)throw new InvalidDataException("float geometry underflow");}return r;
        }
    }
    // Store takes ownership of each packed batch. Release must withdraw participation immediately.
    public interface IGeometryStore : IDisposable {void Add(PackedBatch batch);void Validate();void Withdraw();}
    public interface IQuarantineLease {bool Matches(SourceProvenance source);}
    public sealed class FullGeometry : ITriangleSink,IDisposable
    {
        readonly IGeometryStore store;readonly IQuarantineLease lease;readonly SourceProvenance source;
        readonly Dictionary<string,PrimitiveBinding> bindings=new Dictionary<string,PrimitiveBinding>(StringComparer.Ordinal);
        readonly long maxTriangles,maxBytes;readonly int maxBatches;
        bool closed,complete;PreviewLifetime visibilityOwner;
        public long Triangles {get;private set;} public long RetainedBytes {get;private set;} public int Batches {get;private set;}
        public string Disposition {get{return "pilot-quarantine-no-gameplay-activation";}}
        FullGeometry(MapStream stream,byte[] manifest,IGeometryStore store,IQuarantineLease lease,long triangles,int batches,long bytes)
        {
            if(store==null||lease==null||triangles<1||triangles>2000000||batches<1||batches>20000||bytes<1||bytes>536870912)throw new ArgumentException("store/lease/retention limits");
            this.store=store;this.lease=lease;source=stream.Provenance;maxTriangles=triangles;maxBatches=batches;maxBytes=bytes;
            if(manifest==null||manifest.Length>16777216)throw new InvalidDataException("exact source manifest required");
            byte[] frozen=(byte[])manifest.Clone();if(MapStream.Hash(frozen)!=source.ManifestSha256)throw new InvalidDataException("exact source manifest required");var doc=(Dictionary<string,object>)MapJson.Parse(frozen,16777216,128,1000000);
            foreach(object row in (object[])doc["instances"]) {var d=(Dictionary<string,object>)row;string id=(string)d["id"];int material=checked((int)((MapJson.Number)d["material"]).AsInteger());bindings.Add(id,new PrimitiveBinding(id,material,frozen,source.ManifestSha256));}
        }
        // Takes store ownership on entry, including every rejection. Caller owns the stream.
        public static FullGeometry Stage(MapStream stream,QuarantinedWorld world,byte[] manifest,IGeometryStore store,IQuarantineLease lease,long triangles,int batches,long bytes)
        {
            FullGeometry sink=null;
            try {if(stream==null||world==null)throw new ArgumentNullException("stream/world");sink=new FullGeometry(stream,manifest,store,lease,triangles,batches,bytes);sink.CheckLease();WorldMapAdapter.StreamQuarantined(stream,world,sink,256);sink.complete=true;sink.Validate();return sink;}
            catch {if(sink!=null)sink.Dispose();else if(store!=null){try{store.Withdraw();}finally{store.Dispose();}}throw;}
        }
        void CheckLease(){if(closed)throw new ObjectDisposedException("FullGeometry");if(!lease.Matches(source))throw new InvalidOperationException("quarantine lease revoked");}
        public void Accept(string id,TriangleBatch batch)
        {
            try {
                CheckLease();if(complete)throw new InvalidOperationException("stream complete");PrimitiveBinding binding;
                if(id==null||!bindings.TryGetValue(id,out binding)||batch==null||batch.TriangleCount<1||batch.TriangleCount>256||batch.Positions==null||batch.Positions.Length!=batch.TriangleCount*9||batch.Normals!=null&&batch.Normals.Length!=batch.Positions.Length||batch.Uv!=null&&batch.Uv.Length!=batch.TriangleCount*6)throw new InvalidDataException("batch shape/identity");
                long size=checked((long)(batch.Positions.Length+(batch.Normals==null?0:batch.Normals.Length)+(batch.Uv==null?0:batch.Uv.Length)+batch.TriangleCount*3)*4);
                if(Triangles+batch.TriangleCount>maxTriangles||Batches>=maxBatches||RetainedBytes+size>maxBytes)throw new InvalidDataException("full geometry retention budget exceeded");
                var packed=new PackedBatch(binding,batch);store.Add(packed);CheckLease();Triangles+=batch.TriangleCount;Batches++;RetainedBytes+=size;
            }catch{Dispose();throw;}
        }
        public void Validate(){try{CheckLease();store.Validate();CheckLease();}catch{Dispose();throw;}}
        // Binds both managed and native ownership to the same terminal root lifetime.
        // Root must explicitly pump Validate for source/appearance changes; no hidden worker.
        public void BindPreviewLifetime(PreviewLifetime owner)
        {
            try{Validate();if(!complete||owner==null||visibilityOwner!=null)throw new InvalidOperationException("completed unbound preview required");owner.Check();visibilityOwner=owner;owner.Revoked+=Invalidate;}
            catch{Dispose();throw;}
        }
#if UNITY_6000_0_OR_NEWER
        public void ActivatePreview(PreviewLifetime owner)
        {
            try{BindPreviewLifetime(owner);var native=store as NativeGeometryStore;if(native==null)throw new InvalidOperationException("native preview store required");native.ActivatePreview(owner);Validate();}
            catch{Dispose();throw;}
        }
#endif
        public void Invalidate(){Dispose();}
        public void Dispose(){if(closed)return;closed=true;if(visibilityOwner!=null){visibilityOwner.Revoked-=Invalidate;visibilityOwner=null;}try{store.Withdraw();}finally{try{store.Dispose();}finally{bindings.Clear();RetainedBytes=0;}}}
    }
}

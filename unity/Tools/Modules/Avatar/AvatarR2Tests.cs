using System;
using System.IO;
using System.Text;
using System.Threading;
using Ashfall.Core;
using Ashfall.Modules.Avatar;
class AvatarR2Tests
{
    sealed class Bytes:IAvatarAssetBytes {
        public byte[] Data=new byte[]{1,2,3}; public bool Race; public Thread Worker;
        public byte[] Read(string p) {
            if(Race) { Worker=new Thread(delegate() { Thread.Sleep(1); Data[0]=99; }); Worker.Start(); }
            return Data;
        }
    }
    sealed class Approval:IAvatarApproval {
        public string Hash; public AvatarManifest Original;
        public bool IsApproved(string h,string r) { if(Original!=null) Original.Parts[0].Mount[3]=123; return h==Hash && r=="receipt"; }
    }
    static int count;
    static void Check(bool b,string s) { if(!b) throw new Exception(s); count++; Console.WriteLine("PASS "+s); }
    static void Reject(Action a,string s) { try { a(); } catch(AvatarValidationException) { Check(true,s); return; } throw new Exception("Expected rejection: "+s); }
    static AvatarManifest Fixture(Bytes b) {
        var labels=AvatarAssembler.RequiredLabels(); var p=new PartRecord[labels.Length];
        for(int i=0;i<p.Length;i++) p[i]=new PartRecord{Label=labels[i],AssetId="a",Node=labels[i],MaterialSlots="mat",ColliderRole="render-only",Mount=RenderMounts.Identity()};
        return new AvatarManifest{Schema=1,StyleId="pj",ExportVersion="r2-test",Tier="pcvr",Coordinates="UnitySI-SRS-v1",EyeToShoulder=.18f,BodyRear=.06f,ApprovalReceipt="receipt",Parts=p,
            Assets=new[]{new AssetRecord{Id="a",AbsolutePath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"synthetic.bin"),Sha256=ManifestXml.Sha(b.Data),SourceTree="synthetic",SourceRevision="review",License="test-only"}}};
    }
    static AvatarAssembly Prepare(AvatarManifest m,Bytes b) { return AvatarAssembler.Prepare(m,b,new Approval{Hash=ManifestXml.ContentHash(m)},AvatarUse.Production); }
    static PreparedPart Part(byte[] data,string node,string slot) {
        var b=new Bytes{Data=data}; var m=Fixture(b); m.Parts[0].Node=node; m.Parts[0].MaterialSlots=slot; return Prepare(m,b).CopyParts()[0];
    }
    static void Name(BinaryWriter w,string s) { byte[] b=Encoding.UTF8.GetBytes(s); w.Write(b.Length); w.Write(b); }
    static byte[] Mesh(string node,int index,bool extra) {
        using(var s=new MemoryStream()) { using(var w=new BinaryWriter(s,Encoding.UTF8,true)) {
            w.Write(0x314D5641u); w.Write(1); w.Write(1); Name(w,node);
            var t=RenderMounts.Identity(); t[11]=2; foreach(float f in t) w.Write(f);
            w.Write(1); Name(w,"mat"); for(int i=0;i<4;i++)w.Write(1f);
            w.Write(3);
            for(int i=0;i<3;i++) { w.Write(i==1?1f:0f);w.Write(i==2?1f:0f);w.Write(2f); w.Write(0f);w.Write(0f);w.Write(1f);w.Write(0f);w.Write(0f); }
            w.Write(3);w.Write(0);w.Write(1);w.Write(index); if(extra)w.Write(0);
        } return s.ToArray(); }
    }
    static void Main() {
        var b=new Bytes(); var m=Fixture(b);
        var a=AvatarAssembler.Prepare(m,b,new Approval{Hash=ManifestXml.ContentHash(m),Original=m},AvatarUse.Production);
        Check(a.CopyParts()[0].CopyMount()[3]==0,"review callback mutation cannot change snapshot");
        m=Fixture(b);m.Parts[0].Mount[0]=.002f;m.Parts[0].Mount[1]=.002f;m.Parts[0].Mount[5]=.002f;m.Parts[0].Mount[10]=.002f;
        Reject(delegate{Prepare(m,b);},"review exact .002 scale shear rejected");
        foreach(float scale in new[]{.002f,1f,1000f}) {
            var t=RenderMounts.Identity(); t[0]=scale;t[1]=scale;t[5]=scale;t[10]=scale;
            Check(!AvatarMath.ValidTrs(t),"scale-independent shear rejection "+scale);
        }
        m=Fixture(b);m.Parts[0].Mount[0]=1e30f;m.Parts[0].Mount[5]=1e30f;m.Parts[0].Mount[10]=1e30f;
        Reject(delegate{Prepare(m,b);},"review exact huge finite scale rejected");
        bool rejected=false; try { RenderMounts.Glove(true,new Float3(Single.MaxValue,0,0),new Float3(Single.MaxValue,0,0)); } catch(ArgumentException){rejected=true;}
        Check(rejected,"review huge finite landmarks reject outside representable render bounds");
        float[] g=RenderMounts.Glove(false,new Float3(100,200,300),new Float3(-100,-200,-300));
        Check(AvatarMath.ValidTrs(g) && g[7]==.032f,"midpoint cancellation stays finite");
        var rotated=RenderMounts.Identity(); rotated[0]=0;rotated[4]=.002f;rotated[1]=-1000;rotated[5]=0;rotated[10]=2;
        Check(AvatarMath.ValidTrs(rotated),"rotated nonuniform TRS holdout");
        rotated[3]=Single.MaxValue; Check(!AvatarMath.ValidTrs(rotated),"huge finite translation rejected");
        // Reviewer exact retained 128 MiB buffer/1ms mutation; either rejection or exact identity is required.
        b=new Bytes{Data=new byte[128*1024*1024],Race=true};m=Fixture(b);
        AvatarAssembly raced=null; bool hashRejected=false;
        try { raced=Prepare(m,b); } catch(AvatarValidationException) { hashRejected=true; } finally { b.Worker.Join(); }
        Check(hashRejected || ManifestXml.Sha(raced.CopyParts()[0].Asset.CopyBytes())==raced.CopyParts()[0].Asset.Sha256,"retained mutable race cannot approve mismatched bytes");
        b=new Bytes();m=Fixture(b);a=Prepare(m,b); b.Data[0]=99;
        Check(ManifestXml.Sha(a.CopyParts()[0].Asset.CopyBytes())==a.CopyParts()[0].Asset.Sha256,"retained buffer mutation after Prepare cannot alter bytes");
        var data=Mesh("Jacket",2,false); var part=Part(data,"Jacket","mat"); var decoded=VerifiedMeshReader.Read(part);
        Check(decoded.CopyVertices()[2]==-2 && decoded.CopyNormals()[2]==-1 && decoded.CopyTransform()[11]==-2,"one Z reflection of vertices normals and transform");
        Check(decoded.CopyTriangles()[0][1]==2 && decoded.CopyTriangles()[0][2]==1,"one winding reversal");
        float[] exposed=decoded.CopyVertices();exposed[2]=123;
        Check(decoded.CopyVertices()[2]==-2,"decoded geometry immutable copy");
        Reject(delegate { VerifiedMeshReader.Read(Part(data,"Missing","mat")); },"missing bound node rejected");
        Reject(delegate { VerifiedMeshReader.Read(Part(data,"Jacket","different")); },"material binding substitution rejected");
        Reject(delegate { decoded.RequireBinding(Part(Mesh("Jacket",1,false),"Jacket","mat")); },"hash to mesh substitution rejected");
        Reject(delegate { VerifiedMeshReader.Read(Part(Mesh("Jacket",3,false),"Jacket","mat")); },"out of range mesh index rejected");
        Reject(delegate { VerifiedMeshReader.Read(Part(Mesh("Jacket",2,true),"Jacket","mat")); },"trailing dependencies rejected");
        Reject(delegate { VerifiedMeshReader.Read(Part(new byte[]{1,2,3},"Jacket","mat")); },"unsupported opaque assets cannot pass verified importer");
        Array.Resize(ref data,data.Length-1);
        Reject(delegate { VerifiedMeshReader.Read(Part(data,"Jacket","mat")); },"truncated geometry rejected");
        Console.WriteLine("RESULT "+count+" r2 checks passed; engine/runtime gates UNKNOWN");
    }
}

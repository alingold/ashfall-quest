using System;
using System.IO;
using System.Text;
using Ashfall.Core;
using Ashfall.Modules.Avatar;
class AvatarTests
{
    sealed class Bytes : IAvatarAssetBytes
    {
        public byte[] Data=Encoding.UTF8.GetBytes("immutable test mesh"); public int Reads;
        public byte[] Read(string path) { Reads++; return Data; }
    }
    sealed class Approval : IAvatarApproval
    {
        public string Hash;
        public bool IsApproved(string hash,string receipt) { return Hash==hash && receipt=="test-receipt"; }
    }
    static int count;
    static void Check(bool condition,string name) { if(!condition) throw new Exception(name); count++; Console.WriteLine("PASS "+name); }
    static void Reject(Action action,string name)
    {
        try { action(); } catch(AvatarValidationException) { Check(true,name); return; }
        throw new Exception("Expected rejection: "+name);
    }
    static AvatarManifest Fixture(Bytes bytes)
    {
        var labels=AvatarAssembler.RequiredLabels(); var parts=new PartRecord[labels.Length];
        for(int i=0;i<labels.Length;i++) parts[i]=new PartRecord { Label=labels[i], AssetId="mesh", Node=labels[i], MaterialSlots="cloth,armor", ColliderRole="render-only", Mount=RenderMounts.Identity() };
        parts[13].Mount=RenderMounts.StandingHeadToEye();
        return new AvatarManifest { Schema=1, StyleId="pj", ExportVersion="test-v1", Tier="pcvr", Coordinates="UnitySI-SRS-v1", EyeToShoulder=.18f, BodyRear=.06f,
            ApprovalReceipt="test-receipt", Parts=parts, Assets=new[] { new AssetRecord { Id="mesh", AbsolutePath=Path.GetFullPath("test-mesh.bin"), Sha256=ManifestXml.Sha(bytes.Data), SourceTree="synthetic-test", SourceRevision="fixture1", License="test-only" } } };
    }
    static void Main()
    {
        var bytes=new Bytes(); var m=Fixture(bytes); var approval=new Approval { Hash=ManifestXml.ContentHash(m) };
        var assembled=AvatarAssembler.Prepare(m,bytes,approval,AvatarUse.Production);
        Check(assembled.CopyParts().Length==16 && assembled.ProductionApproved,"complete 13+head+both hands production assembly");
        var p=assembled.CopyParts();
        Check(p[14].Label=="LeftHand" && p[15].Label=="RightHand","left/right identity preserved");
        Check(p[13].CopyMount()[7]==-1.68f,"standing head is mounted in eye space");
        m.Parts[0].Mount[7]=99; m.StyleId="changed"; bytes.Data[0]=0;
        var exposed=p[0].CopyMount(); exposed[0]=99;
        Check(assembled.StyleId=="pj" && p[0].CopyMount()[7]==0 && p[0].CopyMount()[0]==1 && p[0].Asset.CopyBytes()[0]==(byte)'i',"prepared assembly owns immutable snapshots");
        bytes=new Bytes(); m=Fixture(bytes); m.Parts[15].Label="LeftHand";
        Reject(delegate { AvatarAssembler.Prepare(m,bytes,null,AvatarUse.UnreviewedPilot); },"missing right hand cannot pass as duplicate left");
        m=Fixture(bytes); m.Parts[0].ColliderRole="capsule";
        Reject(delegate { AvatarAssembler.Prepare(m,bytes,null,AvatarUse.UnreviewedPilot); },"render assembly cannot acquire collision authority");
        m=Fixture(bytes); m.Parts[0].Mount[0]=Single.NaN;
        Reject(delegate { AvatarAssembler.Prepare(m,bytes,null,AvatarUse.UnreviewedPilot); },"nonfinite transforms rejected");
        m=Fixture(bytes); m.Parts[0].Mount[1]=.4f;
        Reject(delegate { AvatarAssembler.Prepare(m,bytes,null,AvatarUse.UnreviewedPilot); },"shear cannot be silently decomposed");
        m=Fixture(bytes); m.Parts[0].Mount[0]=-1;
        Reject(delegate { AvatarAssembler.Prepare(m,bytes,null,AvatarUse.UnreviewedPilot); },"double coordinate reflection rejected");
        m=Fixture(bytes); m.Schema=2;
        Reject(delegate { AvatarAssembler.Prepare(m,bytes,null,AvatarUse.UnreviewedPilot); },"unknown version fails closed");
        m=Fixture(bytes); int before=bytes.Reads;
        Reject(delegate { AvatarAssembler.Prepare(m,bytes,null,AvatarUse.Production); },"manifest cannot self approve");
        Check(bytes.Reads==before,"unapproved production fails before asset IO");
        approval.Hash=ManifestXml.ContentHash(m); m.BodyRear=.12f;
        Reject(delegate { AvatarAssembler.Prepare(m,bytes,approval,AvatarUse.Production); },"approval binds render profile against substitution");
        m=Fixture(bytes); m.Assets[0].SourceRevision=null;
        Reject(delegate { AvatarAssembler.Prepare(m,bytes,null,AvatarUse.UnreviewedPilot); },"pilot still requires provenance");
        m=Fixture(bytes); bytes.Data[0]=0;
        Reject(delegate { AvatarAssembler.Prepare(m,bytes,null,AvatarUse.UnreviewedPilot); },"changed source bytes fail hash gate even in pilot");
        bytes=new Bytes(); m=Fixture(bytes);
        Check(!AvatarAssembler.Prepare(m,bytes,null,AvatarUse.UnreviewedPilot).ProductionApproved,"explicit pilot does not imply acceptance");
        m.Tier="light";
        Check(AvatarAssembler.Prepare(m,bytes,null,AvatarUse.UnreviewedPilot).Tier=="light","light tier remains distinct");
        // Source landmark (2,4,6) is the midpoint of (1,3,5) and (3,5,7).
        // After source->Unity Z reflection and mount, both palms must be exactly (0,.032,0).
        foreach(bool left in new[]{true,false}) {
            float[] g=RenderMounts.Glove(left,new Float3(1,3,5),new Float3(3,5,7));
            float x=g[0]*2+g[1]*4+g[2]*-6+g[3];
            float y=g[4]*2+g[5]*4+g[6]*-6+g[7];
            float z=g[8]*2+g[9]*4+g[10]*-6+g[11];
            Check(Math.Abs(x)<1e-6 && Math.Abs(y-.032)<1e-6 && Math.Abs(z)<1e-6,"source palm alignment "+(left?"left":"right"));
        }
        using(var s=new MemoryStream(Encoding.UTF8.GetBytes("<!DOCTYPE x [<!ENTITY e SYSTEM 'file:///C:/Windows/win.ini'>]><AvatarManifest>&e;</AvatarManifest>"))) {
            bool rejected=false; try { ManifestXml.Read(s); } catch(InvalidOperationException) { rejected=true; }
            Check(rejected,"XML external entity payload rejected");
        }
        bytes=new Bytes(); m=Fixture(bytes);
        string directory=AppDomain.CurrentDomain.BaseDirectory;
        string meshPath=Path.Combine(directory,"fixture-mesh.bin");
        string manifestPath=Path.Combine(directory,"fixture-manifest.xml");
        File.WriteAllBytes(meshPath,bytes.Data); m.Assets[0].AbsolutePath=meshPath;
        m.ApprovalReceipt=null;
        using(var s=File.Create(manifestPath)) ManifestXml.Write(s,m);
        AvatarManifest loaded;
        using(var s=File.OpenRead(manifestPath)) loaded=ManifestXml.Read(s);
        var fromDisk=AvatarAssembler.Prepare(loaded,new FileAvatarAssets(),null,AvatarUse.UnreviewedPilot);
        Check(fromDisk.CopyParts().Length==16 && !fromDisk.ProductionApproved,"XML manifest and filesystem byte loader round trip");
        File.WriteAllText(meshPath,"tampered asset after export");
        Reject(delegate { AvatarAssembler.Prepare(loaded,new FileAvatarAssets(),null,AvatarUse.UnreviewedPilot); },"on-disk source replacement rejected");
        File.WriteAllBytes(meshPath,bytes.Data);
        Console.WriteLine("RESULT "+count+" checks passed");
    }
}

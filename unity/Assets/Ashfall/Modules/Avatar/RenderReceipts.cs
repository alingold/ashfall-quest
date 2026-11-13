using System;
using System.IO;
using System.Collections.Generic;
namespace Ashfall.Modules.Avatar
{
    public sealed class ReceiptSkinDependencies : ISkinDependencies
    {
        readonly Dictionary<string,VerifiedAsset> assets=new Dictionary<string,VerifiedAsset>(StringComparer.Ordinal);
        public string ExpectedClosureSha256 {get;private set;}
        public ReceiptSkinDependencies(string closure,IDictionary<string,VerifiedAsset> input) {
            SkinJson.Check(closure!=null&&closure.Length==64&&input!=null&&input.Count<=32,"Dependency receipt shape");ExpectedClosureSha256=closure;
            long total=0;foreach(var p in input){SkinJson.Check(p.Key!=null&&p.Value!=null,"Dependency entry");total+=p.Value.ByteLength;SkinJson.Check(total<=VerifiedSkinReader.MaxBytes,"Dependency aggregate");assets.Add(p.Key,p.Value);}
        }
        public VerifiedAsset Resolve(string uri){VerifiedAsset a;SkinJson.Check(assets.TryGetValue(uri,out a),"Dependency not in receipt");return a;}
    }
    // Content hash includes body profile via Assembly.ContentSha256; accepts only independently
    // approved, exact material content. Quarantined reference materials cannot self-promote.
    public sealed class HandMaterialReceipt
    {
        public readonly string AssemblySha256,StyleId,Slot,ContentSha256;
        readonly byte[] albedo,orm,normal;
        public readonly bool Approved;
        public HandMaterialReceipt(AvatarAssembly assembly,string slot,VerifiedAsset albedo,VerifiedAsset orm,VerifiedAsset normal,string receipt,IAvatarApproval approval,AvatarUse use) {
            SkinJson.Check(assembly!=null&&slot!=null&&albedo!=null&&orm!=null&&normal!=null,"Material closure required");
            AssemblySha256=assembly.ContentSha256;StyleId=assembly.StyleId;Slot=slot;
            this.albedo=Png(albedo);this.orm=Png(orm);this.normal=Png(normal);
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){w.Write(AssemblySha256);w.Write(StyleId);w.Write(Slot);w.Write(albedo.Sha256);w.Write(orm.Sha256);w.Write(normal.Sha256);w.Flush();ContentSha256=ManifestXml.Sha(s.ToArray());}
            Approved=use==AvatarUse.Production&&assembly.ProductionApproved&&approval!=null&&approval.IsApproved(ContentSha256,receipt);
            SkinJson.Check(use==AvatarUse.UnreviewedPilot||(use==AvatarUse.Production&&assembly.ProductionApproved&&approval!=null&&approval.IsApproved(ContentSha256,receipt)),"Material not approved");
        }
        static byte[] Png(VerifiedAsset a){SkinJson.Check(a.ByteLength<=4*1024*1024,"Texture encoded budget");byte[] b=a.CopyBytes();SkinJson.Check(ManifestXml.Sha(b)==a.Sha256&&b.Length>=33&&BitConverter.ToString(b,0,8)=="89-50-4E-47-0D-0A-1A-0A"&&System.Text.Encoding.ASCII.GetString(b,12,4)=="IHDR","Texture PNG identity");int width=Big(b,16),height=Big(b,20);SkinJson.Check(width>0&&height>0&&width<=2048&&height<=2048&&b[24]==8&&(b[25]==2||b[25]==6),"Texture decoded budget/format");return b;}
        static int Big(byte[] b,int p){return (b[p]<<24)|(b[p+1]<<16)|(b[p+2]<<8)|b[p+3];}
        public byte[] CopyAlbedo(){return (byte[])albedo.Clone();}public byte[] CopyOrm(){return (byte[])orm.Clone();}public byte[] CopyNormal(){return (byte[])normal.Clone();}
        public void Require(AvatarAssembly assembly,PreparedPart part){SkinJson.Check((!assembly.ProductionApproved||Approved)&&assembly.ContentSha256==AssemblySha256&&assembly.StyleId==StyleId&&part.MaterialSlots==Slot,"Material/profile substitution");}
    }
    public sealed class AvatarRenderReceipts
    {
        readonly Dictionary<string,ReceiptSkinDependencies> dependencies;
        readonly Dictionary<string,HandMaterialReceipt> materials;
        public AvatarRenderReceipts(IDictionary<string,ReceiptSkinDependencies> deps,IDictionary<string,HandMaterialReceipt> mats){SkinJson.Check(deps!=null&&mats!=null&&deps.Count<=2&&mats.Count==2,"Hand receipt count");dependencies=new Dictionary<string,ReceiptSkinDependencies>(deps,StringComparer.Ordinal);materials=new Dictionary<string,HandMaterialReceipt>(mats,StringComparer.Ordinal);}
        public ISkinDependencies Dependencies(string label){ReceiptSkinDependencies d;return dependencies.TryGetValue(label,out d)?d:null;}
        public HandMaterialReceipt Material(AvatarAssembly assembly,PreparedPart part){HandMaterialReceipt m;SkinJson.Check(materials.TryGetValue(part.Label,out m)&&m!=null,"Missing hand material receipt");m.Require(assembly,part);return m;}
    }
}

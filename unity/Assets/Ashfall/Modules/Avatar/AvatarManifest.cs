using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Ashfall.Modules.Avatar
{
    public sealed class AvatarManifest
    {
        public int Schema;
        public string StyleId, ExportVersion, Tier, Coordinates, ApprovalReceipt;
        public float EyeToShoulder, BodyRear;
        public bool ChestPlateFollowsJacket;
        public AssetRecord[] Assets;
        public PartRecord[] Parts;
    }
    public sealed class AssetRecord
    {
        public string Id, AbsolutePath, Sha256, SourceTree, SourceRevision, License;
    }
    public sealed class PartRecord
    {
        public string Label, AssetId, Node, MaterialSlots, ColliderRole;
        // Row-major affine matrix in Unity SI coordinates. Mesh conversion is baked once.
        public float[] Mount;
    }
    public static class ManifestXml
    {
        public static AvatarManifest Read(Stream stream)
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null, MaxCharactersInDocument = 1024 * 1024 };
            using (var reader = XmlReader.Create(stream, settings))
                return (AvatarManifest)new XmlSerializer(typeof(AvatarManifest)).Deserialize(reader);
        }
        public static void Write(Stream stream, AvatarManifest manifest)
        { new XmlSerializer(typeof(AvatarManifest)).Serialize(stream, manifest); }
        // XML formatting and receipt are excluded; all content-affecting fields are included.
        public static string ContentHash(AvatarManifest m)
        {
            using (var s = new MemoryStream())
            {
                using (var w = new BinaryWriter(s, Encoding.UTF8, true))
                {
                    w.Write(m.Schema); Text(w,m.StyleId); Text(w,m.ExportVersion); Text(w,m.Tier);
                    Text(w,m.Coordinates); w.Write(m.EyeToShoulder); w.Write(m.BodyRear);
                    w.Write(m.ChestPlateFollowsJacket); w.Write(m.Assets.Length);
                    foreach(var a in m.Assets) { Text(w,a.Id); Text(w,a.AbsolutePath); Text(w,a.Sha256);
                        Text(w,a.SourceTree); Text(w,a.SourceRevision); Text(w,a.License); }
                    w.Write(m.Parts.Length);
                    foreach(var p in m.Parts) { Text(w,p.Label); Text(w,p.AssetId); Text(w,p.Node);
                        Text(w,p.MaterialSlots); Text(w,p.ColliderRole); w.Write(p.Mount.Length);
                        foreach(float f in p.Mount) w.Write(f); }
                }
                return Sha(s.ToArray());
            }
        }
        static void Text(BinaryWriter w, string s) { w.Write(s != null); if(s != null) w.Write(s); }
        public static string Sha(byte[] bytes)
        { using(var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
    }
    public sealed class FileAvatarAssets : IAvatarAssetBytes
    {
        public byte[] Read(string absolutePath) {
            using(var f=new FileStream(absolutePath,FileMode.Open,FileAccess.Read,FileShare.Read)) {
                SkinJson.Check(f.Length<=VerifiedSkinReader.MaxBytes,"Initial file budget");
                var bytes=new byte[(int)f.Length];int n=0;
                while(n<bytes.Length){int k=f.Read(bytes,n,bytes.Length-n);SkinJson.Check(k>0,"Truncated file");n+=k;}
                SkinJson.Check(f.ReadByte()==-1,"File grew during read");return bytes;
            }
        }
    }
    public sealed class VerifiedAsset
    {
        public readonly string Id, AbsolutePath, Sha256;
        readonly byte[] bytes;
        internal VerifiedAsset(AssetRecord a, byte[] data)
        { Id=a.Id; AbsolutePath=a.AbsolutePath; Sha256=a.Sha256; bytes=data; /* Internal transfer of exclusively owned hashed snapshot. */ }
        public byte[] CopyBytes() { return (byte[])bytes.Clone(); }
        public int ByteLength { get { return bytes.Length; } }
    }
    public sealed class PreparedPart
    {
        public readonly string Label, Node, MaterialSlots;
        public readonly VerifiedAsset Asset;
        readonly float[] mount;
        internal PreparedPart(PartRecord p, VerifiedAsset asset)
        { Label=p.Label; Node=p.Node; MaterialSlots=p.MaterialSlots; Asset=asset; mount=(float[])p.Mount.Clone(); }
        public float[] CopyMount() { return (float[])mount.Clone(); }
    }
    public sealed class AvatarAssembly
    {
        public readonly string StyleId, ContentSha256, Tier;
        public readonly bool ProductionApproved, ChestPlateFollowsJacket;
        public readonly float EyeToShoulder, BodyRear;
        readonly PreparedPart[] parts;
        internal AvatarAssembly(AvatarManifest m, string hash, bool approved, PreparedPart[] p)
        { StyleId=m.StyleId; Tier=m.Tier; ContentSha256=hash; ProductionApproved=approved;
          EyeToShoulder=m.EyeToShoulder; BodyRear=m.BodyRear; ChestPlateFollowsJacket=m.ChestPlateFollowsJacket; parts=p; }
        public PreparedPart[] CopyParts() { return (PreparedPart[])parts.Clone(); }
    }
    public static class AvatarAssembler
    {
        // 13 body labels, standing-space head, two independently authored hands.
        static readonly string[] labels = { "Jacket", "ChestPlate", "Belt", "LUpperArm", "LForearm",
            "LThigh", "LShin", "LBoot", "RUpperArm", "RForearm", "RThigh", "RShin", "RBoot",
            "AvatarHead", "LeftHand", "RightHand" };
        public static string[] RequiredLabels() { return (string[])labels.Clone(); }
        static void Need(bool value, string reason) { if(!value) throw new AvatarValidationException(reason); }
        static bool Bound(string s) { return s==null||s.Length<=2048; }
        static bool Text(string s) { return !String.IsNullOrWhiteSpace(s); }
        static bool Finite(float v) { return !Single.IsNaN(v) && !Single.IsInfinity(v); }
        static bool Hash(string s)
        { if(s==null || s.Length!=64) return false; foreach(char c in s) if(!Uri.IsHexDigit(c)) return false; return true; }
        public static AvatarAssembly Prepare(AvatarManifest input, IAvatarAssetBytes source, IAvatarApproval approvals, AvatarUse use)
        {
            if(input==null || source==null) throw new ArgumentNullException();
            Need(use==AvatarUse.Production || use==AvatarUse.UnreviewedPilot,"Unknown use");
            // Bound shape/strings BEFORE XML allocation. Custom byte ports are trusted IO,
            // but returned arrays are bounded before our snapshot allocation.
            Need(input.Assets!=null&&input.Assets.Length>0&&input.Assets.Length<=128&&input.Parts!=null&&input.Parts.Length==16,"Manifest shape budget");
            foreach(var a in input.Assets)Need(a!=null&&Bound(a.Id)&&Bound(a.AbsolutePath)&&Bound(a.Sha256)&&Bound(a.SourceTree)&&Bound(a.SourceRevision)&&Bound(a.License),"Asset text budget");
            foreach(var p in input.Parts)Need(p!=null&&Bound(p.Label)&&Bound(p.AssetId)&&Bound(p.Node)&&Bound(p.MaterialSlots)&&Bound(p.ColliderRole)&&p.Mount!=null&&p.Mount.Length==16,"Part shape budget");
            Need(Bound(input.StyleId)&&Bound(input.ExportVersion)&&Bound(input.Tier)&&Bound(input.Coordinates)&&Bound(input.ApprovalReceipt),"Manifest text budget");
            // Snapshot prevents caller mutation after validation, including arrays and asset records.
            AvatarManifest m;
            using(var s=new MemoryStream()) { ManifestXml.Write(s,input); s.Position=0; m=ManifestXml.Read(s); }
            Need(m.Schema==1,"Unsupported avatar schema");
            Need(Text(m.StyleId) && Text(m.ExportVersion),"Missing stable style/export identity");
            Need(m.Tier=="pcvr" || m.Tier=="light","Unknown tier");
            Need(m.Coordinates=="UnitySI-SRS-v1","Assets must already have one Z reflection baked");
            Need(Finite(m.EyeToShoulder) && m.EyeToShoulder>0 && m.EyeToShoulder<1 &&
                Finite(m.BodyRear) && Math.Abs(m.BodyRear)<1,"Invalid render profile");
            Need(m.Assets!=null && m.Assets.Length>0 && m.Assets.Length<=128,"Invalid assets");
            Need(m.Parts!=null && m.Parts.Length==labels.Length,"Require 13 body pieces, head and both hands");
            var records=new Dictionary<string,AssetRecord>(StringComparer.Ordinal);
            foreach(var a in m.Assets)
            {
                Need(a!=null && Text(a.Id),"Invalid asset");
                Need(!records.ContainsKey(a.Id),"Duplicate asset ID");
                Need(Text(a.AbsolutePath) && Path.IsPathRooted(a.AbsolutePath) &&
                    Path.GetFullPath(a.AbsolutePath)==a.AbsolutePath,"Asset path must be canonical absolute");
                Need(Hash(a.Sha256) && Text(a.SourceTree) && Text(a.SourceRevision) && Text(a.License),"Missing source hash/provenance/license");
                records.Add(a.Id,a);
            }
            var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var p in m.Parts)
            {
                Need(p!=null && Array.IndexOf(labels,p.Label)>=0 && seen.Add(p.Label),"Missing/unknown/duplicate piece");
                Need(Text(p.AssetId) && records.ContainsKey(p.AssetId) && Text(p.Node) && Text(p.MaterialSlots),"Missing mesh/material binding");
                Need(p.ColliderRole=="render-only","Avatar cannot own collision");
                Need(p.Mount!=null && p.Mount.Length==16,"Invalid mount matrix");
                Need(AvatarMath.ValidTrs(p.Mount),"Invalid bounded TRS mount");
            }
            string hash=ManifestXml.ContentHash(m);
            bool approved=Text(m.ApprovalReceipt) && approvals!=null && approvals.IsApproved(hash,m.ApprovalReceipt);
            Need(use!=AvatarUse.Production || approved,"No accepted immutable manifest receipt");
            var assets=new Dictionary<string,VerifiedAsset>(StringComparer.Ordinal);
            long aggregate=0;
            foreach(var a in m.Assets) {
                byte[] borrowed=source.Read(a.AbsolutePath);
                Need(borrowed!=null&&borrowed.Length<=VerifiedSkinReader.MaxBytes,"Missing/oversize asset bytes");
                aggregate+=borrowed.Length;Need(aggregate<=64L*1024*1024,"Assembly source aggregate budget");
                byte[] bytes=(byte[])borrowed.Clone(); // Hash and retain this exact owned snapshot.
                Need(bytes!=null && String.Equals(ManifestXml.Sha(bytes),a.Sha256,StringComparison.OrdinalIgnoreCase),"Asset hash mismatch: "+a.Id);
                assets.Add(a.Id,new VerifiedAsset(a,bytes));
            }
            var parts=new PreparedPart[labels.Length];
            for(int i=0;i<labels.Length;i++) foreach(var p in m.Parts) if(p.Label==labels[i]) parts[i]=new PreparedPart(p,assets[p.AssetId]);
            return new AvatarAssembly(m,hash,approved,parts);
        }
    }
}

// Derived from pinned AvatarR4Fixtures; Asset uses actual public assembler, with no internal constructor access.
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Ashfall.Modules.Avatar;
public static class AvatarR4Fixtures
{
 public sealed class Source:IAvatarAssetBytes {public Dictionary<string,byte[]> Bytes=new Dictionary<string,byte[]>();public byte[] Read(string p){return Bytes[p];}}
 public static VerifiedAsset Asset(string file){byte[] b=File.ReadAllBytes(file);Source source;var manifest=Manifest(out source);var record=manifest.Assets[0];record.Sha256=ManifestXml.Sha(b);source.Bytes[record.AbsolutePath]=b;return AvatarAssembler.Prepare(manifest,source,null,AvatarUse.UnreviewedPilot).CopyParts()[0].Asset;}
 public static SourceGripPose Grip(VerifiedSkin s,string side){var sets=new List<GripRotations>();foreach(string name in new[]{"Grip 5","Grip","Grip Shaft","Grip 1"}){string p=side+"_"+name+".res";sets.Add(new GripRotations(File.ReadAllBytes(p),File.ReadAllText(p+".sha")));}return new SourceGripPose(s,sets[0],sets[1],sets[2],sets[3]);}
 static void Name(BinaryWriter w,string s){byte[] b=Encoding.UTF8.GetBytes(s);w.Write(b.Length);w.Write(b);}
 static byte[] Body(string node){using(var s=new MemoryStream()){using(var w=new BinaryWriter(s,Encoding.UTF8,true)){w.Write(0x314D5641u);w.Write(1);w.Write(1);Name(w,node);foreach(float f in SkinMatrix.Identity())w.Write(f);w.Write(1);Name(w,"mat");foreach(float f in new[]{.3f,.4f,.5f,1f})w.Write(f);w.Write(3);for(int i=0;i<3;i++)foreach(float f in new[]{i==1?1f:0f,i==2?1f:0f,0f,0f,0f,1f,0f,0f})w.Write(f);w.Write(3);w.Write(0);w.Write(1);w.Write(2);}return s.ToArray();}}
 public static AvatarManifest Manifest(out Source source){source=new Source();var labels=AvatarAssembler.RequiredLabels();var parts=new PartRecord[16];var assets=new AssetRecord[16];for(int i=0;i<16;i++){string path=Path.GetFullPath("part"+i+".bin");bool hand=i>=14;byte[] b=hand?File.ReadAllBytes(i==14?"L.gltf":"R.gltf"):Body(labels[i]);source.Bytes.Add(path,b);assets[i]=new AssetRecord{Id=labels[i],AbsolutePath=path,Sha256=ManifestXml.Sha(b),SourceTree="quarantined-source+analytic-body",SourceRevision="pinned-r4",License="reference-only"};parts[i]=new PartRecord{Label=labels[i],AssetId=labels[i],Node=hand?File.ReadAllLines(i==14?"L.expected":"R.expected")[0]:labels[i],MaterialSlots=hand?"default":"mat",ColliderRole="render-only",Mount=SkinMatrix.Identity()};}return new AvatarManifest{Schema=1,StyleId="camo",ExportVersion="r4-pilot",Tier="pcvr",Coordinates="UnitySI-SRS-v1",EyeToShoulder=.29f,BodyRear=.12f,Assets=assets,Parts=parts};}
 public static AvatarAssembly Assembly(){Source source;var m=Manifest(out source);return AvatarAssembler.Prepare(m,source,null,AvatarUse.UnreviewedPilot);}
 public static HandMaterialReceipt Material(AvatarAssembly a){return new HandMaterialReceipt(a,"default",Asset("glove_camo_albedo.png"),Asset("glove_camo_orm.png"),Asset("glove_camo_normal.png"),null,null,AvatarUse.UnreviewedPilot);}
 public static AvatarRenderReceipts Receipts(AvatarAssembly a){var m=Material(a);return new AvatarRenderReceipts(new Dictionary<string,ReceiptSkinDependencies>(),new Dictionary<string,HandMaterialReceipt>{{"LeftHand",m},{"RightHand",m}});}
}


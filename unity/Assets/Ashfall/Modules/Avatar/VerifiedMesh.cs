using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
namespace Ashfall.Modules.Avatar
{
    // Bounded self-contained rigid mesh interchange, not a GLTF/skin decoder.
    // All data is Godot SI, converted here exactly once. No URI/dependency resolver.
    public sealed class VerifiedMesh
    {
        public readonly string AssetSha256, Node, MaterialSlots;
        readonly float[] vertices, normals, uv, colors, transform;
        readonly int[][] triangles;
        internal VerifiedMesh(string hash,string node,string slots,float[] v,float[] n,float[] u,float[] c,float[] t,int[][] i)
        { AssetSha256=hash; Node=node; MaterialSlots=slots; vertices=v; normals=n; uv=u; colors=c; transform=t; triangles=i; }
        public float[] CopyVertices() { return (float[])vertices.Clone(); }
        public float[] CopyNormals() { return (float[])normals.Clone(); }
        public float[] CopyUv() { return (float[])uv.Clone(); }
        public float[] CopyColors() { return (float[])colors.Clone(); }
        public float[] CopyTransform() { return (float[])transform.Clone(); }
        public int[][] CopyTriangles()
        { var a=new int[triangles.Length][]; for(int i=0;i<a.Length;i++) a[i]=(int[])triangles[i].Clone(); return a; }
        public void RequireBinding(PreparedPart part)
        {
            if(part==null || AssetSha256!=part.Asset.Sha256.ToUpperInvariant() || Node!=part.Node || MaterialSlots!=part.MaterialSlots)
                throw new AvatarValidationException("Mesh substitution: hash/node/material binding mismatch");
        }
    }
    public static class VerifiedMeshReader
    {
        public const int MaxBytes=16*1024*1024;
        static void Need(bool b,string s) { if(!b) throw new AvatarValidationException(s); }
        static int Count(BinaryReader r,int min,int max)
        { int n=r.ReadInt32(); Need(n>=min && n<=max,"Mesh count out of bounds"); return n; }
        static string Name(BinaryReader r)
        {
            int n=Count(r,1,128); byte[] b=r.ReadBytes(n); Need(b.Length==n,"Truncated name");
            string s=new UTF8Encoding(false,true).GetString(b);
            foreach(char c in s) Need(Char.IsLetterOrDigit(c) || c=='_' || c=='-' || c=='.',"Invalid mesh name");
            return s;
        }
        static float Value(BinaryReader r,double max)
        { float v=r.ReadSingle(); Need(AvatarMath.Bounded(v,max),"Nonfinite/out-of-bounds mesh data"); return v; }
        public static VerifiedMesh Read(PreparedPart part)
        {
            if(part==null) throw new ArgumentNullException("part");
            Need(part.Asset.ByteLength<=MaxBytes,"Mesh byte budget exceeded");
            byte[] data=part.Asset.CopyBytes();
            Need(data.Length<=MaxBytes,"Mesh byte budget exceeded");
            Need(ManifestXml.Sha(data)==part.Asset.Sha256.ToUpperInvariant(),"Immutable byte hash mismatch");
            VerifiedMesh selected=null;
            try {
                using(var s=new MemoryStream(data,false)) using(var r=new BinaryReader(s)) {
                    Need(r.ReadUInt32()==0x314D5641 && r.ReadInt32()==1,"Unsupported AVM1 mesh format/version");
                    int nodes=Count(r,1,128), totalVertices=0,totalIndices=0;
                    var names=new HashSet<string>(StringComparer.Ordinal);
                    for(int node=0;node<nodes;node++) {
                        string name=Name(r); Need(names.Add(name),"Duplicate node");
                        var t=new float[16]; for(int k=0;k<16;k++) t[k]=Value(r,AvatarMath.MaxPosition);
                        Need(AvatarMath.ValidTrs(t),"Invalid authored node TRS");
                        // S R S, translation S t; rigid geometry Z and winding reflect once.
                        for(int row=0;row<4;row++) for(int col=0;col<4;col++)
                            if((row==2)!=(col==2)) t[row*4+col]=-t[row*4+col];
                        int slots=Count(r,1,16);
                        var slotNames=new string[slots]; var colors=new float[slots*4];
                        var unique=new HashSet<string>(StringComparer.Ordinal);
                        for(int j=0;j<slots;j++) {
                            slotNames[j]=Name(r); Need(unique.Add(slotNames[j]),"Duplicate material slot");
                            for(int k=0;k<4;k++) { float c=Value(r,1); Need(c>=0,"Negative color"); colors[j*4+k]=c; }
                        }
                        int count=Count(r,3,65535); totalVertices+=count; Need(totalVertices<=131070,"Aggregate vertex budget");
                        var v=new float[count*3]; var n=new float[count*3]; var uv=new float[count*2];
                        for(int j=0;j<count;j++) {
                            for(int k=0;k<3;k++) v[j*3+k]=Value(r,10000)*(k==2?-1:1);
                            double norm=0;
                            for(int k=0;k<3;k++) { float f=Value(r,1); n[j*3+k]=f*(k==2?-1:1); norm+=(double)f*f; }
                            Need(Math.Abs(norm-1)<0.0001,"Normal must be unit length");
                            uv[j*2]=Value(r,10000); uv[j*2+1]=Value(r,10000);
                        }
                        var indices=new int[slots][];
                        for(int j=0;j<slots;j++) {
                            int length=Count(r,3,393210); Need(length%3==0,"Triangle index count");
                            totalIndices+=length; Need(totalIndices<=786420,"Aggregate index budget");
                            indices[j]=new int[length];
                            for(int k=0;k<length;k++) { int index=r.ReadInt32(); Need(index>=0 && index<count,"Invalid vertex index"); indices[j][k]=index; }
                            for(int k=0;k<length;k+=3) { int tmp=indices[j][k+1]; indices[j][k+1]=indices[j][k+2]; indices[j][k+2]=tmp; }
                        }
                        string binding=String.Join(",",slotNames);
                        if(name==part.Node) {
                            Need(binding==part.MaterialSlots,"Material order/name substitution");
                            selected=new VerifiedMesh(ManifestXml.Sha(data),name,binding,v,n,uv,colors,t,indices);
                        }
                    }
                    Need(s.Position==s.Length,"Trailing mesh data/dependencies are unsupported");
                }
            } catch(EndOfStreamException) { throw new AvatarValidationException("Truncated mesh"); }
              catch(DecoderFallbackException) { throw new AvatarValidationException("Invalid UTF8 mesh name"); }
            Need(selected!=null,"Bound node does not exist");
            selected.RequireBinding(part);
            return selected;
        }
    }
}

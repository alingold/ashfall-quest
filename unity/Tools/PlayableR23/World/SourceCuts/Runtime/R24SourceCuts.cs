using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
namespace Ashfall.Integration.Playable.World.Destructibles
{
    public sealed class R24SourceCutMesh
    {
        public int Owner;public bool Precision;public float[] Origin,Basis,Vertices;public int[] SourceTriangles;public double[] Barycentric;public byte[] Kinds;
    }
    // Guarded derivative of the supplied v003 source partitions. All source
    // geometry/attributes remain in geometry.bytes; this file contains weights.
    public sealed class R24SourceCuts
    {
        public const string InputSha="243f1dd751d97ab46dade4efcfacbac4b10c8f0ff9e23b990875e09f27523114";
        public readonly string MapId,BaseMapSha,BaseTowerSha,GeometrySha,ArchitectureSha,SourceArtSha;
        public readonly int BaseOwners,NewOwners,ChunkCount;
        readonly R23ArchivePanelDefinition[] definitions;readonly int[] sourceIds;
        sealed class Tri{internal double[] Bary;internal byte Kind;internal int Owner,Source;}
        sealed class Face{internal int Triangle;internal readonly List<Tri> Triangles=new List<Tri>();}
        sealed class Chunk{internal int Index,Material,Flags,Visible,Vertices,Indices;internal string MaterialKey,VertexSha,IndexSha;internal readonly Dictionary<int,Face> Faces=new Dictionary<int,Face>();}
        readonly Dictionary<int,Chunk> chunks=new Dictionary<int,Chunk>();readonly HashSet<int> verified=new HashSet<int>(),assembled=new HashSet<int>();
        public R24SourceCuts(byte[] bytes,string mapId,string mapSha,string towerSha,string architectureSha,string artSha,string[] materials,int existingOwners,int chunkCount)
        {
            if(bytes==null||bytes.Length>16000000)throw new InvalidDataException("Source cut byte bound");
            using(var r=new BinaryReader(new MemoryStream(bytes,false)))
            {
                if(r.ReadInt32()!=0x34544353||r.ReadInt32()!=1)throw new InvalidDataException("Source cut version");
                MapId=ReadString(r);if(ReadString(r)!=InputSha)throw new InvalidDataException("Source cut input pin");BaseMapSha=ReadString(r);BaseTowerSha=ReadString(r);GeometrySha=ReadString(r);ArchitectureSha=ReadString(r);SourceArtSha=ReadString(r);
                if(ReadString(r)!="a687f6b9bb18aceadeb030f0ce91eae5630b7d52ee87217fd06048fdfeed35d1")throw new InvalidDataException("Source cut prepared R10 pin");
                ChunkCount=r.ReadInt32();BaseOwners=r.ReadInt32();NewOwners=r.ReadInt32();int guards=r.ReadInt32(),faces=r.ReadInt32();
                if(MapId!="archive:classic-bigbox-vr"||mapId!=MapId||BaseMapSha!=mapSha||BaseTowerSha!=towerSha||ArchitectureSha!=architectureSha||SourceArtSha!=artSha||existingOwners!=337||BaseOwners!=337||NewOwners!=381||ChunkCount!=chunkCount||guards!=27||faces!=10907||materials==null)throw new InvalidDataException("Source cut baseline binding changed");
                definitions=new R23ArchivePanelDefinition[NewOwners];sourceIds=new int[NewOwners];var ids=new HashSet<string>();
                for(int i=0;i<NewOwners;i++)
                {
                    sourceIds[i]=r.ReadInt32();var d=new R23ArchivePanelDefinition{id=ReadString(r),sound=ReadString(r),glass=r.ReadInt32()!=0,health=r.ReadSingle(),center=Vector(r),size=Vector(r),probe=Vector(r),normal=Vector(r)};
                    if(!ids.Add(d.id)||d.id!="classic-bigbox-vr/source-passage/"+sourceIds[i]||d.health!=1||d.size[0]<0||d.size[1]<0||d.size[2]<0)throw new InvalidDataException("Source cut definition");definitions[i]=d;
                }
                int total=0;var usedOwners=new bool[NewOwners];
                for(int i=0;i<guards;i++)
                {
                    var c=new Chunk{Index=r.ReadInt32(),Material=r.ReadInt32(),Flags=r.ReadInt32(),Visible=r.ReadInt32(),Vertices=r.ReadInt32(),Indices=r.ReadInt32(),MaterialKey=ReadString(r),VertexSha=ReadString(r),IndexSha=ReadString(r)};
                    if(c.Index<0||c.Index>=chunkCount||chunks.ContainsKey(c.Index)||c.Material<0||c.Material>=materials.Length||materials[c.Material]!=c.MaterialKey||(c.MaterialKey!="vc"&&c.MaterialKey!="s48")||c.Flags!=1||c.Visible!=1||c.Vertices<3||c.Vertices>200000||c.Indices<3||c.Indices%3!=0)throw new InvalidDataException("Source cut chunk guard");
                    int count=Count(r,1,c.Indices/3);
                    for(int j=0;j<count;j++)
                    {
                        var face=new Face{Triangle=r.ReadInt32()};if(face.Triangle<0||face.Triangle>=c.Indices/3||c.Faces.ContainsKey(face.Triangle))throw new InvalidDataException("Source cut face reference");
                        int stat=Count(r,0,4096),owners=Count(r,1,64);
                        for(int t=0;t<stat;t++){int kind=r.ReadInt32();if(kind!=255&&!((kind>=1&&kind<=7)||(kind>=32&&kind<40)||(kind>=48&&kind<64)))throw new InvalidDataException("Source cut static material kind");face.Triangles.Add(new Tri{Bary=Weights(r),Kind=(byte)kind,Owner=-1,Source=face.Triangle});}
                        for(int k=0;k<owners;k++)
                        {
                            int owner=r.ReadInt32(),source=r.ReadInt32(),n=Count(r,1,4096);if(owner<BaseOwners||owner>=BaseOwners+NewOwners||sourceIds[owner-BaseOwners]!=source)throw new InvalidDataException("Source cut owner reference");usedOwners[owner-BaseOwners]=true;
                            for(int t=0;t<n;t++)face.Triangles.Add(new Tri{Bary=Weights(r),Kind=255,Owner=owner,Source=face.Triangle});
                        }
                        double area=0;foreach(var t in face.Triangles)area+=Det(t.Bary);if(Math.Abs(area-1)>2e-8)throw new InvalidDataException("Source cut raw area changed");c.Faces.Add(face.Triangle,face);total++;
                    }
                    chunks.Add(c.Index,c);
                }
                foreach(bool used in usedOwners)if(!used)throw new InvalidDataException("Source cut owner has no geometry");if(total!=faces||r.BaseStream.Position!=r.BaseStream.Length)throw new InvalidDataException("Source cut trailing/missing data");
            }
        }
        public R23ArchivePanelDefinition[] Definitions(R23ArchivePanelDefinition[] existing)
        {if(existing==null||existing.Length!=BaseOwners)throw new InvalidDataException("Protected source owners changed");var result=new R23ArchivePanelDefinition[BaseOwners+NewOwners];Array.Copy(existing,result,BaseOwners);Array.Copy(definitions,0,result,BaseOwners,NewOwners);return result;}
        public void VerifyGeometry(Stream stream){if(HashSpan(stream,0,stream.Length)!=GeometrySha)throw new InvalidDataException("Source cut original geometry changed");}
        public bool VerifyChunk(int chunk,Stream stream,long start,long indexStart,long end,int material,int flags,bool visible,int vertices,int indices)
        {
            Chunk c;if(!chunks.TryGetValue(chunk,out c))return false;
            if(!verified.Add(chunk)||c.Material!=material||c.Flags!=flags||c.Visible!=(visible?1:0)||c.Vertices!=vertices||c.Indices!=indices||indexStart-start!=(long)vertices*48||end-indexStart!=(long)indices*4||HashSpan(stream,start,indexStart-start)!=c.VertexSha||HashSpan(stream,indexStart,end-indexStart)!=c.IndexSha)throw new InvalidDataException("Source cut original chunk changed");return true;
        }
        public int[] KeepStatic(int chunk,int[] original,int[] staticRanges=null)
        {
            Chunk c;if(!chunks.TryGetValue(chunk,out c))return null;if(original.Length!=c.Indices)throw new InvalidDataException("Source cut index span");var selected=new bool[original.Length/3];
            if(staticRanges==null)for(int i=0;i<selected.Length;i++)selected[i]=true;
            else{if(staticRanges.Length%2!=0)throw new InvalidDataException("Source cut static ranges");for(int i=0;i<staticRanges.Length;i+=2){int at=staticRanges[i],n=staticRanges[i+1];if(at<0||n<1||at>selected.Length-n)throw new InvalidDataException("Source cut static range outside original");for(int j=at;j<at+n;j++){if(selected[j])throw new InvalidDataException("Source cut repeated static face");selected[j]=true;}}}
            foreach(int f in c.Faces.Keys)if(!selected[f])throw new InvalidDataException("Source cut would steal protected owner geometry");var keep=new List<int>();for(int i=0;i<selected.Length;i++)if(selected[i]&&!c.Faces.ContainsKey(i)){keep.Add(original[i*3]);keep.Add(original[i*3+1]);keep.Add(original[i*3+2]);}return keep.ToArray();
        }
        public R24SourceCutMesh[] Assemble(int chunk,float[] original,int[] indices)
        {
            Chunk c;if(!chunks.TryGetValue(chunk,out c))return new R24SourceCutMesh[0];if(!verified.Contains(chunk)||!assembled.Add(chunk)||original==null||original.Length!=c.Vertices*12||indices.Length!=c.Indices)throw new InvalidDataException("Source cut assembly input");
            D3 min=Position(original,0),max=min;for(int i=1;i<c.Vertices;i++){var p=Position(original,i);min=D3.Min(min,p);max=D3.Max(max,p);}D3 center=(min+max)*.5;
            var regular=new Dictionary<int,Builder>();var precise=new List<Builder>();var result=new List<Builder>();
            foreach(var face in c.Faces.Values)
            {
                int[] ids={indices[face.Triangle*3],indices[face.Triangle*3+1],indices[face.Triangle*3+2]};var a=Position(original,ids[0]);var e1=Position(original,ids[1])-a;var e2=Position(original,ids[2])-a;var normal=D3.Cross(e1,e2);if(normal.Length<=0)throw new InvalidDataException("Original source cut face degenerate");
                foreach(var t in face.Triangles)
                {
                    D3[] world=new D3[3];for(int k=0;k<3;k++)world[k]=a+e1*t.Bary[k*3+1]+e2*t.Bary[k*3+2];Builder b;
                    if(!regular.TryGetValue(t.Owner,out b)){D3 origin=t.Owner<0?center:From(definitions[t.Owner-BaseOwners].center);b=new Builder(t.Owner,origin);regular.Add(t.Owner,b);result.Add(b);}
                    if(!b.Accepts(world,normal,t.Source,t.Bary))
                    {
                        b=null;foreach(var candidate in precise)if(candidate.Owner==t.Owner&&candidate.Coplanar(world,normal,t.Source)&&candidate.Accepts(world,normal,t.Source,t.Bary)){b=candidate;break;}
                        if(b==null){b=Builder.PrecisionFrame(t.Owner,t.Source,world,t.Bary,e1,e2,normal);precise.Add(b);result.Add(b);}
                    }
                    b.Add(t,world,ids,original,e1,e2,normal);
                }
            }
            var meshes=new List<R24SourceCutMesh>();foreach(var b in result)if(b.Faces.Count>0)meshes.Add(b.Finish());return meshes.ToArray();
        }
        // Explicit Float32 quaternion used by the Unity adapter and CPU TRS
        // conservation checks alike; no hidden LookRotation implementation.
        public static float[] Rotation(R24SourceCutMesh mesh)
        {
            var m=mesh.Basis;double x,y,z,w,trace=m[0]+m[4]+m[8];
            if(trace>0){double s=Math.Sqrt(trace+1)*2;w=.25*s;x=(m[7]-m[5])/s;y=(m[2]-m[6])/s;z=(m[3]-m[1])/s;}
            else if(m[0]>m[4]&&m[0]>m[8]){double s=Math.Sqrt(1+m[0]-m[4]-m[8])*2;w=(m[7]-m[5])/s;x=.25*s;y=(m[1]+m[3])/s;z=(m[2]+m[6])/s;}
            else if(m[4]>m[8]){double s=Math.Sqrt(1+m[4]-m[0]-m[8])*2;w=(m[2]-m[6])/s;x=(m[1]+m[3])/s;y=.25*s;z=(m[5]+m[7])/s;}
            else{double s=Math.Sqrt(1+m[8]-m[0]-m[4])*2;w=(m[3]-m[1])/s;x=(m[2]+m[6])/s;y=(m[5]+m[7])/s;z=.25*s;}
            double n=Math.Sqrt(x*x+y*y+z*z+w*w);return new[]{(float)(x/n),(float)(y/n),(float)(z/n),(float)(w/n)};
        }
        public void Complete(){if(verified.Count!=chunks.Count||assembled.Count!=chunks.Count)throw new InvalidDataException("Source cut guarded chunks not consumed");}
        sealed class Builder
        {
            internal readonly int Owner;internal readonly D3 Origin,U,V,N;internal readonly bool Precision;readonly int sourceFace;internal readonly List<int> Faces=new List<int>();readonly List<float> vertices=new List<float>();readonly List<double> bary=new List<double>();readonly List<byte> kinds=new List<byte>();
            internal Builder(int owner,D3 origin){Owner=owner;Origin=origin;sourceFace=-1;U=new D3(1,0,0);V=new D3(0,1,0);N=new D3(0,0,1);}
            Builder(int owner,int face,D3 origin,D3 u,D3 v,D3 n){Owner=owner;sourceFace=face;Origin=origin;U=u;V=v;N=n;Precision=true;}
            D3 Local(D3 p){var d=p-Origin;return new D3(D3.Dot(d,U),D3.Dot(d,V),D3.Dot(d,N));}
            D3 LocalNormal(D3 p){return new D3(D3.Dot(p,U),D3.Dot(p,V),D3.Dot(p,N));}
            internal bool Coplanar(D3[] world,D3 normal,int face){return sourceFace==face;}
            internal bool Accepts(D3[] p,D3 normal,int face,double[] weights){var a=Local(p[0]).Float;var b=Local(p[1]).Float;var c=Local(p[2]).Float;if(Precision&&sourceFace==face){a.Z=b.Z=c.Z=0;}double fraction=D3.Dot(D3.Cross(b-a,c-a),LocalNormal(normal))/D3.Dot(normal,normal),expected=Det(weights);return fraction>0&&Math.Abs(fraction-expected)<=1e-6+expected*1e-5;}
            internal static Builder PrecisionFrame(int owner,int face,D3[] world,double[] weights,D3 e1,D3 e2,D3 normal)
            {var d=e1*(weights[4]-weights[1])+e2*(weights[5]-weights[2]);var n=normal.Normalized;var u=d.Normalized;if(d.Length<=0)throw new InvalidDataException("Source cut precision edge");return new Builder(owner,face,world[0],u,D3.Cross(n,u).Normalized,n);}
            internal void Add(Tri t,D3[] world,int[] ids,float[] source,D3 e1,D3 e2,D3 normal)
            {
                D3[] local={Local(world[0]),Local(world[1]),Local(world[2])};
                // All fragments of this SAME original triangle are analytically
                // coplanar. Retain exact zero normal-coordinate instead of
                // turning floating dot-product cancellation into fake thickness.
                if(Precision&&sourceFace==t.Source)for(int k=0;k<3;k++)local[k].Z=0;
                if(Precision&&sourceFace==t.Source&&Faces.Count==0)
                {
                    var first=e1*(t.Bary[4]-t.Bary[1])+e2*(t.Bary[5]-t.Bary[2]);var second=e1*(t.Bary[7]-t.Bary[1])+e2*(t.Bary[8]-t.Bary[2]);
                    local[0]=new D3();local[1]=new D3(first.Length,0,0);local[2]=new D3(D3.Dot(second,U),Det(t.Bary)*normal.Length/first.Length,0);
                }
                var cross=D3.Cross(local[1].Float-local[0].Float,local[2].Float-local[0].Float);if(D3.Dot(cross,LocalNormal(normal))<=0)throw new InvalidDataException("Source cut Float32 positive winding could not be retained");
                for(int k=0;k<3;k++)
                {
                    var p=local[k];vertices.Add((float)p.X);vertices.Add((float)p.Y);vertices.Add((float)p.Z);
                    var n=new D3(Sample(source,ids,t.Bary,k,3),Sample(source,ids,t.Bary,k,4),Sample(source,ids,t.Bary,k,5)).Normalized;n=LocalNormal(n);vertices.Add((float)n.X);vertices.Add((float)n.Y);vertices.Add((float)n.Z);
                    for(int channel=6;channel<12;channel++)vertices.Add((float)Sample(source,ids,t.Bary,k,channel));
                    bary.Add(t.Bary[k*3]);bary.Add(t.Bary[k*3+1]);bary.Add(t.Bary[k*3+2]);
                }
                Faces.Add(t.Source);kinds.Add(t.Kind);
            }
            internal R24SourceCutMesh Finish(){return new R24SourceCutMesh{Owner=Owner,Precision=Precision,Origin=Origin.Array,Basis=new[]{(float)U.X,(float)V.X,(float)N.X,(float)U.Y,(float)V.Y,(float)N.Y,(float)U.Z,(float)V.Z,(float)N.Z},Vertices=vertices.ToArray(),SourceTriangles=Faces.ToArray(),Barycentric=bary.ToArray(),Kinds=kinds.ToArray()};}
        }
        static double Sample(float[] v,int[] ids,double[] w,int k,int channel){return v[ids[0]*12+channel]*w[k*3]+v[ids[1]*12+channel]*w[k*3+1]+v[ids[2]*12+channel]*w[k*3+2];}
        struct D3
        {
            internal double X,Y,Z;internal D3(double x,double y,double z){X=x;Y=y;Z=z;}internal double Length{get{return Math.Sqrt(X*X+Y*Y+Z*Z);}}internal D3 Normalized{get{double n=Length;if(n<=0||Double.IsNaN(n)||Double.IsInfinity(n))throw new InvalidDataException("Source cut nonzero finite vector");return this*(1/n);}}internal D3 Float{get{return new D3((float)X,(float)Y,(float)Z);}}internal float[] Array{get{return new[]{(float)X,(float)Y,(float)Z};}}
            public static D3 operator+(D3 a,D3 b){return new D3(a.X+b.X,a.Y+b.Y,a.Z+b.Z);}public static D3 operator-(D3 a,D3 b){return new D3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);}public static D3 operator*(D3 a,double n){return new D3(a.X*n,a.Y*n,a.Z*n);}internal static double Dot(D3 a,D3 b){return a.X*b.X+a.Y*b.Y+a.Z*b.Z;}internal static D3 Cross(D3 a,D3 b){return new D3(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);}internal static D3 Min(D3 a,D3 b){return new D3(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y),Math.Min(a.Z,b.Z));}internal static D3 Max(D3 a,D3 b){return new D3(Math.Max(a.X,b.X),Math.Max(a.Y,b.Y),Math.Max(a.Z,b.Z));}
        }
        static D3 Position(float[] v,int i){return new D3(v[i*12],v[i*12+1],v[i*12+2]);}static D3 From(float[] v){return new D3(v[0],v[1],v[2]);}
        static int Count(BinaryReader r,int min,int max){int n=r.ReadInt32();if(n<min||n>max)throw new InvalidDataException("Source cut bounded count");return n;}
        static string ReadString(BinaryReader r){int n=Count(r,0,1024);byte[] b=r.ReadBytes(n);if(b.Length!=n)throw new EndOfStreamException();return Encoding.UTF8.GetString(b);}
        static float[] Vector(BinaryReader r){var a=new[]{r.ReadSingle(),r.ReadSingle(),r.ReadSingle()};foreach(float x in a)if(Single.IsNaN(x)||Single.IsInfinity(x))throw new InvalidDataException("Source cut finite definition");return a;}
        static double[] Weights(BinaryReader r){var a=new double[9];for(int i=0;i<9;i++){a[i]=r.ReadDouble();if(Double.IsNaN(a[i])||Double.IsInfinity(a[i])||a[i]<-1e-10||a[i]>1+1e-10)throw new InvalidDataException("Source cut finite inside barycentric");}for(int i=0;i<9;i+=3)if(Math.Abs(a[i]+a[i+1]+a[i+2]-1)>1e-9)throw new InvalidDataException("Source cut barycentric sum");if(Det(a)<=0)throw new InvalidDataException("Source cut positive barycentric winding");return a;}
        static double Det(double[] a){return (a[4]-a[1])*(a[8]-a[2])-(a[5]-a[2])*(a[7]-a[1]);}
        public static string Hash(byte[] bytes){using(var stream=new MemoryStream(bytes,false))return HashSpan(stream,0,stream.Length);}
        static string HashSpan(Stream stream,long at,long count){long saved=stream.Position;try{if(at<0||count<0||at>stream.Length-count)throw new InvalidDataException("Source cut hash span");stream.Position=at;using(var h=SHA256.Create()){var buf=new byte[32768];while(count>0){int n=stream.Read(buf,0,(int)Math.Min(buf.Length,count));if(n<=0)throw new EndOfStreamException();h.TransformBlock(buf,0,n,buf,0);count-=n;}h.TransformFinalBlock(new byte[0],0,0);return BitConverter.ToString(h.Hash).Replace("-","").ToLowerInvariant();}}finally{stream.Position=saved;}}
    }
}

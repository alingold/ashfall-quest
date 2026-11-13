using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;


namespace Ashfall.Modules.MapStreaming
{
    // Pilot-only, no production activation API. Source handle denies writers until disposed.
    public sealed class MapStream : IDisposable
    {
        readonly FileStream file;
        readonly Dictionary<string,object> doc;
        readonly List<Instance> instances = new List<Instance>();
        readonly long binOffset, binLength;
        readonly byte[] page = new byte[65536];
        long pageStart=-1; int pageCount; bool streaming, disposed;
        public int PrimitiveCount { get { return instances.Count; } }
        public string MapId { get; private set; }
        public SourceProvenance Provenance { get; private set; }
        public string ApprovalDisposition { get { return "pilot-quarantine"; } }
        public int SourceCacheBytes { get { return page.Length; } }
        sealed class Instance { public string Id; public double[] M; public Dictionary<string,object> P; public int Node,Mesh,Primitive; }
        public static string Hash(byte[] bytes) { using(var h=SHA256.Create()) return Hex(h.ComputeHash(bytes)); }
        static string Hex(byte[] b) { return BitConverter.ToString(b).Replace("-","").ToLowerInvariant(); }
        static void Need(bool v,string why) { if(!v) throw new InvalidDataException(why); }
        static Dictionary<string,object> D(object o) { return (Dictionary<string,object>)o; }
        static object[] A(object o) { return (object[])o; }
        static long L(object o) { var n=o as MapJson.Number;Need(n!=null,"integer type");return n.AsInteger(); }
        static int I(object o) { return checked((int)L(o)); }
        static long N(Dictionary<string,object> d,string k,long fallback) { return d.ContainsKey(k)?L(d[k]):fallback; }
        static double F(object o) { double x=o is double?(double)o:o is MapJson.Number?((MapJson.Number)o).AsDouble():Double.NaN;Need(!double.IsNaN(x)&&!double.IsInfinity(x),"nonfinite");return x; }
        static object Parse(byte[] raw) { return MapJson.Parse(raw,16777216,128,1000000); }
        static bool Equal(object a,object b)
        {
            var x=a as Dictionary<string,object>; var y=b as Dictionary<string,object>;
            if(x!=null&&y!=null) { if(x.Count!=y.Count)return false;foreach(var k in x.Keys)if(!y.ContainsKey(k)||!Equal(x[k],y[k]))return false;return true; }
            var ar=a as object[];var br=b as object[];
            if(ar!=null&&br!=null) {if(ar.Length!=br.Length)return false;for(int i=0;i<ar.Length;i++)if(!Equal(ar[i],br[i]))return false;return true;}
            if(a==null||b==null)return a==b;
            if(a is string||b is string||a is bool||b is bool)return a.Equals(b);
            return F(a)==F(b);
        }
        public static MapStream OpenPilot(byte[] mapref,string expectedMaprefSha256)
        { Need(mapref!=null&&mapref.Length<=16777216,"manifest budget");Need(Hash(mapref)==expectedMaprefSha256,"manifest hash");return new MapStream(D(Parse(mapref)),expectedMaprefSha256); }
        MapStream(Dictionary<string,object> input,string manifestHash)
        {

            MapSchema.Validate(input);
            Need((string)input["format"]=="ASHFALL-MAPREF/1"&&I(input["schema"])==1,"format");
            MapId=(string)input["mapId"];Need(!String.IsNullOrEmpty(MapId),"mapId");
            var src=D(input["source"]);string path=(string)src["path"];
            Need(Path.IsPathRooted(path)&&path.Length>3&&path[1]==':'&&Path.GetFullPath(path)==path,"canonical local path");
            Need((string)src["approvalDisposition"]=="pilot-quarantine"&&!String.IsNullOrWhiteSpace((string)src["revision"]),"pilot provenance");
            Provenance=new SourceProvenance(MapId, path, (string)src["revision"], (string)src["sha256"], manifestHash, L(src["length"]));
            var policy=D(input["policy"]);Need((string)policy["collisionRole"]=="UNKNOWN"&&!(bool)policy["playableApproval"],"quarantine policy");
            if((bool)policy["indoor"])Need(policy["capsules"] is bool&&!(bool)policy["capsules"],"indoor capsule prohibition");
            file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            try {
                Need(file.Length==L(src["length"]),"source length");using(var h=SHA256.Create())Need(Hex(h.ComputeHash(file))==(string)src["sha256"],"source hash");file.Position=0;
                var reader=new BinaryReader(file,Encoding.UTF8,true);
                Need(reader.ReadUInt32()==0x46546c67&&reader.ReadUInt32()==2&&reader.ReadUInt32()==file.Length,"GLB header");
                int size=checked((int)reader.ReadUInt32());Need(size>=2&&size<=8388608&&size%4==0&&reader.ReadUInt32()==0x4e4f534a,"GLB JSON");
                byte[] raw=reader.ReadBytes(size);Need(raw.Length==size&&Hash(raw)==(string)src["jsonSha256"],"JSON identity");doc=D(Parse(raw));
                binLength=reader.ReadUInt32();Need(reader.ReadUInt32()==0x004e4942,"GLB BIN");binOffset=file.Position;
                Need(binOffset+binLength==file.Length&&binOffset==L(src["binOffset"])&&binLength==L(src["binLength"]),"BIN range");
                var refs=D(input["references"]);foreach(string key in new[]{"nodes","scenes","meshes","accessors","bufferViews","buffers","materials","textures","images","samplers"})
                    Need(Equal(refs[key],doc.ContainsKey(key)?doc[key]:new object[0]),"source metadata mismatch: "+key);
                if(doc.ContainsKey("extensionsRequired"))foreach(object e in A(doc["extensionsRequired"]))Need((string)e=="KHR_mesh_quantization","unsupported required extension");
                var buffers=A(doc["buffers"]);Need(buffers.Length==1&&!D(buffers[0]).ContainsKey("uri")&&N(D(buffers[0]),"byteLength",-1)<=binLength&&binLength-N(D(buffers[0]),"byteLength",-1)<=3,"embedded buffer");
                foreach(object view in A(doc["bufferViews"])) {var v=D(view);Need(N(v,"buffer",-1)==0&&N(v,"byteOffset",0)>=0&&N(v,"byteLength",0)>0&&checked(N(v,"byteOffset",0)+N(v,"byteLength",0))<=N(D(buffers[0]),"byteLength",-1),"bufferView range");}
                var ranges=A(input["accessorRanges"]);Need(ranges.Length==A(doc["accessors"]).Length,"accessor coverage");
                for(int i=0;i<ranges.Length;i++)ValidateAccessor(i,D(ranges[i]));
                var visited=new HashSet<int>();foreach(object root in A(D(A(doc["scenes"])[N(doc,"scene",0)])["nodes"]))Walk(I(root),Identity(),visited,0);
                Need(visited.Count==A(doc["nodes"]).Length,"unreachable nodes explicitly unsupported");
                var rows=A(input["instances"]);Need(rows.Length==instances.Count,"primitive coverage");
                for(int i=0;i<rows.Length;i++) {
                    var row=D(rows[i]);var inst=instances[i];Need(N(row,"node",-1)==inst.Node&&N(row,"mesh",-1)==inst.Mesh&&N(row,"primitive",-1)==inst.Primitive,"numeric primitive identity");Need((string)row["id"]==inst.Id&&(string)row["collisionRole"]=="UNKNOWN","primitive identity/role");
                    var m=A(row["matrix"]);Need(m.Length==16,"matrix size");for(int j=0;j<16;j++)Need(Math.Abs(F(m[j])-inst.M[j])<1e-10,"hierarchy matrix mismatch");
                    Need(Equal(row["attributes"],inst.P["attributes"])&&N(row,"indices",-1)==N(inst.P,"indices",-1)&&N(row,"material",-1)==N(inst.P,"material",-1),"primitive references");
                }
                // Immutable source handle: preflight every normal before any sink publication.
                // No geometry or normal cache is retained; the 64 KiB page budget is unchanged.
                var checkNormal=new double[3];foreach(var inst in instances) {
                    var at=D(inst.P["attributes"]);if(!at.ContainsKey("NORMAL"))continue;
                    int ai=I(at["NORMAL"]);long count=N(D(A(doc["accessors"])[ai]),"count",0);var basis=ScaledBasis(inst.M);
                    for(long vi=0;vi<count;vi++)Normal(basis,Read(ai,vi,"VEC3"),checkNormal,0);
                }
            } catch {file.Dispose();throw;}
        }
        void ValidateAccessor(int index,Dictionary<string,object> range)
        {
            var a=D(A(doc["accessors"])[index]);Need(!a.ContainsKey("sparse")&&a.ContainsKey("bufferView"),"sparse/implicit accessor unsupported");
            var v=D(A(doc["bufferViews"])[I(a["bufferView"])]);int width=Width(I(a["componentType"])),count=Components((string)a["type"]);long stride=N(v,"byteStride",width*count),n=N(a,"count",0),ao=N(a,"byteOffset",0),vo=N(v,"byteOffset",0),vl=N(v,"byteLength",0);
            Need(N(v,"buffer",-1)==0&&n>0&&n<=100000000&&ao>=0&&vo>=0&&vl>=0&&stride>=width*count&&stride<=252&&stride%width==0&&(vo+ao)%width==0,"accessor layout");
            Need(checked(ao+(n-1)*stride+width*count)<=vl&&checked(vo+vl)<=N(D(A(doc["buffers"])[0]),"byteLength",-1),"accessor range");
            Need(N(range,"id",-1)==index&&N(range,"offset",-1)==binOffset+vo+ao&&N(range,"viewOffset",-1)==binOffset+vo&&N(range,"viewLength",-1)==vl&&N(range,"componentType",0)==N(a,"componentType",0)&&N(range,"count",0)==n&&(string)range["type"]==(string)a["type"]&&N(range,"byteStride",0)==N(v,"byteStride",0)&&Equal(range["normalized"],a.ContainsKey("normalized")?a["normalized"]:false),"range identity");
        }
        static int Width(int t) { switch(t){case 5120:case 5121:return 1;case 5122:case 5123:return 2;case 5125:case 5126:return 4;default:throw new InvalidDataException("component type");} }
        static int Components(string t) { switch(t){case "SCALAR":return 1;case "VEC2":return 2;case "VEC3":return 3;case "VEC4":return 4;default:throw new InvalidDataException("accessor shape");} }
        static double[] Identity() {return new double[]{1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1};}
        static double[] Mul(double[] a,double[] b) {var c=new double[16];for(int j=0;j<4;j++)for(int i=0;i<4;i++)for(int k=0;k<4;k++)c[j*4+i]+=a[k*4+i]*b[j*4+k];return c;}
        static double[] Vec(Dictionary<string,object> n,string k,double[] fallback) {if(!n.ContainsKey(k))return fallback;var a=A(n[k]);Need(a.Length==fallback.Length,"TRS size");var x=new double[a.Length];for(int i=0;i<x.Length;i++)x[i]=F(a[i]);return x;}
        void Walk(int index,double[] parent,HashSet<int> seen,int depth)
        {
            Need(depth<128&&seen.Add(index),"cycle/shared node/depth");var n=D(A(doc["nodes"])[index]);double[] m;
            if(n.ContainsKey("matrix")) {Need(!n.ContainsKey("translation")&&!n.ContainsKey("rotation")&&!n.ContainsKey("scale"),"matrix with TRS");m=Vec(n,"matrix",Identity());}
            else {
                var t=Vec(n,"translation",new double[3]);var s=Vec(n,"scale",new double[]{1,1,1});var q=Vec(n,"rotation",new double[]{0,0,0,1});double x=q[0],y=q[1],z=q[2],w=q[3];Need(Math.Abs(x*x+y*y+z*z+w*w-1)<1e-5,"quaternion");
                m=new double[]{(1-2*(y*y+z*z))*s[0],2*(x*y+z*w)*s[0],2*(x*z-y*w)*s[0],0,2*(x*y-z*w)*s[1],(1-2*(x*x+z*z))*s[1],2*(y*z+x*w)*s[1],0,2*(x*z+y*w)*s[2],2*(y*z-x*w)*s[2],(1-2*(x*x+y*y))*s[2],0,t[0],t[1],t[2],1};
            }
            m=Mul(parent,m);foreach(double f in m)F(f);Need(m[3]==0&&m[7]==0&&m[11]==0&&m[15]==1,"affine matrix");ScaledBasis(m);
            if(n.ContainsKey("mesh")) {int mesh=I(n["mesh"]);var ps=A(D(A(doc["meshes"])[mesh])["primitives"]);for(int j=0;j<ps.Length;j++) {
                var p=D(ps[j]);Need(N(p,"mode",4)==4&&!p.ContainsKey("targets"),"triangles only; no morphs");Need(!n.ContainsKey("skin"),"skin unsupported");var at=D(p["attributes"]);Need(at.ContainsKey("POSITION"),"position required");
                instances.Add(new Instance {Id="node/"+index+"/mesh/"+mesh+"/primitive/"+j,M=m,P=p,Node=index,Mesh=mesh,Primitive=j});
            }}
            if(n.ContainsKey("children"))foreach(object c in A(n["children"]))Walk(I(c),m,seen,depth+1);
        }
        // Normalize columns independently: uniform scaling would underflow anisotropic axes.
        static double[] ScaledBasis(double[] m)
        {
            var b=new double[12];for(int j=0;j<3;j++) {double scale=Math.Max(Math.Abs(m[j*4]),Math.Max(Math.Abs(m[j*4+1]),Math.Abs(m[j*4+2])));Need(scale>0,"singular matrix");b[9+j]=scale;for(int i=0;i<3;i++)b[j*3+i]=m[j*4+i]/scale;}
            double det=b[0]*(b[4]*b[8]-b[7]*b[5])-b[3]*(b[1]*b[8]-b[7]*b[2])+b[6]*(b[1]*b[5]-b[4]*b[2]);
            Need(Math.Abs(det)>1e-14,"singular/unsupported conditioning");return b;
        }
        static double Sign(double[] b) {return b[0]*(b[4]*b[8]-b[7]*b[5])-b[3]*(b[1]*b[8]-b[7]*b[2])+b[6]*(b[1]*b[5]-b[4]*b[2]);}
        // Absolute-error balls. Inputs are the actual decoded binary64 values.
        // 4 ulps per operation covers roundoff and gradual underflow conservatively.
        struct Ball
        {
            internal double V,E;
            internal Ball(double v,double e) {V=v;E=e;}
        }
        const double Roundoff=8.8817841970012523e-16;
        static Ball Rounded(double v,double e) {return new Ball(v,(e+Math.Abs(v)*Roundoff+Double.Epsilon*4)*(1+Roundoff));}
        static Ball Add(Ball a,Ball b) {return Rounded(a.V+b.V,a.E+b.E);}
        static Ball Neg(Ball a) {return new Ball(-a.V,a.E);}
        static Ball Times(Ball a,Ball b) {return Rounded(a.V*b.V,Math.Abs(a.V)*b.E+Math.Abs(b.V)*a.E+a.E*b.E);}
        // Exact power-of-two decomposition, including subnormals; no logarithms.
        static double Mantissa(double value,out int exponent)
        {
            long bits=BitConverter.DoubleToInt64Bits(Math.Abs(value));int e=(int)((bits>>52)&2047);
            if(e==0) {double v=Mantissa(value*18014398509481984.0,out exponent);exponent-=54;return v;}
            exponent=e-1023;return BitConverter.Int64BitsToDouble((bits&0x000fffffffffffffL)|0x3ff0000000000000L);
        }
        static void Normal(double[] b,double[] n,double[] target,int offset)
        {
            if(n[0]==0&&n[1]==0&&n[2]==0)return; // Preserve source zero exactly.
            var w=new Ball[3];var exponents=new int[3];int largest=Int32.MinValue;
            for(int i=0;i<3;i++)if(n[i]!=0) {
                int en,es;double mn=Mantissa(n[i],out en),ms=Mantissa(b[9+i],out es);
                exponents[i]=en-es;largest=Math.Max(largest,exponents[i]);w[i]=Rounded(Math.Sign(n[i])*mn/ms,0);
            }
            for(int i=0;i<3;i++)if(n[i]!=0) {
                int shift=exponents[i]-largest;
                // A discarded term is bounded rather than silently treated as exact zero.
                if(shift < -1022)w[i]=new Ball(0,4*2.2250738585072014e-308);
                else {double power=BitConverter.Int64BitsToDouble((long)(shift+1023)<<52);w[i]=Rounded(w[i].V*power,w[i].E*power);}
            }
            var basis=new Ball[9];for(int i=0;i<9;i++)basis[i]=Rounded(b[i],0);
            var result=new Ball[3];
            // Row i of cofactor(B), multiplied by n / columnScale.
            for(int i=0;i<3;i++) {
                int j=(i+1)%3,k=(i+2)%3;
                for(int c=0;c<3;c++) {
                    int d=(c+1)%3,e=(c+2)%3;
                    Ball cofactor=Add(Times(basis[d*3+j],basis[e*3+k]),Neg(Times(basis[e*3+j],basis[d*3+k])));
                    result[i]=Add(result[i],Times(cofactor,w[c]));
                }
            }
            double scale=0,error=0;for(int i=0;i<3;i++){scale=Math.Max(scale,Math.Abs(result[i].V));error=Math.Max(error,result[i].E);}
            // ||delta||2 <= sqrt(3)*error. Normalization changes direction by at most
            // 2*||delta||2/||computed||2, so this admits <4e-9 component error,
            // plus <1e-14 final normalization roundoff. It is an error-domain gate,
            // not a relaxed determinant threshold or a promise for every invertible input.
            Need(scale>0&&error<=scale*1e-9,"normal unsupported cancellation/error bound");
            double a=result[0].V/scale,cx=result[1].V/scale,dz=result[2].V/scale;
            double len=Math.Sqrt(a*a+cx*cx+dz*dz)*Math.Sign(Sign(b));target[offset]=F(a/len);target[offset+1]=F(cx/len);target[offset+2]=F(-dz/len);
        }
        byte Byte(long offset)
        {
            Need(offset>=binOffset&&offset<binOffset+binLength,"read outside BIN");
            if(offset<pageStart||offset>=pageStart+pageCount) {pageStart=offset/65536*65536;file.Position=pageStart;pageCount=file.Read(page,0,page.Length);}
            Need(offset<pageStart+pageCount,"short read");return page[(int)(offset-pageStart)];
        }
        double Component(long offset,int type,bool normalized)
        {
            ulong u=0;for(int i=0;i<Width(type);i++)u|=(ulong)Byte(offset+i)<<(8*i);double x;
            switch(type) {case 5120:x=unchecked((sbyte)u);break;case 5121:x=u;break;case 5122:x=unchecked((short)u);break;case 5123:case 5125:x=u;break;default:x=BitConverter.ToSingle(BitConverter.GetBytes((uint)u),0);break;}
            if(normalized) {Need(type!=5126&&type!=5125,"invalid normalization");x=Math.Max(-1,x/(type==5120?127:type==5121?255:type==5122?32767:65535));}return F(x);
        }
        double[] Read(int ai,long index,string shape)
        {
            var a=D(A(doc["accessors"])[ai]);Need((string)a["type"]==shape&&index>=0&&index<N(a,"count",0),"attribute shape/index");var v=D(A(doc["bufferViews"])[I(a["bufferView"])]);int type=I(a["componentType"]),width=Width(type),count=Components(shape);long offset=binOffset+N(v,"byteOffset",0)+N(a,"byteOffset",0)+index*N(v,"byteStride",width*count);var r=new double[count];for(int k=0;k<count;k++)r[k]=Component(offset+k*width,type,a.ContainsKey("normalized")&&(bool)a["normalized"]);return r;
        }
        long Index(Dictionary<string,object> p,long i) {if(!p.ContainsKey("indices"))return i;var a=D(A(doc["accessors"])[I(p["indices"])]);long type=N(a,"componentType",0);Need((type==5121||type==5123||type==5125)&&(!a.ContainsKey("normalized")||!(bool)a["normalized"]),"index encoding");return checked((long)Read(I(p["indices"]),i,"SCALAR")[0]);}
        // Caller owns sink retention. Loader allocates at most one <=256-triangle batch at a time.
        public void StreamAll(ITriangleSink sink,int batchTriangles)
        {
            if(disposed)throw new ObjectDisposedException("MapStream");Need(!streaming&&sink!=null&&batchTriangles>=1&&batchTriangles<=256,"stream state/budget");streaming=true;
            try {foreach(var inst in instances) {
                var p=inst.P;var at=D(p["attributes"]);int pos=I(at["POSITION"]);foreach(string key in new[]{"NORMAL","TEXCOORD_0"})if(at.ContainsKey(key))Need(N(D(A(doc["accessors"])[I(at[key])]),"count",0)==N(D(A(doc["accessors"])[pos]),"count",0),"attribute count mismatch");long count=N(D(A(doc["accessors"])[p.ContainsKey("indices")?I(p["indices"]):pos]),"count",0);Need(count%3==0,"triangle remainder");var m=inst.M;var basis=ScaledBasis(m);double det=Sign(basis);
                for(long start=0;start<count;start+=batchTriangles*3) {
                    int corners=(int)Math.Min(count-start,batchTriangles*3);var xyz=new double[corners*3];var normal=at.ContainsKey("NORMAL")?new double[corners*3]:null;var uv=at.ContainsKey("TEXCOORD_0")?new double[corners*2]:null;
                    for(int k=0;k<corners;k++) {
                        long ordinal=start+k; if(det>0)ordinal=start+(k/3)*3+(k%3==0?0:k%3==1?2:1);long vi=Index(p,ordinal);var v=Read(pos,vi,"VEC3");
                        xyz[k*3]=F(m[0]*v[0]+m[4]*v[1]+m[8]*v[2]+m[12]);xyz[k*3+1]=F(m[1]*v[0]+m[5]*v[1]+m[9]*v[2]+m[13]);xyz[k*3+2]=F(-(m[2]*v[0]+m[6]*v[1]+m[10]*v[2]+m[14]));
                        if(normal!=null) {
                            Normal(basis,Read(I(at["NORMAL"]),vi,"VEC3"),normal,k*3);
                        }
                        if(uv!=null) {var t=Read(I(at["TEXCOORD_0"]),vi,"VEC2");uv[k*2]=t[0];uv[k*2+1]=t[1];}
                    }
                    sink.Accept(inst.Id,new TriangleBatch(xyz,normal,uv,corners/3));
                }
            }} finally {streaming=false;}
        }
        public void Dispose() {Need(!streaming,"cannot dispose during callback");if(!disposed){disposed=true;file.Dispose();}}
    }
}


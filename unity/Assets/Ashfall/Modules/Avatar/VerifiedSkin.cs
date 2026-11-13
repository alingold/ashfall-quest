using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
namespace Ashfall.Modules.Avatar
{
    public sealed class VerifiedSkin
    {
        public readonly string AssetSha256, ClosureSha256, Node, MaterialSlots;
        public readonly int MeshNode;
        public int NodeCount { get { return local.Length; } }
        readonly float[] vertices,normals,uv,weights; readonly int[] influences,indices,joints,parents;
        readonly string[] names; readonly float[][] local,bind;
        internal VerifiedSkin(PreparedPart p,string closure,int mesh,float[] v,float[] n,float[] u,float[] w,int[] inf,int[] ix,int[] j,int[] par,string[] nm,float[][] l,float[][] b) {
            AssetSha256=p.Asset.Sha256.ToUpperInvariant();ClosureSha256=closure;Node=p.Node;MaterialSlots=p.MaterialSlots;MeshNode=mesh;
            vertices=v;normals=n;uv=u;weights=w;influences=inf;indices=ix;joints=j;parents=par;names=nm;local=l;bind=b;
        }
        internal static float[][] Clone(float[][] x) { SkinJson.Check(x!=null,"Matrix array");var b=new float[x.Length][];for(int i=0;i<x.Length;i++){float[] m=x[i];SkinJson.Check(m!=null&&m.Length==16,"Matrix shape");b[i]=(float[])m.Clone();}return b; }
        public float[] CopyVertices(){return (float[])vertices.Clone();} public float[] CopyNormals(){return (float[])normals.Clone();}
        public float[] CopyUv(){return (float[])uv.Clone();} public float[] CopyWeights(){return (float[])weights.Clone();}
        public int[] CopyInfluences(){return (int[])influences.Clone();} public int[] CopyIndices(){return (int[])indices.Clone();}
        public int[] CopyJoints(){return (int[])joints.Clone();} public int[] CopyParents(){return (int[])parents.Clone();}
        public string[] CopyNames(){return (string[])names.Clone();} public float[][] CopyLocalMatrices(){return Clone(local);}
        public float[][] CopyBindMatrices(){return Clone(bind);}
        public void RequireBinding(PreparedPart p) { SkinJson.Check(p!=null&&p.Asset.Sha256.ToUpperInvariant()==AssetSha256&&p.Node==Node&&p.MaterialSlots==MaterialSlots,"Skin substitution"); }
        public float[][] WorldMatrices(float[][] pose) {
            SkinJson.Check(pose!=null&&pose.Length==local.Length,"Pose node count");pose=Clone(pose);var world=new float[pose.Length][];var visiting=new bool[pose.Length];
            for(int i=0;i<pose.Length;i++)SkinJson.Check(AvatarMath.ValidTrs(pose[i]),"Pose bounded TRS");
            for(int i=0;i<pose.Length;i++)World(i,pose,world,visiting);return world;
        }
        void World(int i,float[][] pose,float[][] result,bool[] seen) { if(result[i]!=null)return;SkinJson.Check(!seen[i],"Pose cycle");seen[i]=true;if(parents[i]<0)result[i]=(float[])pose[i].Clone();else{World(parents[i],pose,result,seen);result[i]=SkinMatrix.Multiply(result[parents[i]],pose[i]);} }
        // Linear blend skinning in mesh-local coordinates, same matrices supplied to Unity.
        public float[] Deform(float[][] pose) {
            var world=WorldMatrices(pose);var inv=SkinMatrix.Inverse(world[MeshNode]);var palette=new float[joints.Length][];
            for(int i=0;i<joints.Length;i++)palette[i]=SkinMatrix.Multiply(SkinMatrix.Multiply(inv,world[joints[i]]),bind[i]);
            var output=new float[vertices.Length];for(int v=0;v<vertices.Length/3;v++)for(int k=0;k<4;k++){
                var m=palette[influences[v*4+k]];double w=weights[v*4+k];for(int r=0;r<3;r++)output[v*3+r]+=(float)(w*(m[r*4]*vertices[v*3]+m[r*4+1]*vertices[v*3+1]+m[r*4+2]*vertices[v*3+2]+m[r*4+3]));
            } foreach(float f in output)SkinJson.Check(AvatarMath.Bounded(f,AvatarMath.MaxPosition),"Deformed bounds");return output;
        }
    }
    public static class VerifiedSkinReader
    {
        public const int MaxBytes=16*1024*1024;
        static void Need(bool b,string m){SkinJson.Check(b,m);}
        static Dictionary<string,object> Obj(object x){var d=x as Dictionary<string,object>;Need(d!=null,"Expected object");return d;}
        static object Get(Dictionary<string,object> d,string k,object fallback=null){object x;return d.TryGetValue(k,out x)?x:fallback;}
        static object[] Arr(object x){var a=x as object[];Need(a!=null&&a.Length<=131070,"Array budget/type");return a;}
        static string Str(object x){Need(x is string,"Expected string");return (string)x;}
        static int Int(object x){Need(x is double && (double)x>=0 && (double)x<=Int32.MaxValue && (double)x==Math.Floor((double)x),"Unsigned integer");return (int)(double)x;}
        static int I(Dictionary<string,object> d,string k,int fallback=0){return Int(Get(d,k,(double)fallback));}
        static Dictionary<string,object> At(object[] a,int i){Need(i>=0&&i<a.Length,"Index bounds");return Obj(a[i]);}
        static float F(object x){Need(x is double&&Math.Abs((double)x)<=AvatarMath.MaxPosition,"Finite bounded scalar");return (float)(double)x;}
        static float[] Vec(object x,int n){var a=Arr(x);Need(a.Length==n,"Vector width");var b=new float[n];for(int i=0;i<n;i++)b[i]=F(a[i]);return b;}
        static byte[] Dependency(string uri,ISkinDependencies deps) {
            Need(uri!=null&&uri.Length<MaxBytes*2,"URI budget");
            if(uri.StartsWith("data:",StringComparison.Ordinal)) {
                int comma=uri.IndexOf(',');Need(comma>0,"Data URI");string kind=uri.Substring(0,comma);
                Need(kind=="data:application/octet-stream;base64"||kind=="data:application/gltf-buffer;base64"||kind=="data:image/png;base64"||kind=="data:image/jpeg;base64","Data MIME");
                Need(uri.Length-comma-1<=(MaxBytes/3+1)*4,"Base64 budget");try{return Convert.FromBase64String(uri.Substring(comma+1));}catch(FormatException){throw new AvatarValidationException("Base64 encoding");}
            }
            Need(uri.Length>0&&uri.Length<1024,"External URI size");
            foreach(char c in uri)Need(Char.IsLetterOrDigit(c)||c=='_'||c=='-'||c=='.'||c=='/',"External URI characters");
            Need(!uri.StartsWith("/",StringComparison.Ordinal)&&!uri.Contains("//"),"Absolute URI");foreach(string seg in uri.Split('/'))Need(seg!="."&&seg!=".."&&seg.Length>0,"URI traversal");
            Need(deps!=null,"Missing immutable dependency ledger");VerifiedAsset asset=deps.Resolve(uri);Need(asset!=null&&asset.ByteLength<=MaxBytes,"Missing/big dependency");byte[] b=asset.CopyBytes();Need(ManifestXml.Sha(b)==asset.Sha256.ToUpperInvariant(),"Dependency hash");return b;
        }
        sealed class Access
        {
            internal object[] access,views;internal byte[][] buffers;
            internal float[] Read(int id,string type,int component=-1) {
                var a=At(access,id);Need(!a.ContainsKey("sparse"),"Sparse unsupported");Need(Str(Get(a,"type"))==type,"Accessor type");
                int width=type=="MAT4"?16:type=="VEC4"?4:type=="VEC3"?3:type=="VEC2"?2:1;
                Need(a.ContainsKey("bufferView")&&a.ContainsKey("componentType")&&a.ContainsKey("count"),"Accessor required fields");int ct=I(a,"componentType");Need(component<0||component==ct,"Accessor component");int size=ct==5121?1:ct==5123?2:ct==5125||ct==5126?4:0;Need(size>0,"Component unsupported");
                object normalization=Get(a,"normalized",false);Need(normalization is bool,"Normalized boolean");bool normalized=(bool)normalization;Need(!normalized||ct==5121||ct==5123,"Normalized type");
                int count=I(a,"count");Need(count>0&&count<=65535,"Accessor count");var view=At(views,I(a,"bufferView"));int bi=I(view,"buffer");Need(bi<buffers.Length,"Buffer index");var b=buffers[bi];
                int offset=I(a,"byteOffset"), start=I(view,"byteOffset"), length=I(view,"byteLength"), stride=I(view,"byteStride",width*size);
                Need(start%size==0&&offset%size==0&&stride>=width*size&&stride<=252&&stride%size==0,"Accessor alignment/stride");
                Need((long)start+length<=b.Length&&(long)offset+(long)(count-1)*stride+width*size<=length,"Accessor range");
                var result=new float[count*width];for(int i=0;i<count;i++)for(int k=0;k<width;k++){
                    int pos=start+offset+i*stride+k*size;double v=ct==5121?b[pos]:ct==5123?BitConverter.ToUInt16(b,pos):ct==5125?BitConverter.ToUInt32(b,pos):BitConverter.ToSingle(b,pos);
                    if(normalized)v/=ct==5121?255:65535;Need(!Double.IsNaN(v)&&!Double.IsInfinity(v)&&Math.Abs(v)<=AvatarMath.MaxPosition,"Accessor numeric bounds");result[i*width+k]=(float)v;
                }return result;
            }
        }
        public static VerifiedSkin Read(PreparedPart part,ISkinDependencies dependencies=null) {
            Need(part!=null&&part.Asset.ByteLength<=MaxBytes,"Skin source budget");byte[] source=part.Asset.CopyBytes();Need(ManifestXml.Sha(source)==part.Asset.Sha256.ToUpperInvariant(),"Source hash");
            byte[] json=source,bin=null;
            if(source.Length>=4&&BitConverter.ToUInt32(source,0)==0x46546c67) {
                Need(source.Length>=20&&BitConverter.ToUInt32(source,4)==2&&BitConverter.ToUInt32(source,8)==source.Length,"GLB header");int p=12;var chunks=new List<byte[]>();
                while(p<source.Length){Need(source.Length-p>=8,"GLB chunk header");uint n=BitConverter.ToUInt32(source,p),t=BitConverter.ToUInt32(source,p+4);Need(n%4==0&&n<=source.Length-p-8,"GLB chunk range");Need(chunks.Count<2&&(chunks.Count==0?t==0x4e4f534a:t==0x004e4942),"GLB chunk order/type");var b=new byte[(int)n];Buffer.BlockCopy(source,p+8,b,0,b.Length);chunks.Add(b);p+=8+b.Length;}
                Need(chunks.Count>0,"GLB JSON");json=chunks[0];if(chunks.Count==2)bin=chunks[1];
            }
            var doc=SkinJson.Read(json);Need(Str(Get(Obj(Get(doc,"asset")),"version"))=="2.0","glTF version");Need(!doc.ContainsKey("extensionsRequired")||Arr(doc["extensionsRequired"]).Length==0,"Required extensions unsupported");
            var bufs=Arr(Get(doc,"buffers"));Need(bufs.Length>0&&bufs.Length<=32,"Buffer count");var buffers=new byte[bufs.Length][];long total=source.Length;
            using(var closure=new MemoryStream())using(var writer=new BinaryWriter(closure)) {
                writer.Write(part.Asset.Sha256.ToUpperInvariant());
                for(int i=0;i<bufs.Length;i++){var d=At(bufs,i);string uri=d.ContainsKey("uri")?Str(d["uri"]):null;byte[] b=uri==null?bin:Dependency(uri,dependencies);int n=I(d,"byteLength");Need(b!=null&&n>0&&b.Length>=n&&b.Length-n<=((uri==null)?3:0),"Buffer byteLength");total+=b.Length;Need(total<=MaxBytes,"Closure byte budget");buffers[i]=new byte[n];Buffer.BlockCopy(b,0,buffers[i],0,n);writer.Write(uri??"GLB:BIN");writer.Write(ManifestXml.Sha(b));}
                // Capture texture dependencies even though this candidate renderer uses neutral material.
                object imgs=Get(doc,"images");if(imgs!=null)foreach(object img in Arr(imgs)){var d=Obj(img);string uri=d.ContainsKey("uri")?Str(d["uri"]):null;if(uri!=null){var b=Dependency(uri,dependencies);total+=b.Length;Need(total<=MaxBytes,"Image closure budget");writer.Write(uri);writer.Write(ManifestXml.Sha(b));}else{var view=At(Arr(Get(doc,"bufferViews")),I(d,"bufferView"));int bi=I(view,"buffer"),offset=I(view,"byteOffset"),len=I(view,"byteLength");Need(bi<buffers.Length&&(long)offset+len<=buffers[bi].Length,"Image view range");}}
                var a=new Access{access=Arr(Get(doc,"accessors")),views=Arr(Get(doc,"bufferViews")),buffers=buffers};
                var nodes=Arr(Get(doc,"nodes"));Need(nodes.Length>0&&nodes.Length<=256,"Node budget");var local=new float[nodes.Length][];var parents=new int[nodes.Length];var names=new string[nodes.Length];int meshNode=-1;
                for(int i=0;i<nodes.Length;i++)parents[i]=-1;
                for(int i=0;i<nodes.Length;i++){
                    var d=At(nodes,i);names[i]=Get(d,"name","node-"+i) as string;Need(names[i]!=null&&names[i].Length<=128,"Node name");if(names[i]==part.Node){Need(meshNode<0,"Ambiguous node binding");meshNode=i;}
                    float[] m;if(d.ContainsKey("matrix")){Need(!d.ContainsKey("translation")&&!d.ContainsKey("rotation")&&!d.ContainsKey("scale"),"Matrix/TRS conflict");var col=Vec(d["matrix"],16);m=new float[16];for(int r=0;r<4;r++)for(int c=0;c<4;c++)m[r*4+c]=col[c*4+r];}
                    else {var t=Vec(Get(d,"translation",new object[]{0d,0d,0d}),3);var q=Vec(Get(d,"rotation",new object[]{0d,0d,0d,1d}),4);var s=Vec(Get(d,"scale",new object[]{1d,1d,1d}),3);double norm=0;foreach(float v in q)norm+=(double)v*v;Need(Math.Abs(norm-1)<0.00001,"Quaternion unit");float x=q[0],y=q[1],z=q[2],w=q[3];m=SkinMatrix.Identity();m[0]=1-2*(y*y+z*z);m[1]=2*(x*y-z*w);m[2]=2*(x*z+y*w);m[4]=2*(x*y+z*w);m[5]=1-2*(x*x+z*z);m[6]=2*(y*z-x*w);m[8]=2*(x*z-y*w);m[9]=2*(y*z+x*w);m[10]=1-2*(x*x+y*y);for(int r=0;r<3;r++){for(int c=0;c<3;c++)m[r*4+c]*=s[c];m[r*4+3]=t[r];}}
                    Need(AvatarMath.ValidTrs(m),"Node bounded TRS");local[i]=SkinMatrix.Reflect(m);
                    if(d.ContainsKey("children"))foreach(object child in Arr(d["children"])){int id=Int(child);Need(id<nodes.Length&&id!=i&&parents[id]==-1,"Hierarchy child/duplicate parent");parents[id]=i;}
                }
                Need(meshNode>=0,"Missing mesh node");for(int i=0;i<nodes.Length;i++){int steps=0;for(int j=i;j>=0;j=parents[j])Need(++steps<=nodes.Length,"Hierarchy cycle");}
                var nd=At(nodes,meshNode);var skin=At(Arr(Get(doc,"skins")),I(nd,"skin",Int32.MaxValue));var ja=Arr(Get(skin,"joints"));Need(ja.Length>0&&ja.Length<=128,"Joint count");var joints=new int[ja.Length];var unique=new HashSet<int>();for(int i=0;i<joints.Length;i++){joints[i]=Int(ja[i]);Need(joints[i]<nodes.Length&&unique.Add(joints[i]),"Joint identity");}
                if(skin.ContainsKey("skeleton")){int root=I(skin,"skeleton");Need(root<nodes.Length,"Skeleton root");foreach(int joint in joints){int j=joint;while(j>=0&&j!=root)j=parents[j];Need(j==root,"Skeleton root containment");}}
                var bind=new float[joints.Length][];float[] bm=skin.ContainsKey("inverseBindMatrices")?a.Read(I(skin,"inverseBindMatrices"),"MAT4",5126):null;Need(bm==null||bm.Length==16*joints.Length,"Bind count");for(int i=0;i<bind.Length;i++){var m=SkinMatrix.Identity();if(bm!=null)for(int r=0;r<4;r++)for(int c=0;c<4;c++)m[r*4+c]=bm[i*16+c*4+r];Need(m[12]==0&&m[13]==0&&m[14]==0&&m[15]==1,"Affine bind");SkinMatrix.Inverse(m);bind[i]=SkinMatrix.Reflect(m);}
                var mesh=At(Arr(Get(doc,"meshes")),I(nd,"mesh",Int32.MaxValue));Need(!mesh.ContainsKey("weights"),"Morph unsupported");var primitives=Arr(Get(mesh,"primitives"));Need(primitives.Length==1,"Single primitive skin required");var prim=At(primitives,0);Need(I(prim,"mode",4)==4&&!prim.ContainsKey("targets"),"Triangle skin only");var attrs=Obj(Get(prim,"attributes"));foreach(string required in new[]{"POSITION","NORMAL","TEXCOORD_0","JOINTS_0","WEIGHTS_0"})Need(attrs.ContainsKey(required),"Missing skin attribute: "+required);Need(prim.ContainsKey("indices"),"Missing skin indices");Need(!attrs.ContainsKey("JOINTS_1")&&!attrs.ContainsKey("WEIGHTS_1"),"More than four influences unsupported");
                string material="default";if(prim.ContainsKey("material"))material=Str(Get(At(Arr(Get(doc,"materials")),I(prim,"material")),"name"));Need(material==part.MaterialSlots,"Material substitution");
                var vertices=a.Read(I(attrs,"POSITION"),"VEC3",5126);var normals=a.Read(I(attrs,"NORMAL"),"VEC3",5126);var uv=a.Read(I(attrs,"TEXCOORD_0"),"VEC2",5126);var inf=a.Read(I(attrs,"JOINTS_0"),"VEC4");var weights=a.Read(I(attrs,"WEIGHTS_0"),"VEC4");int count=vertices.Length/3;
                int jointType=I(At(a.access,I(attrs,"JOINTS_0")),"componentType");Need(jointType==5121||jointType==5123,"Joint component type");Need(!Object.Equals(Get(At(a.access,I(attrs,"JOINTS_0")),"normalized",false),true),"Joint normalized");int wt=I(At(a.access,I(attrs,"WEIGHTS_0")),"componentType");Need(wt==5126||((wt==5121||wt==5123)&&Object.Equals(Get(At(a.access,I(attrs,"WEIGHTS_0")),"normalized"),true)),"Weight component/normalization");
                Need(normals.Length==vertices.Length&&uv.Length==count*2&&inf.Length==count*4&&weights.Length==inf.Length,"Attribute count");var influences=new int[inf.Length];
                for(int v=0;v<count;v++){double sum=0,norm=0;for(int k=0;k<3;k++)norm+=(double)normals[v*3+k]*normals[v*3+k];Need(Math.Abs(norm-1)<0.001,"Unit normals");vertices[v*3+2]*=-1;normals[v*3+2]*=-1;for(int k=0;k<4;k++){int j=v*4+k;Need(inf[j]>=0&&inf[j]<joints.Length&&inf[j]==Math.Floor(inf[j]),"Influence joint range");influences[j]=(int)inf[j];Need(weights[j]>=0&&weights[j]<=1,"Weight bounds");sum+=weights[j];}Need(sum>0&&Math.Abs(sum-1)<0.02,"Weight sum");for(int k=0;k<4;k++)weights[v*4+k]=(float)(weights[v*4+k]/sum);}
                var ix=a.Read(I(prim,"indices"),"SCALAR");int it=I(At(a.access,I(prim,"indices")),"componentType");Need((it==5121||it==5123||it==5125)&&!Object.Equals(Get(At(a.access,I(prim,"indices")),"normalized",false),true)&&ix.Length%3==0,"Triangle index type/count");var indices=new int[ix.Length];for(int i=0;i<ix.Length;i++){Need(ix[i]>=0&&ix[i]<count,"Index range");indices[i]=(int)ix[i];}for(int i=0;i<indices.Length;i+=3){int t=indices[i+1];indices[i+1]=indices[i+2];indices[i+2]=t;}
                writer.Flush();string closureHash=ManifestXml.Sha(closure.ToArray());if(dependencies!=null)Need(String.Equals(closureHash,dependencies.ExpectedClosureSha256,StringComparison.OrdinalIgnoreCase),"Closure receipt mismatch");var result=new VerifiedSkin(part,ManifestXml.Sha(closure.ToArray()),meshNode,vertices,normals,uv,weights,influences,indices,joints,parents,names,local,bind);result.WorldMatrices(local);result.RequireBinding(part);return result;
            }
        }
    }
}


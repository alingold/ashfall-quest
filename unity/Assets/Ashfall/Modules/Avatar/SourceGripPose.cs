using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
namespace Ashfall.Modules.Avatar
{
    public sealed class FrozenSkinPose : ISkinPose
    {
        readonly float[][] matrices;
        public FrozenSkinPose(VerifiedSkin skin,float[][] source) {
            SkinJson.Check(skin!=null,"Skin required");SkinJson.Check(source!=null&&source.Length==skin.NodeCount,"Pose node count");matrices=VerifiedSkin.Clone(source);skin.WorldMatrices(matrices);
        }
        public float[][] CopyLocalMatrices(){return VerifiedSkin.Clone(matrices);}
    }
    // Narrow Godot4 RSRC Animation rotation track decoder. No engine execution or resource loading.
    // Only the source one-key rotation_3d subset is admitted; arbitrary resources fail closed.
    public sealed class GripRotations
    {
        readonly Dictionary<string,float[]> rotations=new Dictionary<string,float[]>(StringComparer.Ordinal);
        public readonly string SourceSha256;
        public GripRotations(byte[] source,string expectedSha256) {
            SkinJson.Check(source!=null&&source.Length<=16384,"Grip resource budget");var b=(byte[])source.Clone();
            SourceSha256=ManifestXml.Sha(b);SkinJson.Check(SourceSha256==expectedSha256,"Grip resource identity");
            SkinJson.Check(b.Length>110&&Encoding.ASCII.GetString(b,0,4)=="RSRC"&&BitConverter.ToUInt32(b,4)==0&&BitConverter.ToUInt32(b,8)==0&&BitConverter.ToUInt32(b,12)==4,"Grip resource format");
            int pos=102;int count=(int)U(b,pos);pos+=4;SkinJson.Check(count>0&&count<=256,"Grip string budget");var strings=new string[count];
            for(int i=0;i<count;i++){int n=(int)U(b,pos);pos+=4;SkinJson.Check(n>0&&n<=256&&pos+n<=b.Length&&b[pos+n-1]==0,"Grip string");strings[i]=new UTF8Encoding(false,true).GetString(b,pos,n-1);pos+=n;}
            byte[] marker=Encoding.ASCII.GetBytes("rotation_3d\0");
            for(int i=pos;i+marker.Length<b.Length;i++) {
                bool match=true;for(int j=0;j<marker.Length;j++)if(b[i+j]!=marker[j])match=false;if(!match)continue;
                int start=i-12;SkinJson.Check(U(b,start+4)==5&&U(b,start+8)==12,"Grip type");int property=(int)U(b,start);
                SkinJson.Check(property>=0&&property+6<count&&strings[property].EndsWith("/type",StringComparison.Ordinal),"Grip property");
                SkinJson.Check(U(b,start+52)==22&&U(b,start+56)==65538&&U(b,start+60)==0,"Grip relative nodepath");
                int bone=(int)U(b,start+68);SkinJson.Check(bone>=0&&bone<count,"Grip bone string");
                SkinJson.Check(U(b,start+100)==33&&U(b,start+104)==6&&U(b,start+108)==0&&BitConverter.ToSingle(b,start+112)==1,"Grip single packed key");
                var q=new float[4];for(int k=0;k<4;k++)q[k]=BitConverter.ToSingle(b,start+116+k*4);Unit(q);
                SkinJson.Check(!rotations.ContainsKey(strings[bone]),"Duplicate grip bone");rotations.Add(strings[bone],q);
            }
            SkinJson.Check(rotations.Count==19,"Require source nineteen rotation tracks");
        }
        static uint U(byte[] b,int p){SkinJson.Check(p>=0&&p+4<=b.Length,"Grip bounds");return BitConverter.ToUInt32(b,p);}
        internal static void Unit(float[] q){double n=0;foreach(float x in q){SkinJson.Check(!Single.IsNaN(x)&&!Single.IsInfinity(x),"Grip finite quaternion");n+=(double)x*x;}SkinJson.Check(Math.Abs(n-1)<0.0001,"Grip unit quaternion");}
        internal bool Try(string name,out float[] q){return rotations.TryGetValue(name,out q);}
        internal IEnumerable<string> Names {get{return rotations.Keys;}}
    }
    public sealed class SourceGripPose
    {
        readonly VerifiedSkin skin;readonly GripRotations opened,grip,shaft,pistol;readonly string[] names;
        float[][] current;readonly Dictionary<int,float[]> rotation=new Dictionary<int,float[]>();
        public SourceGripPose(VerifiedSkin skin,GripRotations opened,GripRotations grip,GripRotations shaft,GripRotations pistol) {
            SkinJson.Check(skin!=null&&opened!=null&&grip!=null&&shaft!=null&&pistol!=null,"Grip inputs");this.skin=skin;this.opened=opened;this.grip=grip;this.shaft=shaft;this.pistol=pistol;names=skin.CopyNames();current=skin.CopyLocalMatrices();
            foreach(var set in new[]{opened,grip,shaft,pistol})foreach(string name in set.Names)SkinJson.Check(Array.IndexOf(names,name)>=0,"Grip skin bone mismatch");
            for(int i=0;i<names.Length;i++){float[] q;if(opened.Try(names[i],out q))rotation.Add(i,Quaternion(current[i]));}
        }
        public static float Closure(string id,string profile,bool index,float requested) {
            if(id=="matadors")return index?.80f:1.05f;
            if(profile=="sword"||id=="magnum"||id=="tec9")return index?.86f:1.12f;
            if(id=="knife")return index?.80f:1.05f;
            if(id=="px4"||id=="1911"||id=="mp9")return index?.62f:.85f;
            return index?.54f:.74f;
        }
        public FrozenSkinPose Update(float gripAmount,float trigger,string role,string profile,string id,float delta) {
            SkinJson.Check(Finite(gripAmount)&&Finite(trigger)&&Finite(delta)&&role!=null&&profile!=null&&id!=null,"Grip frame");
            var closed=profile=="sword"?shaft:(id=="px4"||id=="1911"||id=="tec9"||id=="mp9"||id=="magnum"||id=="matadors"?pistol:grip);
            float blend=(float)(1-Math.Exp(-Math.Max(delta,0)*22));var next=VerifiedSkin.Clone(current);var pending=new Dictionary<int,float[]>();
            foreach(var item in rotation){int i=item.Key;float[] end,start;if(!closed.Try(names[i],out end))continue;SkinJson.Check(opened.Try(names[i],out start),"Grip missing open");bool index=names[i].StartsWith("Index_",StringComparison.Ordinal);float amount=Math.Max(0,Math.Min(1,index?trigger:gripAmount));if(role!="free")amount=Closure(id,profile,index,amount);
                // Convert Godot quaternion to Unity by SRS, once: (-x,-y,z,w).
                var target=Slerp(Reflect(start),Reflect(end),amount);var q=Slerp(item.Value,target,blend);pending.Add(i,q);SetRotation(next[i],q);
            }
            var result=new FrozenSkinPose(skin,next);current=next;foreach(var pair in pending)rotation[pair.Key]=pair.Value;return result;
        }
        static bool Finite(float x){return !Single.IsNaN(x)&&!Single.IsInfinity(x);}
        static float[] Reflect(float[] q){return new[]{-q[0],-q[1],q[2],q[3]};}
        internal static float[] Slerp(float[] a,float[] b,float t){double dot=0;for(int i=0;i<4;i++)dot+=(double)a[i]*b[i];double sign=dot<0?-1:1;dot=Math.Min(1,Math.Abs(dot));double x=1-t,y=t;if(dot<.9995){double angle=Math.Acos(dot),s=Math.Sin(angle);x=Math.Sin((1-t)*angle)/s;y=Math.Sin(t*angle)/s;}var q=new float[4];double n=0;for(int i=0;i<4;i++){q[i]=(float)(x*a[i]+y*sign*b[i]);n+=(double)q[i]*q[i];}for(int i=0;i<4;i++)q[i]/=(float)Math.Sqrt(n);return q;}
        static void SetRotation(float[] m,float[] q){float x=q[0],y=q[1],z=q[2],w=q[3];var r=new[]{1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w),2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w),2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)};for(int c=0;c<3;c++){double scale=0;for(int k=0;k<3;k++)scale+=(double)m[k*4+c]*m[k*4+c];for(int k=0;k<3;k++)m[k*4+c]=r[k*3+c]*(float)Math.Sqrt(scale);}}
        static float[] Quaternion(float[] m){ // Matrix rotation extraction, branch stable at pi.
            var r=(float[])m.Clone();for(int c=0;c<3;c++){double n=0;for(int k=0;k<3;k++)n+=(double)m[k*4+c]*m[k*4+c];for(int k=0;k<3;k++)r[k*4+c]/=(float)Math.Sqrt(n);}float t=r[0]+r[5]+r[10];var q=new float[4];if(t>0){float s=(float)Math.Sqrt(t+1)*2;q[3]=s/4;q[0]=(r[9]-r[6])/s;q[1]=(r[2]-r[8])/s;q[2]=(r[4]-r[1])/s;}else{int i=r[0]>r[5]?(r[0]>r[10]?0:2):(r[5]>r[10]?1:2),j=(i+1)%3,k=(i+2)%3;float s=(float)Math.Sqrt(1+r[i*4+i]-r[j*4+j]-r[k*4+k])*2;q[i]=s/4;q[j]=(r[j*4+i]+r[i*4+j])/s;q[k]=(r[k*4+i]+r[i*4+k])/s;q[3]=(r[k*4+j]-r[j*4+k])/s;}return q;
        }
    }
}


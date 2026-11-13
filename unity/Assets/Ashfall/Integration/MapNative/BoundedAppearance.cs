using System;
using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;
using Ashfall.Modules.MapStreaming;

namespace Ashfall.Integration.MapNative
{
    // Expected identity is supplied by a trusted root receipt, never derived as approval.
    // Whole GLB hash is streamed with a fixed buffer; only the admitted slice is allocated.
    public sealed class VerifiedImageSlice
    {
        readonly string path,sha;readonly long fileLength,offset;readonly int length;
        public VerifiedImageSlice(string path,long fileLength,string sha,long offset,int length)
        {
            if(!Path.IsPathRooted(path)||Path.GetFullPath(path)!=path||fileLength<1||offset<0||length<1||length>16777216||offset>fileLength-length||sha==null||sha.Length!=64)throw new ArgumentException("image receipt bounds");
            this.path=path;this.fileLength=fileLength;this.sha=sha.ToLowerInvariant();this.offset=offset;this.length=length;
        }
        public string SourcePath {get{return path;}}
        public string FileSha256 {get{return sha;}} public long FileLength {get{return fileLength;}}
        public long Offset {get{return offset;}} public int Length {get{return length;}}
        public byte[] Read(int maxBytes)
        {
            if(length>maxBytes)throw new InvalidDataException("encoded dependency budget");
            using(var f=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)) {
                if(f.Length!=fileLength)throw new InvalidDataException("dependency length");
                using(var h=SHA256.Create())if(BitConverter.ToString(h.ComputeHash(f)).Replace("-","").ToLowerInvariant()!=sha)throw new InvalidDataException("dependency hash");
                f.Position=offset;var b=new byte[length];int n=0;while(n<b.Length){int got=f.Read(b,n,b.Length-n);if(got==0)throw new EndOfStreamException();n+=got;}
                if(f.Length!=fileLength)throw new InvalidDataException("dependency changed");return b;
            }
        }
    }
    public static class BoundedPng
    {
        static uint U(byte[] b,int p){return ((uint)b[p]<<24)|((uint)b[p+1]<<16)|((uint)b[p+2]<<8)|b[p+3];}
        static void Need(bool b){if(!b)throw new InvalidDataException("unsupported or corrupt bounded PNG");}
        public static long Inspect(byte[] b,int maxDimension,out int width,out int height)
        {
            Need(b!=null&&b.Length>=57&&b.Length<=16777216&&maxDimension>0&&maxDimension<=4096);
            byte[] sig={137,80,78,71,13,10,26,10};for(int i=0;i<8;i++)Need(b[i]==sig[i]);
            width=height=0;bool header=false,data=false,end=false;int p=8;
            while(p<b.Length) {
                Need(b.Length-p>=12);uint size=U(b,p);Need(size<=16777216&&size<=b.Length-p-12);int n=(int)size;
                string type=System.Text.Encoding.ASCII.GetString(b,p+4,4);uint crc=0xffffffff;
                for(int i=p+4;i<p+8+n;i++){crc^=b[i];for(int k=0;k<8;k++)crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0);}
                Need((crc^0xffffffff)==U(b,p+8+n));
                if(!header){Need(type=="IHDR"&&n==13);width=checked((int)U(b,p+8));height=checked((int)U(b,p+12));Need(width>0&&height>0&&width<=maxDimension&&height<=maxDimension&&b[p+16]==8&&(b[p+17]==2||b[p+17]==6)&&b[p+18]==0&&b[p+19]==0&&b[p+20]==0);header=true;}
                else if(type=="IDAT"){Need(!end);data=true;}
                else if(type=="IEND"){Need(n==0&&data&&p+12==b.Length);end=true;}
                else Need(false); // Reject profiles, interlace, palette, animation and hidden decoder paths.
                p+=n+12;
            }
            Need(end);return checked((long)width*height*4);
        }
    }
    // Serialized root control lane, synchronous revoke, terminal lifetime. No production approval.
    public sealed class PreviewLifetime : IDisposable
    {
        readonly int lane=System.Threading.Thread.CurrentThread.ManagedThreadId;
        public bool Live {get;private set;} public event Action Revoked;
        public PreviewLifetime(){Live=true;}
        public void Check(){if(System.Threading.Thread.CurrentThread.ManagedThreadId!=lane)throw new InvalidOperationException("preview control lane");if(!Live)throw new ObjectDisposedException("PreviewLifetime");}
        public void Dispose(){if(System.Threading.Thread.CurrentThread.ManagedThreadId!=lane)throw new InvalidOperationException("preview control lane");if(!Live)return;Live=false;var e=Revoked;Exception failure=null;if(e!=null)foreach(Action a in e.GetInvocationList())try{a();}catch(Exception ex){if(failure==null)failure=ex;}if(failure!=null)throw new InvalidOperationException("preview revoked; subscriber failed",failure);}
    }
}

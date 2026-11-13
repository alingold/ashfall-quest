using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
namespace Ashfall.Modules.Avatar
{
    // Strict bounded JSON, independent of editor or third-party serializers.
    internal sealed class SkinJson
    {
        readonly string s; int p, tokens;
        SkinJson(string text) { s=text; }
        internal static Dictionary<string,object> Read(byte[] b) {
            var r=new SkinJson(new UTF8Encoding(false,true).GetString(b));
            var v=r.Value(0) as Dictionary<string,object>; r.Space();
            Check(v!=null && r.p==r.s.Length,"JSON document"); return v;
        }
        internal static void Check(bool b,string m) { if(!b) throw new AvatarValidationException(m); }
        void Space() { while(p<s.Length && (s[p]==' '||s[p]=='\n'||s[p]=='\r'||s[p]=='\t')) p++; }
        char Take() { Check(p<s.Length,"Truncated JSON"); return s[p++]; }
        string Str() {
            Check(Take()=='"',"JSON string"); var b=new StringBuilder();
            while(true) { char c=Take(); if(c=='"') return b.ToString(); Check(c>=32,"JSON control");
                if(c=='\\') { c=Take(); switch(c) {
                    case '"': case '\\': case '/': break;
                    case 'b': c='\b'; break; case 'f': c='\f'; break; case 'n': c='\n'; break;
                    case 'r': c='\r'; break; case 't': c='\t'; break;
                    case 'u': Check(p+4<=s.Length,"JSON escape"); ushort n; Check(UInt16.TryParse(s.Substring(p,4),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out n),"JSON unicode"); p+=4;c=(char)n;break;
                    default: throw new AvatarValidationException("JSON escape");
                }} b.Append(c); Check(b.Length<=1048576,"JSON string budget");
            }
        }
        object Value(int depth) {
            Check(depth<64 && ++tokens<200000,"JSON complexity"); Space(); Check(p<s.Length,"JSON value"); char c=s[p];
            if(c=='"') return Str();
            if(c=='{' || c=='[') {
                p++; Space(); bool dict=c=='{'; char end=dict?'}':']';
                var d=new Dictionary<string,object>(StringComparer.Ordinal); var a=new List<object>();
                if(p<s.Length && s[p]==end) { p++; return dict?(object)d:a.ToArray(); }
                while(true) { Space(); if(dict) { string k=Str(); Space(); Check(Take()==':' && !d.ContainsKey(k),"JSON duplicate/key"); d.Add(k,Value(depth+1)); } else a.Add(Value(depth+1));
                    Space(); c=Take(); if(c==end) break; Check(c==',',"JSON delimiter"); }
                return dict?(object)d:a.ToArray();
            }
            foreach(string lit in new[]{"true","false","null"}) if(p+lit.Length<=s.Length && String.CompareOrdinal(s,p,lit,0,lit.Length)==0) { p+=lit.Length; return lit=="null"?null:(object)(lit=="true"); }
            int start=p; if(s[p]=='-') p++; Check(p<s.Length,"JSON number");
            if(s[p]=='0') p++; else { Check(s[p]>='1'&&s[p]<='9',"JSON number"); while(p<s.Length&&Char.IsDigit(s[p]))p++; }
            if(p<s.Length&&s[p]=='.') { p++; int q=p; while(p<s.Length&&Char.IsDigit(s[p]))p++; Check(q<p,"JSON fraction"); }
            if(p<s.Length&&(s[p]=='e'||s[p]=='E')) { p++; if(p<s.Length&&(s[p]=='+'||s[p]=='-'))p++; int q=p;while(p<s.Length&&Char.IsDigit(s[p]))p++;Check(q<p,"JSON exponent"); }
            double x; Check(Double.TryParse(s.Substring(start,p-start),NumberStyles.Float,CultureInfo.InvariantCulture,out x)&&!Double.IsInfinity(x)&&!Double.IsNaN(x),"JSON finite number"); return x;
        }
    }
    public static class SkinMatrix
    {
        public static float[] Identity() { return new float[]{1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1}; }
        public static float[] Multiply(float[] a,float[] b) { var m=new float[16]; for(int r=0;r<4;r++)for(int c=0;c<4;c++){ double v=0;for(int k=0;k<4;k++)v+=(double)a[r*4+k]*b[k*4+c]; SkinJson.Check(Math.Abs(v)<=AvatarMath.MaxPosition,"Matrix overflow");m[r*4+c]=(float)v;}return m; }
        public static float[] Inverse(float[] m) {
            var a=new double[4,8];for(int r=0;r<4;r++)for(int c=0;c<4;c++){a[r,c]=m[r*4+c];a[r,c+4]=r==c?1:0;}
            for(int c=0;c<4;c++){int best=c;for(int r=c+1;r<4;r++)if(Math.Abs(a[r,c])>Math.Abs(a[best,c]))best=r;SkinJson.Check(Math.Abs(a[best,c])>1e-12,"Singular matrix");for(int k=0;k<8;k++){double t=a[c,k];a[c,k]=a[best,k];a[best,k]=t;}double v=a[c,c];for(int k=0;k<8;k++)a[c,k]/=v;for(int r=0;r<4;r++)if(r!=c){v=a[r,c];for(int k=0;k<8;k++)a[r,k]-=v*a[c,k];}}
            var b=new float[16];for(int r=0;r<4;r++)for(int c=0;c<4;c++){SkinJson.Check(Math.Abs(a[r,c+4])<=AvatarMath.MaxPosition,"Inverse bounds"); b[r*4+c]=(float)a[r,c+4];}return b;
        }
        internal static float[] Reflect(float[] m) { var b=(float[])m.Clone();for(int r=0;r<4;r++)for(int c=0;c<4;c++)if((r==2)!=(c==2))b[r*4+c]=-b[r*4+c];return b; }
    }
}


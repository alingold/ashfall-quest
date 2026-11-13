using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.Collections.Generic;

namespace Ashfall.Modules.MapStreaming
{
    // Deliberately owned: no dependency on a sibling parser or Framework web assembly.
    public static class MapJson
    {
        public sealed class Number
        {
            public readonly string Token;
            internal Number(string token) { Token=token; AsDouble(); }
            public long AsInteger()
            {
                long n;
                if(Token.IndexOfAny(new[]{'.','e','E'})>=0 || !Int64.TryParse(Token,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out n))
                    throw new InvalidDataException("Exact Int64 token required: "+Token);
                return n;
            }
            public double AsDouble()
            {
                double n;
                if(!Double.TryParse(Token,NumberStyles.Float,CultureInfo.InvariantCulture,out n)||Double.IsInfinity(n)||Double.IsNaN(n))throw new InvalidDataException("Nonfinite number");
                // Nonzero literals that underflow to zero are not silently rewritten.
                if(n==0) { string mantissa=Token.Split('e','E')[0]; foreach(char c in mantissa)if(c>='1'&&c<='9')throw new InvalidDataException("Number underflow"); }
                return n;
            }
        }
        public static object Parse(byte[] utf8,int byteBudget,int maxDepth,int nodeBudget)
        {
            if(utf8==null||byteBudget<0||utf8.Length>byteBudget||maxDepth<1||maxDepth>128||nodeBudget<1)throw new InvalidDataException("JSON budget");
            try { var r=new Reader(new UTF8Encoding(false,true).GetString(utf8),maxDepth,nodeBudget);object v=r.Value(0);r.White();if(r.P!=r.S.Length)throw r.Error("Trailing input");return v; }
            catch(DecoderFallbackException e) { throw new InvalidDataException("Invalid UTF8",e); }
        }
        sealed class Reader
        {
            internal readonly string S; internal int P; readonly int depth; int remaining;
            internal Reader(string s,int d,int n) {S=s;depth=d;remaining=n;}
            internal InvalidDataException Error(string why) {return new InvalidDataException(why+" at character "+P);}
            internal void White() {while(P<S.Length&&(S[P]==' '||S[P]=='\t'||S[P]=='\r'||S[P]=='\n'))P++;}
            bool Take(char c) {if(P<S.Length&&S[P]==c){P++;return true;}return false;}
            void Want(char c) {if(!Take(c))throw Error("Expected "+c);}
            void Count() {if(--remaining<0)throw Error("JSON node budget");}
            internal object Value(int level)
            {
                Count();if(level>=depth)throw Error("JSON depth");White();if(P==S.Length)throw Error("Missing value");char c=S[P];
                if(c=='{') {P++;var d=new Dictionary<string,object>(StringComparer.Ordinal);White();if(Take('}'))return d;do {White();Count();string k=Text();if(d.ContainsKey(k))throw Error("Duplicate member "+k);White();Want(':');d.Add(k,Value(level+1));White();if(Take('}'))return d;Want(',');}while(true);}
                if(c=='[') {P++;var a=new List<object>();White();if(Take(']'))return a.ToArray();do {a.Add(Value(level+1));White();if(Take(']'))return a.ToArray();Want(',');}while(true);}
                if(c=='"')return Text();
                foreach(string literal in new[]{"true","false","null"})if(c==literal[0]) {if(P+literal.Length>S.Length||S.Substring(P,literal.Length)!=literal)throw Error("Invalid literal");P+=literal.Length;return literal=="null"?null:(object)(literal=="true");}
                int start=P;Take('-');if(!Take('0')) {if(P==S.Length||S[P]<'1'||S[P]>'9')throw Error("Number");Digits();}
                if(Take('.')) {int p=P;Digits();if(P==p)throw Error("Fraction");}
                if(Take('e')||Take('E')) {if(!Take('+'))Take('-');int p=P;Digits();if(P==p)throw Error("Exponent");}
                return new Number(S.Substring(start,P-start));
            }
            void Digits() {while(P<S.Length&&S[P]>='0'&&S[P]<='9')P++;}
            int Hex() {if(P+4>S.Length)throw Error("Unicode escape");int n=0;for(int i=0;i<4;i++){char c=S[P++];int v=c>='0'&&c<='9'?c-'0':c>='a'&&c<='f'?c-'a'+10:c>='A'&&c<='F'?c-'A'+10:-1;if(v<0)throw Error("Unicode hex");n=n*16+v;}return n;}
            string Text()
            {
                Want('"');var b=new StringBuilder();while(P<S.Length) {char c=S[P++];if(c=='"')return b.ToString();if(c<' ')throw Error("String control");
                    if(c=='\\') {if(P==S.Length)throw Error("Escape");c=S[P++];switch(c){case '"':case '\\':case '/':break;case 'b':c='\b';break;case 'f':c='\f';break;case 'n':c='\n';break;case 'r':c='\r';break;case 't':c='\t';break;case 'u':c=(char)Hex();break;default:throw Error("Escape");}}
                    if(Char.IsHighSurrogate(c)) {b.Append(c);char low;if(P<S.Length&&Char.IsLowSurrogate(S[P]))low=S[P++];else {if(P+2>S.Length||S[P++]!='\\'||S[P++]!='u')throw Error("Surrogate pair");low=(char)Hex();}if(!Char.IsLowSurrogate(low))throw Error("Surrogate pair");b.Append(low);}
                    else {if(Char.IsLowSurrogate(c))throw Error("Lone surrogate");b.Append(c);}
                }throw Error("Unterminated string");
            }
        }
    }
}

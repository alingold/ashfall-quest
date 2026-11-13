using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.Serialization;
namespace Ashfall.Modules.World
{
    // Validate the wire grammar BEFORE DataContract deserialization can discard a member.
    internal sealed class StrictWorldJson
    {
        readonly string text; int at;
        StrictWorldJson(string value) { text=value; }
        internal static void Validate(byte[] bytes)
        {
            if(bytes==null||bytes.Length==0||bytes.Length>16*1024*1024)throw new InvalidDataException("Map payload limit");
            try { var p=new StrictWorldJson(new UTF8Encoding(false,true).GetString(bytes));p.Value(typeof(MapDocument),0);p.Space();if(p.at!=p.text.Length)p.Bad(); }
            catch(DecoderFallbackException e){throw new InvalidDataException("Malformed UTF8",e);}
        }
        void Bad(){throw new InvalidDataException("Unsupported/malformed map JSON at "+at);}
        void Space(){while(at<text.Length&&(text[at]==' '||text[at]=='\r'||text[at]=='\n'||text[at]=='\t'))at++;}
        bool Take(char c){Space();if(at<text.Length&&text[at]==c){at++;return true;}return false;}
        void Need(char c){if(!Take(c))Bad();}
        bool Literal(string s){Space();if(at+s.Length<=text.Length&&String.CompareOrdinal(text,at,s,0,s.Length)==0){at+=s.Length;return true;}return false;}
        string StringValue()
        {
            Need('"');var b=new StringBuilder();bool ended=false;
            while(at<text.Length){char c=text[at++];if(c=='"'){ended=true;break;}if(c<32)Bad();
                if(c=='\\'){if(at==text.Length)Bad();c=text[at++];switch(c){case '"':case '\\':case '/':break;case 'b':c='\b';break;case 'f':c='\f';break;case 'n':c='\n';break;case 'r':c='\r';break;case 't':c='\t';break;case 'u':int n=0;for(int i=0;i<4;i++){if(at==text.Length)Bad();char h=text[at++];int v=h>='0'&&h<='9'?h-'0':h>='a'&&h<='f'?h-'a'+10:h>='A'&&h<='F'?h-'A'+10:-1;if(v<0)Bad();n=n*16+v;}c=(char)n;break;default:Bad();break;}}
                b.Append(c);
            }
            if(!ended)Bad();string result=b.ToString();for(int i=0;i<result.Length;i++){if(Char.IsHighSurrogate(result[i])){if(++i==result.Length||!Char.IsLowSurrogate(result[i]))Bad();}else if(Char.IsLowSurrogate(result[i]))Bad();}return result;
        }
        bool Digit(){return at<text.Length&&text[at]>='0'&&text[at]<='9';}
        void Number(bool integer)
        {
            Space();if(at<text.Length&&text[at]=='-')at++;if(!Digit())Bad();if(text[at]=='0')at++;else while(Digit())at++;
            if(at<text.Length&&text[at]=='.'){if(integer)Bad();at++;if(!Digit())Bad();while(Digit())at++;}
            if(at<text.Length&&(text[at]=='e'||text[at]=='E')){if(integer)Bad();at++;if(at<text.Length&&(text[at]=='+'||text[at]=='-'))at++;if(!Digit())Bad();while(Digit())at++;}
        }
        void Value(Type type,int depth)
        {
            if(depth>32)Bad();Space();if(!type.IsValueType&&Literal("null"))return;
            if(type==typeof(string)){StringValue();return;}if(type==typeof(bool)){if(!Literal("true")&&!Literal("false"))Bad();return;}
            if(type==typeof(float)||type==typeof(int)){Number(type==typeof(int));return;}
            if(type.IsArray){Need('[');if(Take(']'))return;do{Value(type.GetElementType(),depth+1);}while(Take(','));Need(']');return;}
            var fields=new Dictionary<string,Type>(StringComparer.Ordinal);
            foreach(var f in type.GetFields(BindingFlags.Public|BindingFlags.Instance)){var a=(DataMemberAttribute)Attribute.GetCustomAttribute(f,typeof(DataMemberAttribute));if(a!=null)fields.Add(a.Name??f.Name,f.FieldType);}
            if(fields.Count==0)Bad();Need('{');var seen=new HashSet<string>(StringComparer.Ordinal);if(Take('}'))return;
            do{string key=StringValue();Type field;if(!seen.Add(key)||!fields.TryGetValue(key,out field))Bad();Need(':');Value(fields[key],depth+1);}while(Take(','));Need('}');
        }
    }
}

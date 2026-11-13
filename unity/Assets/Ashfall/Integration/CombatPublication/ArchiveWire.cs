using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
namespace Ashfall.Integration {
    internal static class CheckpointWire
    {
        internal const int MaximumBytes=2097152;
        internal static bool Binding(string s){if(s==null||s.Length!=64)return false;foreach(char c in s)if(!Uri.IsHexDigit(c))return false;return true;}
        internal static string Hash(byte[] bytes){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        internal static bool Matches(byte[] bytes,string hash){return bytes!=null&&bytes.Length>0&&bytes.Length<=MaximumBytes&&hash!=null&&hash.Length==64&&String.Equals(Hash(bytes),hash,StringComparison.OrdinalIgnoreCase);}
        internal static int Count(BinaryReader r,int maximum){int n=r.ReadInt32();if(n<0||n>maximum)throw new InvalidDataException("checkpoint-count");return n;}
        internal static long Number(BinaryReader r,long minimum,long maximum){long n=r.ReadInt64();if(n<minimum||n>maximum)throw new InvalidDataException("checkpoint-number");return n;}
        internal static string Text(BinaryReader r){return Text(r,512,false);}
        internal static string Text(BinaryReader r,int maximum,bool empty)
        {
            uint length=0;int shift=0;
            while(true){byte b=r.ReadByte();if(shift==28&&(b&240)!=0)throw new InvalidDataException("checkpoint-string-length");length|=(uint)(b&127)<<shift;if((b&128)==0)break;shift+=7;if(shift>28)throw new InvalidDataException("checkpoint-string-length");}
            if(length>(uint)maximum*4||length>r.BaseStream.Length-r.BaseStream.Position)throw new InvalidDataException("checkpoint-string-length");
            byte[] bytes=r.ReadBytes((int)length);string s=new UTF8Encoding(false,true).GetString(bytes);
            if((!empty&&s.Length==0)||s.Length>maximum)throw new InvalidDataException("checkpoint-string");return s;
        }
        internal static float Amount(BinaryReader r,float max){float n=r.ReadSingle();if(Single.IsNaN(n)||Single.IsInfinity(n)||n<0||n>max)throw new InvalidDataException("checkpoint-amount");return n;}
        internal static double Time(BinaryReader r){double n=r.ReadDouble();if(Double.IsNaN(n)||Double.IsInfinity(n)||n<0)throw new InvalidDataException("checkpoint-time");return n;}
        internal static void Vector(BinaryWriter w,Float3 p){w.Write(p.X);w.Write(p.Y);w.Write(p.Z);}
        internal static Float3 Vector(BinaryReader r){var v=new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());if(!v.IsFinite)throw new InvalidDataException("checkpoint-vector");return v;}
        internal static void Result(BinaryWriter w,RuleResult r){w.Write(r.Accepted);w.Write(r.Reason??"");}
        internal static RuleResult Result(BinaryReader r){bool ok=r.ReadBoolean();string reason=Text(r,512,true);if(ok&&reason!="")throw new InvalidDataException("checkpoint-result");return new RuleResult(ok,reason);}
        internal static void Damage(BinaryWriter w,DamageReceipt d){MatchCheckpointRecords.WriteDamageRecord(w,d);}
        internal static DamageReceipt Damage(BinaryReader r){return MatchCheckpointRecords.ReadDamageRecord(r);}
        internal static void Transition(BinaryWriter w,HealthTransition t){MatchCheckpointRecords.WriteTransitionRecord(w,t);}
        internal static HealthTransition Transition(BinaryReader r){return MatchCheckpointRecords.ReadTransitionRecord(r);}
    }
}


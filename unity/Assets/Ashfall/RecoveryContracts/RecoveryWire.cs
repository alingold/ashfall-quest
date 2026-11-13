using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Security.Cryptography;
namespace Ashfall.RecoveryContracts
{
    // Codec utilities are not authority. Only ColdInventoryCheckpoint checks a root receipt.
    public static class RecoveryWire
    {
        public const int Limit=4*1024*1024;
        public static void Require(bool ok){if(!ok)throw new InvalidDataException("Invalid or unsupported checkpoint");}
        public static void Text(BinaryWriter w,string s){Require(s!=null);byte[] b=new UTF8Encoding(false,true).GetBytes(s);Require(b.Length<=1024);w.Write(b.Length);w.Write(b);}
        public static string Text(BinaryReader r){int n=Count(r,1024);byte[] b=r.ReadBytes(n);Require(b.Length==n);return new UTF8Encoding(false,true).GetString(b);}
        public static int Count(BinaryReader r,int max){int n=r.ReadInt32();Require(n>=0&&n<=max);return n;}
        public static void Blob(BinaryWriter w,byte[] b){Require(b.Length<=Limit);w.Write(b.Length);w.Write(b);}
        public static byte[] Blob(BinaryReader r){int n=Count(r,Limit);byte[] b=r.ReadBytes(n);Require(b.Length==n);return b;}
        public static BinaryReader Reader(byte[] b){Require(b!=null&&b.Length<=Limit);return new BinaryReader(new MemoryStream(b,false),new UTF8Encoding(false,true));}
        public static void End(BinaryReader r){Require(r.BaseStream.Position==r.BaseStream.Length);}
        public static bool Bool(BinaryReader r){byte b=r.ReadByte();Require(b<2);return b==1;}
        public static float Float(BinaryReader r){float f=r.ReadSingle();Require(!Single.IsNaN(f)&&!Single.IsInfinity(f));return f;}
        public static string Hash(byte[] b){using(var s=SHA256.Create())return BitConverter.ToString(s.ComputeHash(b)).Replace("-","").ToLowerInvariant();}
        // Names AND runtime types form a frozen explicit field inventory; no automatic serialization.
        public static void Fields(Type type,string expected){var names=new System.Collections.Generic.List<string>();foreach(var f in type.GetFields(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.DeclaredOnly))names.Add(f.Name+":"+f.FieldType.ToString());names.Sort(StringComparer.Ordinal);Require(String.Join("\n",names.ToArray())==expected);}
    }
    public sealed class CheckpointBinding
    {
        public readonly string Build,Catalog,Session,Owner;
        public readonly long Epoch,Life,Connection,Sequence;
        public CheckpointBinding(string build,string catalog,string session,string owner,long epoch,long life,long connection,long sequence){RecoveryWire.Require(!String.IsNullOrEmpty(build)&&!String.IsNullOrEmpty(catalog)&&!String.IsNullOrEmpty(session)&&!String.IsNullOrEmpty(owner)&&epoch>=0&&life>=0&&connection>=0&&sequence>0);Build=build;Catalog=catalog;Session=session;Owner=owner;Epoch=epoch;Life=life;Connection=connection;Sequence=sequence;}
        public void Write(BinaryWriter w){RecoveryWire.Text(w,Build);RecoveryWire.Text(w,Catalog);RecoveryWire.Text(w,Session);RecoveryWire.Text(w,Owner);w.Write(Epoch);w.Write(Life);w.Write(Connection);w.Write(Sequence);}
        public void Check(BinaryReader r){RecoveryWire.Require(RecoveryWire.Text(r)==Build&&RecoveryWire.Text(r)==Catalog&&RecoveryWire.Text(r)==Session&&RecoveryWire.Text(r)==Owner&&r.ReadInt64()==Epoch&&r.ReadInt64()==Life&&r.ReadInt64()==Connection&&r.ReadInt64()==Sequence);}
    }
}

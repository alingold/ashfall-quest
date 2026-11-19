using System;using System.IO;using Ashfall.Core;
namespace Ashfall.Integration.Playable.Networking
{
    // Host-only victim cue on the existing score snapshot feature. Layout 5,
    // spawn int32 + life int64 + world source 3xf32 = 33 payload bytes including host time.
    public sealed class R26DamageDirectionReceipt
    {
        public const byte Layout=5;public const int Bytes=33;
        public readonly double Time;public readonly int Spawn;public readonly long Life;public readonly Float3 Source;
        public R26DamageDirectionReceipt(int spawn,long life,Float3 source,double time){if(spawn<1||life<1||!source.IsFinite||Double.IsNaN(time)||Double.IsInfinity(time)||time<0)throw new ArgumentException("Damage direction receipt");Spawn=spawn;Life=life;Source=source;Time=time;}
        public byte[] Encode(){using(var s=new MemoryStream(Bytes))using(var w=new BinaryWriter(s)){w.Write(Layout);w.Write(Spawn);w.Write(Life);w.Write(Source.X);w.Write(Source.Y);w.Write(Source.Z);w.Write(Time);return s.ToArray();}}
        public static bool TryDecode(byte[] payload,out R26DamageDirectionReceipt receipt)
        {
            receipt=null;if(payload==null||payload.Length!=Bytes||payload[0]!=Layout)return false;
            using(var s=new MemoryStream(payload,false))using(var r=new BinaryReader(s)){r.ReadByte();int spawn=r.ReadInt32();long life=r.ReadInt64();var source=new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());double time=r.ReadDouble();if(spawn<1||life<1||!source.IsFinite||Double.IsNaN(time)||Double.IsInfinity(time)||time<0)return false;receipt=new R26DamageDirectionReceipt(spawn,life,source,time);return true;}
        }
    }
}

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
namespace Ashfall.Modules.Networking
{
    public enum PacketKind : byte { Hello=1, Command=2, Snapshot=3, Delta=4 }
    public sealed class Packet
    {
        public PacketKind Kind;
        public string Protocol=Ashfall.Core.Protocol.Name, ContentHash="", SessionId="", ActorId="", CommandId="", FeatureId="";
        public int Schema=Ashfall.Core.Protocol.Schema;
        public long Epoch, Tick, Sequence, BaseSequence;
        public byte[] Payload=new byte[0];
    }
    public static class Wire
    {
        public const int MaxPacketBytes=1000, MaxPayloadBytes=512;
        private static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
        // Shared configuration/wire rule: strict UTF8, byte bound, explicit empty policy.
        public static bool IsText(string text,int maxBytes,bool allowEmpty)
        {
            if(text==null || maxBytes<0 || (!allowEmpty && text.Length==0)) return false;
            try { return Utf8.GetByteCount(text)<=maxBytes; }
            catch(EncoderFallbackException) { return false; }
        }
        private static void WriteText(BinaryWriter w,string s,int max)
        {
            if(!IsText(s,max,true)) throw new ArgumentException("text-size-or-encoding");
            byte[] bytes=Utf8.GetBytes(s);
            w.Write((ushort)bytes.Length); w.Write(bytes);
        }
        private static string ReadText(BinaryReader r,int max)
        {
            int n=r.ReadUInt16(); if(n>max || n>r.BaseStream.Length-r.BaseStream.Position) throw new InvalidDataException();
            return Utf8.GetString(r.ReadBytes(n));
        }
        public static byte[] Encode(Packet p)
        {
            if(p==null || (byte)p.Kind<1 || (byte)p.Kind>4 || p.Payload==null || p.Payload.Length>MaxPayloadBytes) throw new ArgumentException("packet");
            using(var m=new MemoryStream()) using(var w=new BinaryWriter(m,Utf8))
            {
                w.Write(0x314E4641); w.Write((byte)p.Kind); w.Write(p.Schema);
                WriteText(w,p.Protocol,32); WriteText(w,p.ContentHash,64); WriteText(w,p.SessionId,64);
                WriteText(w,p.ActorId,64); WriteText(w,p.CommandId,64); WriteText(w,p.FeatureId,64);
                w.Write(p.Epoch); w.Write(p.Tick); w.Write(p.Sequence); w.Write(p.BaseSequence);
                w.Write((ushort)p.Payload.Length); w.Write(p.Payload); w.Flush();
                if(m.Length>MaxPacketBytes) throw new ArgumentException("packet-size"); return m.ToArray();
            }
        }
        public static bool TryDecode(byte[] bytes,out Packet p)
        {
            p=null; if(bytes==null || bytes.Length>MaxPacketBytes) return false;
            try
            {
                using(var m=new MemoryStream(bytes,false)) using(var r=new BinaryReader(m,Utf8))
                {
                    if(r.ReadInt32()!=0x314E4641) return false;
                    var n=new Packet(); n.Kind=(PacketKind)r.ReadByte(); if((byte)n.Kind<1 || (byte)n.Kind>4) return false;
                    n.Schema=r.ReadInt32(); n.Protocol=ReadText(r,32); n.ContentHash=ReadText(r,64); n.SessionId=ReadText(r,64);
                    n.ActorId=ReadText(r,64); n.CommandId=ReadText(r,64); n.FeatureId=ReadText(r,64);
                    n.Epoch=r.ReadInt64(); n.Tick=r.ReadInt64(); n.Sequence=r.ReadInt64(); n.BaseSequence=r.ReadInt64();
                    int size=r.ReadUInt16(); if(size>MaxPayloadBytes || size!=m.Length-m.Position) return false;
                    n.Payload=r.ReadBytes(size); p=n; return true;
                }
            }
            catch(InvalidDataException) { return false; } catch(IOException) { return false; } catch(ArgumentException) { return false; }
        }
        public static bool IsHash(string hash)
        {
            if(hash==null || hash.Length!=64) return false;
            foreach(char c in hash) if(!((c>='0' && c<='9') || (c>='a' && c<='f'))) return false;
            return true;
        }
    }
    // Single simulation thread. Explicit rejection; never evicts reliable work silently.
    public sealed class BoundedQueue<T>
    {
        private readonly Queue<T> queue=new Queue<T>(); private readonly int capacity;
        public BoundedQueue(int capacity) { if(capacity<1) throw new ArgumentOutOfRangeException("capacity"); this.capacity=capacity; }
        public int Count { get { return queue.Count; } }
        public bool TryAdd(T item) { if(queue.Count==capacity) return false; queue.Enqueue(item); return true; }
        public bool TryTake(out T item) { if(queue.Count==0) { item=default(T); return false; } item=queue.Dequeue(); return true; }
        public void Clear() { queue.Clear(); }
    }
}


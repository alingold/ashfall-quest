using System;
using System.IO;
namespace Ashfall.Modules.Networking
{
    // AFR1 envelope is explicitly opt-in, not Godot or legacy AFN1 compatibility.
    // Generation is a Sessions-bound lifetime ID, never a credential. Transport must
    // authenticate peer identity. The total datagram retains the frozen 1000 byte cap.
    public static class ResyncWire
    {
        private const int Magic=0x31524641;
        public static byte[] EncodeRequest(ResyncRequest request)
        {
            if(request==null || request.Generation<=0 || request.RequestId<=0 || request.MinimumSequence< -1) throw new ArgumentException("request");
            using(var m=new MemoryStream()) using(var w=new BinaryWriter(m))
            { w.Write(Magic); w.Write((byte)1); w.Write(request.Generation); w.Write(request.RequestId); w.Write(request.MinimumSequence); return m.ToArray(); }
        }
        public static bool TryDecodeRequest(byte[] bytes,out ResyncRequest request)
        {
            request=null; if(bytes==null || bytes.Length!=29) return false;
            using(var m=new MemoryStream(bytes,false)) using(var r=new BinaryReader(m))
            {
                if(r.ReadInt32()!=Magic || r.ReadByte()!=1) return false;
                long g=r.ReadInt64(), id=r.ReadInt64(), min=r.ReadInt64();
                if(g<=0 || id<=0 || min< -1) return false;
                request=new ResyncRequest(g,id,min); return true;
            }
        }
        public static byte[] EncodeState(long generation,long requestId,Packet state)
        {
            if(generation<=0 || requestId<0 || state==null || (state.Kind!=PacketKind.Snapshot && state.Kind!=PacketKind.Delta)) throw new ArgumentException("state");
            byte[] body=Wire.Encode(state);
            if(body.Length+21>Wire.MaxPacketBytes) throw new ArgumentException("frame-size");
            using(var m=new MemoryStream()) using(var w=new BinaryWriter(m))
            { w.Write(Magic); w.Write((byte)2); w.Write(generation); w.Write(requestId); w.Write(body); return m.ToArray(); }
        }
        public static bool TryDecodeState(byte[] bytes,out long generation,out long requestId,out byte[] state)
        {
            generation=0; requestId=0; state=null;
            if(bytes==null || bytes.Length<=21 || bytes.Length>Wire.MaxPacketBytes) return false;
            using(var m=new MemoryStream(bytes,false)) using(var r=new BinaryReader(m))
            {
                if(r.ReadInt32()!=Magic || r.ReadByte()!=2) return false;
                long g=r.ReadInt64(),id=r.ReadInt64(); if(g<=0 || id<0) return false;
                byte[] body=r.ReadBytes(bytes.Length-21); Packet p;
                if(!Wire.TryDecode(body,out p) || (p.Kind!=PacketKind.Snapshot && p.Kind!=PacketKind.Delta)) return false;
                generation=g; requestId=id; state=body; return true;
            }
        }
    }
}

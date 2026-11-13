using System;
using System.IO;
using System.Text;
using Ashfall.Core;
using Ashfall.Modules.Sessions;
namespace Ashfall.Integration.SessionReplication
{
    // Bounded member view only: not the complete game/roster/health snapshot.
    public sealed class SessionReplica
    {
        public readonly string Actor;
        public readonly int Team, Revision, Deaths;
        public readonly bool Loaded, Waiting, HasPosition;
        public readonly SessionLife Life;
        public readonly Float3 Position;
        internal SessionReplica(string actor,int team,int revision,int deaths,bool loaded,bool waiting,bool has,SessionLife life,Float3 pos)
        { Actor=actor; Team=team; Revision=revision; Deaths=deaths; Loaded=loaded; Waiting=waiting; HasPosition=has; Life=life; Position=pos; }
    }
    internal static class MemberCodec
    {
        private static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
        internal static byte[] Encode(MemberSnapshot s)
        {
            using(var m=new MemoryStream()) using(var w=new BinaryWriter(m,Utf8))
            { w.Write((byte)1); w.Write(s.ActorId); w.Write(s.Team); w.Write(s.SpawnRevision); w.Write(s.Deaths);
              w.Write((byte)((s.Loaded?1:0)|(s.WaitingRound?2:0)|(s.HasPosition?4:0))); w.Write((byte)s.Life);
              w.Write(s.Position.X); w.Write(s.Position.Y); w.Write(s.Position.Z); return m.ToArray(); }
        }
        internal static bool Decode(byte[] data,string actor,out SessionReplica view)
        {
            view=null; if(data==null || data.Length>128)return false;
            try
            {
                using(var m=new MemoryStream(data,false)) using(var r=new BinaryReader(m,Utf8))
                {
                    if(r.ReadByte()!=1)return false;
                    // Actor is at most64 UTF8 bytes under Wire's identity contract.
                    // Bound before allocation and reject noncanonical/continued
                    // length prefixes; BinaryReader.ReadString allocates by length.
                    int length=r.ReadByte();
                    if(length<1 || length>64 || m.Length-m.Position<length)return false;
                    string id=Utf8.GetString(r.ReadBytes(length));
                    int team=r.ReadInt32(),rev=r.ReadInt32(),deaths=r.ReadInt32();
                    byte flags=r.ReadByte(),life=r.ReadByte(); var p=new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
                    if(id!=actor || team< -1 || team>7 || rev<0 || deaths<0 || flags>7 || life>3 || !p.IsFinite || m.Position!=m.Length)return false;
                    view=new SessionReplica(id,team,rev,deaths,(flags&1)!=0,(flags&2)!=0,(flags&4)!=0,(SessionLife)life,p); return true;
                }
            }
            catch(IOException) { return false; } catch(ArgumentException) { return false; } catch(FormatException) { return false; }
        }
    }
}

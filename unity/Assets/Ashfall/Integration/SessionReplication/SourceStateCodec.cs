using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Networking;
using Ashfall.Modules.Sessions;
using Ashfall.Modules.HealthStates;
namespace Ashfall.Integration.SessionReplication
{
    internal static class SourceStateCodec
    {
        internal const int Maximum=32768,Chunk=400,MaxChunks=82;
        private static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
        private static bool Position(Float3 p){return p.IsFinite&&Math.Abs(p.X)<=100000&&Math.Abs(p.Y)<=100000&&Math.Abs(p.Z)<=100000;}
        private static bool Valid(SourceMemberState m)
        {return m!=null&&Wire.IsText(m.Actor,64,false)&&Wire.IsText(m.Canonical,64,false)&&m.Team>=-1&&m.Team<8&&m.SpawnRevision>=0&&m.Deaths>=0&&
            (int)m.SessionLife>=0&&(int)m.SessionLife<=3&&Position(m.Feet)&&Position(m.Head)&&
            (!m.HasHealth||(m.HealthRevision>0&&m.LifeGeneration>0&&m.ConnectionGeneration>0&&(int)m.HealthLife>=0&&(int)m.HealthLife<=3&&
            !Single.IsNaN(m.Health)&&m.Health>=0&&m.Health<=100&&!Single.IsNaN(m.Shield)&&m.Shield>=0&&m.Shield<=100))&&
            (!m.HasHead||(m.HasHealth&&m.PoseGeneration>0&&m.PoseTick>=0));}
        internal static bool Validate(SourceStateView view)
        {
            if(view==null||(int)view.Stage<0||(int)view.Stage>3||view.LifecycleSequence< -1)return false;
            var actors=new HashSet<string>(StringComparer.Ordinal);var canon=new HashSet<string>(StringComparer.Ordinal);var ms=view.Members;
            if(ms.Length>64)return false;foreach(var m in ms)if(!Valid(m)||!actors.Add(m.Actor)||!canon.Add(m.Canonical))return false;return true;
        }
        private static void Text(BinaryWriter w,string s){byte[] b=Utf8.GetBytes(s);w.Write((byte)b.Length);w.Write(b);}
        private static string Text(BinaryReader r){int n=r.ReadByte();if(n<1||n>64)throw new InvalidDataException();byte[] b=r.ReadBytes(n);if(b.Length!=n)throw new EndOfStreamException();return Utf8.GetString(b);}
        private static void Vec(BinaryWriter w,Float3 p){w.Write(p.X);w.Write(p.Y);w.Write(p.Z);}
        private static Float3 Vec(BinaryReader r){return new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());}
        internal static byte[] Member(SourceMemberState m)
        {
            if(!Valid(m))throw new ArgumentException("member");
            using(var b=new MemoryStream())using(var w=new BinaryWriter(b,Utf8))
            {Text(w,m.Actor);Text(w,m.Canonical);w.Write(m.Team);w.Write(m.SpawnRevision);w.Write(m.Deaths);
             w.Write((byte)((m.Loaded?1:0)|(m.Waiting?2:0)|(m.HasFeet?4:0)|(m.HasHealth?8:0)|(m.HasHead?16:0)));
             w.Write((byte)m.SessionLife);if(m.HasFeet)Vec(w,m.Feet);
             if(m.HasHealth){w.Write((byte)m.HealthLife);w.Write(m.HealthRevision);w.Write(m.LifeGeneration);w.Write(m.ConnectionGeneration);w.Write(m.Health);w.Write(m.Shield);}
             if(m.HasHead){w.Write(m.PoseGeneration);w.Write(m.PoseTick);Vec(w,m.Head);}return b.ToArray();}
        }
        private static SourceMemberState Member(BinaryReader r)
        {
            string a=Text(r),c=Text(r);int t=r.ReadInt32(),s=r.ReadInt32(),d=r.ReadInt32();byte f=r.ReadByte();if(f>31)throw new InvalidDataException();
            var life=(SessionLife)r.ReadByte();bool feet=(f&4)!=0,h=(f&8)!=0,p=(f&16)!=0;Float3 pos=feet?Vec(r):new Float3();
            LifeState hl=LifeState.Spectator;long rev=0,lg=0,cg=0,pg=0,tick=0;float hp=0,shield=0;Float3 head=new Float3();
            if(h){hl=(LifeState)r.ReadByte();rev=r.ReadInt64();lg=r.ReadInt64();cg=r.ReadInt64();hp=r.ReadSingle();shield=r.ReadSingle();}
            if(p){pg=r.ReadInt64();tick=r.ReadInt64();head=Vec(r);}
            var m=new SourceMemberState(a,c,t,s,d,(f&1)!=0,(f&2)!=0,feet,life,pos,h,hl,rev,lg,cg,hp,shield,p,pg,tick,head);
            if(!Valid(m))throw new InvalidDataException();return m;
        }
        internal static bool Equal(SourceMemberState a,SourceMemberState b){return Convert.ToBase64String(Member(a))==Convert.ToBase64String(Member(b));}
        // Full and delta both carry exact lifecycle metadata; delta replaces changed
        // members and removes departed actor IDs. Never mutate the prior view.
        internal static byte[] Encode(SourceStateView next,SourceStateView previous)
        {
            if(!Validate(next))throw new ArgumentException("view");var old=new Dictionary<string,SourceMemberState>(StringComparer.Ordinal);
            if(previous!=null)foreach(var m in previous.Members)old.Add(m.Actor,m);
            var changed=new List<SourceMemberState>();foreach(var m in next.Members){SourceMemberState prior;if(!old.TryGetValue(m.Actor,out prior)||!Equal(m,prior))changed.Add(m);old.Remove(m.Actor);}
            using(var b=new MemoryStream())using(var w=new BinaryWriter(b,Utf8))
            {w.Write((byte)1);w.Write((byte)next.Stage);w.Write(next.LifecycleSequence);w.Write((byte)changed.Count);foreach(var m in changed)w.Write(Member(m));
             w.Write((byte)old.Count);var keys=new List<string>(old.Keys);keys.Sort(StringComparer.Ordinal);foreach(string k in keys)Text(w,k);
             if(b.Length>Maximum)throw new ArgumentException("image-capacity");return b.ToArray();}
        }
        internal static bool Decode(byte[] bytes,SourceStateView previous,out SourceStateView view)
        {
            view=null;if(bytes==null||bytes.Length>Maximum)return false;
            try {using(var b=new MemoryStream(bytes,false))using(var r=new BinaryReader(b,Utf8))
            {if(r.ReadByte()!=1)return false;var stage=(SessionStage)r.ReadByte();long lifecycle=r.ReadInt64();var map=new Dictionary<string,SourceMemberState>(StringComparer.Ordinal);
             if(previous!=null)foreach(var m in previous.Members)map.Add(m.Actor,m);var changed=new HashSet<string>(StringComparer.Ordinal);
             int n=r.ReadByte();if(n>64)return false;for(int i=0;i<n;i++){var m=Member(r);if(!changed.Add(m.Actor))return false;map[m.Actor]=m;}
             n=r.ReadByte();if(n>64||previous==null&&n!=0)return false;for(int i=0;i<n;i++){string a=Text(r);if(!changed.Add(a)||!map.Remove(a))return false;}
             if(b.Position!=b.Length||map.Count>64)return false;var ms=new List<SourceMemberState>(map.Values);ms.Sort(delegate(SourceMemberState a,SourceMemberState c){return StringComparer.Ordinal.Compare(a.Actor,c.Actor);});
             var next=new SourceStateView(stage,lifecycle,ms.ToArray());if(!Validate(next))return false;view=next;return true;}}
            catch(IOException){return false;}catch(DecoderFallbackException){return false;}catch(ArgumentException){return false;}
        }
    }
}

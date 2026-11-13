using System;
using System.IO;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Modes;
using Ashfall.RecoveryContracts;

namespace Ashfall.Modules.ModesRecovery
{
    public sealed partial class RecoverablePingRecord
    {
        internal RecoverablePingRecord(string owner,string map,long epoch,int team,int color,int catalog,Float3 point,Float3 normal,double created,double until)
        { OwnerId=owner; MapId=map; Epoch=epoch; Team=team; Color=color; Catalog=catalog; Point=point; Normal=normal; Created=created; Until=until; Action=RecoverablePingService.Action(color); }
    }
    // Owner-private codec. Call only on the single trusted host mutation lane.
    // Returned instances are quarantined: this API does not publish or restore authority.
    public sealed partial class RecoverablePingService
    {
        public const string Codec = "ashfall-recoverable-ping/1";
        public const int CheckpointLimit=4*1024*1024, HistoryLimit=8192;
        private static void Require(bool ok) { RecoveryWire.Require(ok); }
        private static string F<T>(string name) { return name+":"+typeof(T).ToString(); }
        private static void Fields(Type type,params string[] fields)
        { Array.Sort(fields,StringComparer.Ordinal); RecoveryWire.Fields(type,String.Join("\n",fields)); }
        private static void Guard()
        {
            Fields(typeof(RecoverablePingService),F<IModeRoster>("roster"),F<RecoveryPingLedger>("ledger"),F<Dictionary<string,RecoverablePingRecord>>("marks"),F<Dictionary<string,double>>("last"),F<int>("teams"),F<string>("map"),F<long>("epoch"),F<double>("cooldown"),F<double>("lastTime"));
            Fields(typeof(RecoveryPingLedger),F<string>("session"),F<long>("epoch"),F<IAuthorityGate>("authority"),F<HashSet<string>>("seen"),F<Dictionary<string,long>>("ticks"),F<int>("capacity"));
            Fields(typeof(RecoverablePingRecord),F<string>("OwnerId"),F<string>("MapId"),F<string>("Action"),F<long>("Epoch"),F<int>("Team"),F<int>("Color"),F<int>("Catalog"),F<Float3>("Point"),F<Float3>("Normal"),F<double>("Created"),F<double>("Until"));
        }
        private static void Text(BinaryWriter w,string s)
        { Require(s!=null&&new System.Text.UTF8Encoding(false,true).GetByteCount(s)<=1024);RecoveryWire.Text(w,s); }
        private static string Text(BinaryReader r) { return RecoveryWire.Text(r); }
        private static string Id(BinaryReader r) { string s=Text(r);Require(s.Length>0&&s.Length<=128);return s; }
        private static double Number(BinaryReader r) { double x=r.ReadDouble();Require(Finite(x));return x; }
        private static void Vector(BinaryWriter w,Float3 p) { w.Write(p.X);w.Write(p.Y);w.Write(p.Z); }
        private static Float3 Vector(BinaryReader r) { return new Float3(RecoveryWire.Float(r),RecoveryWire.Float(r),RecoveryWire.Float(r)); }
        private static string[] Keys<T>(Dictionary<string,T> d) { string[] k=new string[d.Count];d.Keys.CopyTo(k,0);Array.Sort(k,StringComparer.Ordinal);return k; }
        private void Validate(CheckpointBinding b)
        {
            Require(b!=null&&b.Session==ledger.Session&&b.Epoch==epoch&&ledger.Epoch==epoch);
            foreach(string text in new string[]{b.Build,b.Catalog,b.Session,b.Owner,map})Require(text!=null&&new System.Text.UTF8Encoding(false,true).GetByteCount(text)<=1024);
            Require(teams>=0&&teams<=8&&!String.IsNullOrEmpty(map)&&Finite(cooldown)&&cooldown>=0&&Finite(lastTime)&&lastTime>=0);
            ledger.ValidateState();Require(last.Count==ledger.ActorCount&&marks.Count==last.Count&&last.Count<=HistoryLimit);
            foreach(string key in last.Keys)
            {
                Require(ledger.HasActor(key)&&marks.ContainsKey(key)&&Finite(last[key])&&last[key]>=0&&last[key]<=lastTime);
                RecoverablePingRecord m=marks[key];
                Require(m!=null&&m.OwnerId==key&&m.Team>=-1&&m.Team<8&&m.MapId==map&&m.Epoch==epoch&&m.Created==last[key]&&Finite(m.Until)&&m.Until==m.Created+30&&m.Color>=0&&m.Color<=8&&m.Catalog>=-1&&m.Catalog<=29&&(m.Color!=8||m.Catalog>=0)&&m.Action==Action(m.Color)&&m.Point.IsFinite&&m.Normal.IsFinite);
                double n=(double)m.Normal.X*m.Normal.X+(double)m.Normal.Y*m.Normal.Y+(double)m.Normal.Z*m.Normal.Z;Require(Math.Abs(n-1)<0.000001);
            }
        }
        public byte[] CaptureCheckpoint(CheckpointBinding binding)
        {
            Guard();Validate(binding);
            using(var stream=new BoundedPingStream()) using(var w=new BinaryWriter(stream))
            {
                Text(w,Codec);w.Write(1);binding.Write(w);Text(w,map);w.Write(teams);w.Write(cooldown);w.Write(lastTime);ledger.Write(w);
                string[] keys=Keys(last);w.Write(keys.Length);
                foreach(string key in keys)
                {
                    Text(w,key);w.Write(last[key]);RecoverablePingRecord m=marks[key];
                    Text(w,m.OwnerId);w.Write(m.Team);w.Write(m.Color);w.Write(m.Catalog);Vector(w,m.Point);Vector(w,m.Normal);w.Write(m.Created);w.Write(m.Until);
                }
                w.Flush();Require(stream.Length<=CheckpointLimit);return stream.ToArray();
            }
        }
        // expectedDigest and binding must come from a trusted external root receipt,
        // including freshness. A digest supplied alongside untrusted bytes is insufficient.
        public static RecoverablePingService RestoreCheckpoint(byte[] image,CheckpointBinding binding,string expectedDigest,IModeRoster restoredRoster,IAuthorityGate restoredGate)
        {
            Guard();Require(image!=null&&image.Length<=CheckpointLimit&&binding!=null&&restoredRoster!=null&&restoredGate!=null);
            byte[] bytes=(byte[])image.Clone();Require(RecoveryWire.Hash(bytes)==expectedDigest);
            using(var r=RecoveryWire.Reader(bytes))
            {
                Require(Text(r)==Codec&&r.ReadInt32()==1);binding.Check(r);
                string map=Text(r);int teams=r.ReadInt32();double cooldown=Number(r),time=Number(r);
                RecoveryPingLedger journal=RecoveryPingLedger.Read(r,binding.Session,binding.Epoch,restoredGate);
                var s=new RecoverablePingService(restoredRoster,restoredGate,binding.Session,binding.Epoch,map,teams,cooldown,journal.Capacity);
                s.ledger.CopyFrom(journal);s.lastTime=time;
                int count=RecoveryWire.Count(r,HistoryLimit);
                for(int i=0;i<count;i++)
                {
                    string key=Id(r);Require(!s.last.ContainsKey(key));double last=Number(r);string owner=Text(r);int team=r.ReadInt32(),color=r.ReadInt32(),catalog=r.ReadInt32();Float3 point=Vector(r),normal=Vector(r);double created=Number(r),until=Number(r);
                    Require(color>=0&&color<=8);s.last.Add(key,last);s.marks.Add(key,new RecoverablePingRecord(owner,map,binding.Epoch,team,color,catalog,point,normal,created,until));
                }
                RecoveryWire.End(r);s.Validate(binding);
                // Canonical ordering and exact bytes reject alternate encodings/unknown state.
                byte[] canonical=s.CaptureCheckpoint(binding);Require(canonical.Length==bytes.Length);
                for(int i=0;i<bytes.Length;i++)Require(bytes[i]==canonical[i]);return s;
            }
        }
    }
    internal sealed class BoundedPingStream : MemoryStream
    {
        public override void Write(byte[] buffer,int offset,int count)
        { RecoveryWire.Require(count>=0&&Position<=RecoverablePingService.CheckpointLimit-count);base.Write(buffer,offset,count); }
        public override void WriteByte(byte value)
        { RecoveryWire.Require(Position<RecoverablePingService.CheckpointLimit);base.WriteByte(value); }
    }
    internal sealed partial class RecoveryPingLedger
    {
        internal string Session { get { return session; } }
        internal long Epoch { get { return epoch; } }
        internal int Capacity { get { return capacity; } }
        internal int ActorCount { get { return ticks.Count; } }
        internal bool HasActor(string actor) { return ticks.ContainsKey(actor); }
        private static void Require(bool ok) { RecoveryWire.Require(ok); }
        private static string Actor(string key)
        {
            int colon=key.IndexOf(':');int n;Require(colon>0&&Int32.TryParse(key.Substring(0,colon),System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out n));
            // Definite assignment independent of short circuiting.
            n=Int32.Parse(key.Substring(0,colon),System.Globalization.CultureInfo.InvariantCulture);
            Require(n>0&&n<=128&&key.Length>colon+1+n&&key.Length-(colon+1+n)<=128);
            string actor=key.Substring(colon+1,n);Require(key==n.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+actor+key.Substring(colon+1+n));return actor;
        }
        internal void ValidateState()
        {
            Require(!String.IsNullOrEmpty(session)&&epoch>=0&&authority!=null&&capacity>0&&capacity<=RecoverablePingService.HistoryLimit&&seen.Count<=capacity&&ticks.Count<=seen.Count);
            var actors=new HashSet<string>(StringComparer.Ordinal);
            foreach(string key in seen){string a=Actor(key);Require(ticks.ContainsKey(a));actors.Add(a);}
            Require(actors.Count==ticks.Count);foreach(var p in ticks)Require(p.Key.Length>0&&p.Key.Length<=128&&p.Value>=0);
        }
        internal void Write(BinaryWriter w)
        {
            w.Write(capacity);var keys=new List<string>(seen);keys.Sort(StringComparer.Ordinal);w.Write(keys.Count);foreach(string k in keys)RecoveryWire.Text(w,k);
            keys=new List<string>(ticks.Keys);keys.Sort(StringComparer.Ordinal);w.Write(keys.Count);foreach(string k in keys){RecoveryWire.Text(w,k);w.Write(ticks[k]);}
        }
        internal static RecoveryPingLedger Read(BinaryReader r,string session,long epoch,IAuthorityGate gate)
        {
            int capacity=r.ReadInt32();Require(capacity>0&&capacity<=RecoverablePingService.HistoryLimit);var l=new RecoveryPingLedger(session,epoch,gate,capacity);
            int n=RecoveryWire.Count(r,capacity);for(int i=0;i<n;i++)Require(l.seen.Add(RecoveryWire.Text(r)));
            n=RecoveryWire.Count(r,capacity);for(int i=0;i<n;i++){string a=RecoveryWire.Text(r);Require(!l.ticks.ContainsKey(a));l.ticks.Add(a,r.ReadInt64());}l.ValidateState();return l;
        }
        internal void CopyFrom(RecoveryPingLedger l)
        { Require(seen.Count==0&&ticks.Count==0&&session==l.session&&epoch==l.epoch&&capacity==l.capacity&&Object.ReferenceEquals(authority,l.authority));foreach(string s in l.seen)seen.Add(s);foreach(var p in l.ticks)ticks.Add(p.Key,p.Value); }
    }
}

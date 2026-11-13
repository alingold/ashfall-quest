using System;
using System.IO;
using Ashfall.Core;
using Ashfall.Modules.Modes;
using Ashfall.Modules.ModesRecovery;
using Ashfall.Modules.Sessions;
using Ashfall.Modules.HealthStates;
using Ashfall.RecoveryContracts;
namespace Ashfall.Integration.MatchDirector
{
    public sealed partial class Director
    {
        // Explicit new lifecycle. This never exports or migrates the accepted Director.pings.
        public RecoveryPingOwner CreateRecoveryPingOwner(double cooldown,int journalCapacity)
        { return new RecoveryPingOwner(this,cooldown,journalCapacity); }
        public sealed class RecoveryPingOwner
        {
            private readonly Director director;
            private readonly RecoverablePingService ping;
            internal RecoveryPingOwner(Director d,double cooldown,int capacity)
            {director=d;ping=new RecoverablePingService(d,d.gate,d.session.Id,d.Epoch,d.session.Rules.MapId,d.session.Rules.Teams,cooldown,capacity);}
            public RuleResult Place(CommandContext actor,Float3 point,Float3 normal,int color,int catalog)
            {var r=director.Time();if(!r.Accepted)return r;return ping.Place(actor,director.Stage,point,normal,color,catalog,director.lastTime);}
            public RecoverablePingRecord[] Visible(string viewer)
            {return ping.Visible(viewer,director.session.Rules.MapId,director.Epoch,director.clock.Now);}
            public byte[] CapturePartialArchive(CheckpointBinding binding,byte[] physicalReceiptFamily)
            {
                byte[] d=director.CaptureHistoricalArchive(binding),p=ping.CaptureCheckpoint(binding);
                ReceiptFamilyArchive.Restore(physicalReceiptFamily);
                MatchPingHeaders.Check(d,p,binding,director.lastTime,director.session.Rules.MapId,director.session.Rules.Teams);
                return MatchPingHeaders.Pack(binding,director.lastTime,d,p,physicalReceiptFamily);
            }
        }
        public sealed class QuarantinedPingArchive
        {
            private readonly QuarantinedArchive director;
            private readonly RecoverablePingService ping;
            private readonly ReceiptFamilyArchive physical;
            internal QuarantinedPingArchive(QuarantinedArchive d,RecoverablePingService p,ReceiptFamilyArchive f){director=d;ping=p;physical=f;}
            public string AdoptionBlocker {get{return "partial-owner-time-only/health-combat-publication-time-and-physical-source-binding/root-durable-authority-required";}}
            public ScoreRow[] Scores(){return director.Scores();}
            public HealthSnapshot HistoricalLife(string id){return director.HistoricalLife(id);}
            public long PendingScore {get{return director.PendingScore;}}
            public long ScoredThrough {get{return director.ScoredThrough;}}
            public int PhysicalPending(string session,long epoch){return physical.OpenJournal().Pending(session,epoch).Length;}
            public RecoverablePingRecord[] Visible(string viewer,string map,long epoch,double now){return ping.Visible(viewer,map,epoch,now);}
            // Probe only mutates a NEW private quarantined owner; cannot route scores or publish.
            public RuleResult ProbePing(CommandContext c,RoundStage stage,Float3 point,Float3 normal,int color,int catalog,double now)
            {return ping.Place(c,stage,point,normal,color,catalog,now);}
        }
        public static QuarantinedPingArchive RestorePartialPingArchive(byte[] image,string trustedDigest,CheckpointBinding binding,ModeRules rules,string principal,int capacity,IMatchClock clock,IAuthorityGate gate,ILogicalPlayerIdentity identity,ISessionClock sessionClock,IReconnectTokens tokens,ISpawnWorld world,ISessionManagement management,HealthService health,Ashfall.Integration.CombatPublication.HealthPublicationLane publication)
        {
            RecoveryWire.Require(image!=null&&image.Length<=RecoveryWire.Limit&&RecoveryWire.Hash(image)==trustedDigest);
            byte[] d,p,f;double time;
            using(var r=RecoveryWire.Reader(image)){RecoveryWire.Require(RecoveryWire.Text(r)==MatchPingHeaders.Codec);binding.Check(r);time=r.ReadDouble();d=RecoveryWire.Blob(r);p=RecoveryWire.Blob(r);f=RecoveryWire.Blob(r);RecoveryWire.End(r);}
            byte[] sessionBytes=MatchPingHeaders.Check(d,p,binding,time,null,rules.Teams);
            var s=Session.RestoreCheckpoint(sessionBytes,binding,gate,sessionClock,tokens,world,management);
            var archive=RestoreHistoricalArchive(d,RecoveryWire.Hash(d),binding,s,rules,principal,capacity,clock,gate,identity,health,publication);
            var ping=archive.RestorePing(p,binding,gate);
            var family=ReceiptFamilyArchive.Restore(f);
            return new QuarantinedPingArchive(archive,ping,family);
        }
        // No assertion of a common Health/Combat/publication logical instant exists in their
        // current owner ports. Caller time/digest flags cannot establish that missing proof.
        public static void RequireCompleteRecovery()
        {throw new InvalidOperationException("owner-logical-time-and-physical-source-proof-unavailable; global-adoption-refused");}
    }
    internal static class MatchPingHeaders
    {
        internal const string Codec="PartialDirectorPing/1";
        private static void Equal(double actual,double expected){RecoveryWire.Require(!Double.IsNaN(expected)&&!Double.IsInfinity(expected)&&expected>=0&&actual==expected);}
        private static void Rules(BinaryReader r){RecoveryWire.Text(r);r.ReadInt32();r.ReadInt32();r.ReadInt32();r.ReadInt32();for(int i=0;i<4;i++)RecoveryWire.Bool(r);RecoveryWire.Float(r);}
        // Read only documented versioned header fields. Actual owner decoders validate full
        // state independently. Unknown owner codec revisions refuse rather than guessing.
        internal static byte[] Check(byte[] d,byte[] p,CheckpointBinding b,double time,string expectedMap,int teams)
        {
            byte[] session,round;
            using(var r=RecoveryWire.Reader(d)){
                RecoveryWire.Require(RecoveryWire.Text(r)==Director.ArchiveCodec);b.Check(r);RecoveryWire.Require(RecoveryWire.Text(r)=="PingUnsupported/1");Rules(r);r.ReadInt32();r.ReadInt32();session=RecoveryWire.Blob(r);round=RecoveryWire.Bool(r)?RecoveryWire.Blob(r):null;RecoveryWire.Blob(r);r.ReadInt32();RecoveryWire.Text(r);for(int i=0;i<4;i++)r.ReadInt64();Equal(r.ReadDouble(),time);
            }
            string map;
            using(var r=RecoveryWire.Reader(session)){
                RecoveryWire.Require(RecoveryWire.Text(r)==Session.CheckpointCodec);b.Check(r);RecoveryWire.Text(r);map=RecoveryWire.Text(r);RecoveryWire.Text(r);RecoveryWire.Text(r);RecoveryWire.Require(r.ReadInt32()==teams);r.ReadInt32();for(int i=0;i<4;i++)RecoveryWire.Bool(r);r.ReadDouble();r.ReadInt32();Equal(r.ReadDouble(),time);
            }
            RecoveryWire.Require(expectedMap==null||map==expectedMap);
            if(round!=null)using(var r=RecoveryWire.Reader(round)){
                RecoveryWire.Require(RecoveryWire.Text(r)==Ashfall.Integration.MatchRounds.RoundController.CheckpointCodec);b.Check(r);Rules(r);RecoveryWire.Text(r);r.ReadInt32();r.ReadInt64();r.ReadDouble();Equal(r.ReadDouble(),time);
            }
            using(var r=RecoveryWire.Reader(p)){
                RecoveryWire.Require(RecoveryWire.Text(r)==RecoverablePingService.Codec&&r.ReadInt32()==1);b.Check(r);RecoveryWire.Require(RecoveryWire.Text(r)==map&&r.ReadInt32()==teams);r.ReadDouble();Equal(r.ReadDouble(),time);
            }
            return session;
        }
        internal static byte[] Pack(CheckpointBinding b,double time,byte[] d,byte[] p,byte[] f)
        {
            long sum=(long)d.Length+p.Length+f.Length;RecoveryWire.Require(sum<=RecoveryWire.Limit);
            using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){RecoveryWire.Text(w,Codec);b.Write(w);w.Write(time);RecoveryWire.Blob(w,d);RecoveryWire.Blob(w,p);RecoveryWire.Blob(w,f);w.Flush();RecoveryWire.Require(m.Length<=RecoveryWire.Limit);return m.ToArray();}
        }
    }
}


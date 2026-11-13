using System;
using System.IO;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Modes;
using Ashfall.Modules.Sessions;
using Ashfall.Modules.HealthStates;
using Ashfall.RecoveryContracts;
using RoundController=Ashfall.Integration.MatchRounds.RoundController;
namespace Ashfall.Integration.MatchDirector
{
    public sealed partial class Director
    {
        public const string ArchiveCodec="DirectorHistorical/1";
        // Deliberately no Director/Session/producer reference or adoption method is exposed.
        public sealed class QuarantinedArchive
        {
            private readonly Director value;
            internal QuarantinedArchive(Director d){value=d;}
            internal Ashfall.Modules.ModesRecovery.RecoverablePingService RestorePing(byte[] bytes,CheckpointBinding b,IAuthorityGate gate)
            {return Ashfall.Modules.ModesRecovery.RecoverablePingService.RestoreCheckpoint(bytes,b,RecoveryWire.Hash(bytes),value,gate);}
            public string AdoptionBlocker {get{return "ping-state/cross-owner-time/root-durable-authority-required";}}
            public ScoreRow[] Scores(){return value.Scores();}
            public long LifeSequence(string id){return value.LifeSequence(id);}
            public long DeliveredThrough {get{return value.eventSequence;}}
            public long ScoredThrough {get{return value.scoredEvent;}}
            public long PendingScore {get{return value.pendingScoreEvent;}}
            public bool HasRetainedEligibility {get{return value.retainedScore!=null;}}
            public HealthSnapshot HistoricalLife(string id){Life l;return value.lives.TryGetValue(id,out l)?l.State:null;}
            public byte[] Recapture(CheckpointBinding b){return value.CaptureHistoricalArchive(b);}
        }
        private static void Rules(BinaryWriter w,ModeRules rules)
        {RecoveryWire.Text(w,rules.Mode);w.Write(rules.Teams);w.Write(rules.ScoreLimit);w.Write(rules.TimeLimit);w.Write(rules.Rounds);w.Write(rules.Catalog);w.Write(rules.InfiniteAmmo);w.Write(rules.InfiniteBuilds);w.Write(rules.ZoneEnabled);w.Write(rules.Damage);}
        private static void Rules(BinaryReader r,ModeRules rules)
        {RecoveryWire.Require(RecoveryWire.Text(r)==rules.Mode&&r.ReadInt32()==rules.Teams&&r.ReadInt32()==rules.ScoreLimit&&r.ReadInt32()==rules.TimeLimit&&r.ReadInt32()==rules.Rounds&&RecoveryWire.Bool(r)==rules.Catalog&&RecoveryWire.Bool(r)==rules.InfiniteAmmo&&RecoveryWire.Bool(r)==rules.InfiniteBuilds&&RecoveryWire.Bool(r)==rules.ZoneEnabled&&RecoveryWire.Float(r)==rules.Damage);}
        private static void Actor(BinaryWriter w,ModeActor a)
        {RecoveryWire.Text(w,a.Id);RecoveryWire.Text(w,a.Name);RecoveryWire.Text(w,a.Life);w.Write(a.Team);w.Write(a.Loaded);w.Write(a.Spectator);}
        private static ModeActor Actor(BinaryReader r)
        {string id=RecoveryWire.Text(r),name=RecoveryWire.Text(r),life=RecoveryWire.Text(r);int team=r.ReadInt32();RecoveryWire.Require(id.Length>0&&team>=-1&&team<8&&(life=="alive"||life=="dead"||life=="downed"||life=="spectator"));return new ModeActor(id,name,team,life,RecoveryWire.Bool(r),RecoveryWire.Bool(r));}
        private static long Nonnegative(BinaryReader r){long n=r.ReadInt64();RecoveryWire.Require(n>=0);return n;}
        private static double TimeValue(BinaryReader r){double n=r.ReadDouble();RecoveryWire.Require(!Double.IsNaN(n)&&!Double.IsInfinity(n)&&n>=0);return n;}
        // This is a partial historical owner image. Ping is expressly unsupported, never reset/replayed as restore.
        // Root must authenticate a common digest over this image AND all other original owner/physical bytes.
        public byte[] CaptureHistoricalArchive(CheckpointBinding b)
        {
            ArchiveFields();
            RecoveryWire.Require(b!=null&&b.Session==session.Id&&b.Epoch==Epoch&&!scoring&&eventSequence<=b.Sequence&&scoredEvent<=b.Sequence&&pendingScoreEvent<=b.Sequence);
            var a=attribution as CommittedAttribution;RecoveryWire.Require(a!=null&&(projection==null||projection is SessionHealthProjection));
            var snapshots=new List<HealthSnapshot>();foreach(var pair in lives)snapshots.Add(pair.Value.State);
            var events=new List<HealthTransition>();if(retainedTransition!=null)events.Add(retainedTransition);
            if(producer!=null&&producer.ArchivePending!=null&&!events.Contains(producer.ArchivePending))events.Add(producer.ArchivePending);
            // Same sequence must retain one exact object; owner archive rejects conflicting/duplicate identities.
            byte[] health=MatchCheckpointRecords.Capture(b,snapshots.ToArray(),events.ToArray());
            using(var m=new MemoryStream())using(var w=new BinaryWriter(m))
            {
                RecoveryWire.Text(w,ArchiveCodec);b.Write(w);RecoveryWire.Text(w,"PingUnsupported/1");
                Rules(w,initialRules);w.Write(round==null?initialRules.ScoreLimit:round.Rules.ScoreLimit);w.Write(round==null?initialRules.TimeLimit:round.Rules.TimeLimit);
                RecoveryWire.Blob(w,session.CaptureCheckpoint(b));w.Write(round!=null);if(round!=null)RecoveryWire.Blob(w,round.CaptureCheckpoint(b));
                RecoveryWire.Blob(w,health);w.Write(capacity);RecoveryWire.Text(w,principal);w.Write(command);w.Write(eventSequence);w.Write(scoredEvent);w.Write(pendingScoreEvent);w.Write(lastTime);w.Write(lastTick);w.Write(limitAdmissions);RecoveryWire.Text(w,Fault);
                w.Write(lives.Count);foreach(var p in lives){RecoveryWire.Text(w,p.Key);w.Write(p.Value.Sequence);}
                w.Write(members.Count);foreach(string id in members)RecoveryWire.Text(w,id);
                w.Write(connections.Count);foreach(var p in connections){RecoveryWire.Text(w,p.Key);RecoveryWire.Text(w,p.Value);}
                w.Write(retainedActors.Count);foreach(var p in retainedActors){RecoveryWire.Text(w,p.Key);Actor(w,p.Value);}
                w.Write(retainedTransition!=null);if(retainedTransition!=null)w.Write(events.IndexOf(retainedTransition));
                w.Write(retainedScore!=null);if(retainedScore!=null)RecoveryWire.Blob(w,round.CaptureCommittedArchive(retainedScore));
                w.Write(retainedKiller!=null);if(retainedKiller!=null)RecoveryWire.Text(w,retainedKiller);w.Write(retainedTeam);
                a.WriteArchive(w);w.Write(projection!=null);if(projection!=null)w.Write(((SessionHealthProjection)projection).ArchiveCommand);w.Write(journal!=null);if(journal!=null)RecoveryWire.Blob(w,journal.CaptureArchive(session.Id,Epoch));
                w.Write(producer!=null);if(producer!=null)producer.WriteArchive(w,events);
                w.Flush();RecoveryWire.Require(m.Length<=RecoveryWire.Limit);return m.ToArray();
            }
        }
        public static QuarantinedArchive RestoreHistoricalArchive(byte[] image,string trustedDigest,CheckpointBinding b,Session quarantinedSession,ModeRules rules,string principal,int capacity,IMatchClock clock,IAuthorityGate gate,ILogicalPlayerIdentity identity,HealthService quarantinedHealth,Ashfall.Integration.CombatPublication.HealthPublicationLane quarantinedPublication)
        {
            ArchiveFields();
            RecoveryWire.Require(image!=null&&image.Length<=RecoveryWire.Limit&&RecoveryWire.Hash(image)==trustedDigest);
            using(var r=RecoveryWire.Reader(image))
            {
                RecoveryWire.Require(RecoveryWire.Text(r)==ArchiveCodec);b.Check(r);RecoveryWire.Require(RecoveryWire.Text(r)=="PingUnsupported/1");
                Rules(r,rules);int score=RecoveryWire.Count(r,500),time=RecoveryWire.Count(r,3600);ModeRules liveRules=rules.WithLimits(score,time);
                byte[] sessionBytes=RecoveryWire.Blob(r);RecoveryWire.Require(RecoveryWire.Hash(sessionBytes)==RecoveryWire.Hash(quarantinedSession.CaptureCheckpoint(b)));
                byte[] rounds=RecoveryWire.Bool(r)?RecoveryWire.Blob(r):null;
                HealthSnapshot[] snapshots;HealthTransition[] events;MatchCheckpointRecords.Restore(RecoveryWire.Blob(r),b,out snapshots,out events);
                RecoveryWire.Require(r.ReadInt32()==capacity&&RecoveryWire.Text(r)==principal);
                var attribution=new CommittedAttribution(quarantinedHealth);
                var x=new Director(quarantinedSession,rules,principal,clock,gate,attribution,capacity,identity,null,null);x.pings=null;
                if(rounds!=null)x.round=RoundController.RestoreCheckpoint(rounds,b,liveRules,x,principal,gate,capacity+ScoreAdmissions);
                x.command=Nonnegative(r);x.eventSequence=Nonnegative(r);x.scoredEvent=Nonnegative(r);x.pendingScoreEvent=Nonnegative(r);x.lastTime=TimeValue(r);x.lastTick=r.ReadInt64();x.limitAdmissions=RecoveryWire.Count(r,capacity);x.Fault=RecoveryWire.Text(r);
                RecoveryWire.Require(x.lastTick>=-1&&x.eventSequence<=b.Sequence&&x.scoredEvent<=b.Sequence&&x.pendingScoreEvent<=b.Sequence);
                int n=RecoveryWire.Count(r,256);RecoveryWire.Require(n==snapshots.Length);for(int i=0;i<n;i++){string key=RecoveryWire.Text(r);long seq=Nonnegative(r);RecoveryWire.Require(key==snapshots[i].Id);x.lives.Add(key,new Life{State=snapshots[i],Sequence=seq});}
                n=RecoveryWire.Count(r,256);for(int i=0;i<n;i++)RecoveryWire.Require(x.members.Add(RecoveryWire.Text(r)));
                n=RecoveryWire.Count(r,256);var aliases=new HashSet<string>(StringComparer.Ordinal);for(int i=0;i<n;i++){string k=RecoveryWire.Text(r),v=RecoveryWire.Text(r);RecoveryWire.Require(x.lives.ContainsKey(k)&&aliases.Add(v));x.connections.Add(k,v);}
                n=RecoveryWire.Count(r,256);for(int i=0;i<n;i++){string k=RecoveryWire.Text(r);ModeActor actor=Actor(r);RecoveryWire.Require(k==actor.Id);x.retainedActors.Add(k,actor);}
                if(RecoveryWire.Bool(r))x.retainedTransition=events[RecoveryWire.Count(r,events.Length-1)];
                if(RecoveryWire.Bool(r)){RecoveryWire.Require(x.round!=null&&x.retainedTransition!=null);x.round=RoundController.RestoreCheckpointWithCommittedArchive(rounds,b,liveRules,x,principal,gate,capacity+ScoreAdmissions,RecoveryWire.Blob(r),x.retainedTransition,out x.retainedScore);}
                x.retainedKiller=RecoveryWire.Bool(r)?RecoveryWire.Text(r):null;x.retainedTeam=r.ReadInt32();RecoveryWire.Require(x.retainedTeam>=-1&&x.retainedTeam<8);
                x.attribution=CommittedAttribution.ReadArchive(r,quarantinedHealth);
                if(RecoveryWire.Bool(r)){var project=new SessionHealthProjection(quarantinedSession,clock,principal);project.RestoreArchiveCommand(Nonnegative(r));x.projection=project;}
                if(RecoveryWire.Bool(r))x.journal=ScoreReceiptJournal.RestoreArchive(RecoveryWire.Blob(r),b.Session,b.Epoch);
                if(RecoveryWire.Bool(r))x.producer=HealthProducerDrain.ReadArchive(r,events,quarantinedHealth,quarantinedPublication);
                RecoveryWire.End(r);
                RecoveryWire.Require((x.retainedScore==null)==(x.retainedTransition==null));
                RecoveryWire.Require(x.retainedTransition==null||x.pendingScoreEvent==x.retainedTransition.Sequence);
                RecoveryWire.Require(RecoveryWire.Hash(image)==RecoveryWire.Hash(x.CaptureHistoricalArchive(b)));
                return new QuarantinedArchive(x);
            }
        }
    }
}


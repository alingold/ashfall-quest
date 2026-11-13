using CheckpointWire = Ashfall.Integration.CheckpointWire;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Persistence;
namespace Ashfall.Integration.CombatPublication
{
    // Single host producer lane. Accepted Persistence owns atomic replacement and durable flush.
    // Legacy mode is an outbox only. Opt-in sourceBinding writes a coherent cold checkpoint.
    // Existing objects never reopen for mutation; use TryRestoreCold to construct new objects.
    public sealed partial class HealthPublicationLane
    {
        public sealed class Packet
        {
            public readonly long Sequence;
            public readonly string Id, Payload;
            internal Packet(long sequence,string id,string payload){Sequence=sequence;Id=id;Payload=payload;}
        }
        readonly HealthService health;readonly CombatHealthBridge bridge;readonly AtomicSettingsStore store;
        readonly string session,host;readonly long epoch;readonly int capacity;
        readonly SortedDictionary<long,Packet> pending=new SortedDictionary<long,Packet>();
        long ack,offered,high;bool failed,reopened,busy,dirty;
        readonly string checkpointBinding;
        CommandContext checkpointContext;
        public sealed class CheckpointReceipt
        {
            public readonly string SourceBinding,Digest;
            public readonly long Generation,HealthSequence,AcknowledgedThrough;
            internal CheckpointReceipt(string binding,string digest,long generation,long sequence,long acknowledged)
            {SourceBinding=binding;Digest=digest;Generation=generation;HealthSequence=sequence;AcknowledgedThrough=acknowledged;}
        }
        // Retain this single immutable object when recording a trusted coordinator receipt.
        public CheckpointReceipt SuccessfulCheckpoint {get;private set;}
        public string CheckpointDigest {get{return SuccessfulCheckpoint==null?null:SuccessfulCheckpoint.Digest;}}
        public long CheckpointGeneration {get{return SuccessfulCheckpoint==null?0:SuccessfulCheckpoint.Generation;}}
        public bool RecoveryRequired {get{return reopened||failed;}}
        public HealthPublicationLane(HealthService service,CombatHealthBridge combat,AtomicSettingsStore persistence,
            string sessionId,long authorityEpoch,string hostActor="host",int eventCapacity=2048,string sourceBinding=null)
            : this(service,combat,persistence,sessionId,authorityEpoch,hostActor,eventCapacity,sourceBinding,false) {}
        // The cold factory already owns an authenticated immutable image. Never reload it.
        HealthPublicationLane(HealthService service,CombatHealthBridge combat,AtomicSettingsStore persistence,
            string sessionId,long authorityEpoch,string hostActor,int eventCapacity,string sourceBinding,bool cold)
        {
            if(service==null||combat==null||persistence==null||String.IsNullOrEmpty(sessionId)||authorityEpoch<0||String.IsNullOrEmpty(hostActor)||eventCapacity<65||eventCapacity>2048)throw new ArgumentException("publication lane");
            health=service;bridge=combat;store=persistence;session=sessionId;epoch=authorityEpoch;host=hostActor;capacity=eventCapacity;
            checkpointBinding=sourceBinding;checkpointContext=new CommandContext(session,host,"checkpoint-initialize",0,epoch,true);
            if(cold)return;
            if(!store.Load(false).Accepted)throw new IOException("Publication journal load");
            string saved;if(store.TryRead("combat-publication/1",out saved)){Decode(saved);reopened=true;}
            else if(store.TryRead("combat-checkpoint/2",out saved)||store.TryRead("combat-checkpoint-sha/2",out saved))reopened=true;
            else if(!Save())throw new IOException("Publication journal initialization");
        }
        bool Host(CommandContext c){return c.ActorId==host&&c.SessionId==session&&c.AuthorityEpoch==epoch&&health.Authorize(c).Accepted;}
        bool Save()
        {
            if(RecoveryRequired)return false;
            try {
                string journal=Encode(),savedDigest=null;long generation=CheckpointGeneration;
                byte[] checkpoint=null;
                if(checkpointBinding!=null) {
                    if(generation==Int64.MaxValue)throw new InvalidOperationException("checkpoint-generation-exhausted");
                    generation++;
                    checkpoint=bridge.CreateColdCheckpoint(checkpointContext,checkpointBinding,journal,generation);
                    savedDigest=CheckpointWire.Hash(checkpoint);
                }
                // Finish capture before staging any persistence mutation.
                store.Write("combat-publication/1",journal);
                if(checkpointBinding!=null) {
                    store.Write("combat-checkpoint/2",Convert.ToBase64String(checkpoint));
                    store.Write("combat-checkpoint-sha/2",savedDigest);
                }
                if(store.Flush().Accepted){if(savedDigest!=null)SuccessfulCheckpoint=new CheckpointReceipt(checkpointBinding,savedDigest,generation,MatchCheckpointRecords.Sequence(health),ack);return true;}
            }catch(IOException){}catch(InvalidOperationException){}
            failed=true;return false;
        }
        bool Capture(CommandContext c)
        {
            var batch=bridge.Pump(c);
            foreach(var e in batch.Events) {
                if(e.SessionId!=session||e.AuthorityEpoch!=epoch){failed=true;return false;}
                if(e.Sequence<=ack)continue;
                string payload=Transition(e);Packet prior;
                if(pending.TryGetValue(e.Sequence,out prior)){if(prior.Payload!=payload){failed=true;return false;}continue;}
                if(pending.Count>=capacity){failed=true;return false;}
                pending.Add(e.Sequence,new Packet(e.Sequence,session+"/"+epoch+"/health/"+e.Sequence,payload));high=Math.Max(high,e.Sequence);
            }
            if(!Save())return false;
            // Bridge ownership transfers only AFTER durable save. Dequeue alone is never durability.
            if(batch.Events.Length>0&&!bridge.Acknowledge(c,batch.Events[batch.Events.Length-1].Sequence).Accepted){failed=true;return false;}
            return batch.PendingHealthEvents==0;
        }
        RuleResult Run(CommandContext c,Func<RuleResult> producer)
        {
            return health.Serialize(()=>{
                if(busy||!Host(c))return RuleResult.Reject("publication-host");
                if(RecoveryRequired)return RuleResult.Reject("health-checkpoint-required");
                if(admissionClosed)return RuleResult.Reject("producer-admission-closed");
                busy=true;
                checkpointContext=c;
                try {
                    if(!Capture(c)||pending.Count>capacity-65)return RuleResult.Reject("publication-pressure");
                    dirty=true;if(!Save())return RuleResult.Reject("journal-before-commit");
                    RuleResult result=producer(); // Trusted owned wrapper only; never a caller callback.
                    if(!Capture(c))return RuleResult.Reject("journal-after-commit-recovery-required");
                    dirty=false;if(!Save())return RuleResult.Reject("journal-after-commit-recovery-required");
                    return result;
                } catch {failed=true;throw;} finally {busy=false;}
            });
        }
        // Servicing also captures rejected-operation expiry transitions. Pressure precedes every producer.
        public RuleResult Register(CommandContext c,string target,int team,bool spectator){return Run(c,()=>health.Register(c,target,team,spectator));}
        public RuleResult Spawn(CommandContext c,string target,long revision,float hp,float shield){return Run(c,()=>health.Spawn(c,target,revision,hp,shield));}
        public RuleResult SetTeam(CommandContext c,string target,long revision,int team){return Run(c,()=>health.SetTeam(c,target,revision,team));}
        public RuleResult SetConnected(CommandContext c,string target,long revision,bool connected){return Run(c,()=>health.SetConnected(c,target,revision,connected));}
        public RuleResult SamplePaddles(CommandContext c,CommandContext actor){return Run(c,()=>health.SamplePaddles(actor));}
        public RuleResult CancelRevive(CommandContext c,CommandContext actor){return Run(c,()=>health.CancelRevive(actor));}
        public RuleResult Revive(CommandContext c,CommandContext actor,string target,long revision){return Run(c,()=>health.Revive(actor,target,revision));}
        public RuleResult Advance(CommandContext c){return Run(c,()=>health.Advance(c));}
        public RuleResult Synchronize(CommandContext c){return Run(c,()=>health.Synchronize(c));}
        public RuleResult PrepareDamage(CommandContext c){return Run(c,()=>health.PrepareDamage(c));}
        public RuleResult Damage(CommandContext c,string logical,string target,long revision,float hp,float shield,bool revivable)
        {return Run(c,()=>health.ApplyDamage(c,logical,target,revision,hp,shield,revivable).Result);}
        public RuleResult Combat(CommandContext c,CommandContext delivery,string hit,long tick,bool revivable)
        {return Run(c,()=>bridge.Apply(delivery,hit,tick,revivable).Result);}
        public RuleResult Gain(CommandContext c,CommandContext actor,string logical,string target,long revision,long life,long connection,float hp,float shield)
        {
            return Run(c,()=>{
                var old=health.FindGain(actor,logical,target);if(old!=null)return old.Outcome==GainOutcome.Failed?RuleResult.Reject("gain"):RuleResult.Accept();
                HealthService.PreparedGain gain;
                if(!health.PrepareGain(actor,logical,target,revision,life,connection,hp,shield,out gain))return RuleResult.Reject("gain-prepare");
                if(!gain.Validate()){gain.Abort();return RuleResult.Reject("gain-stale");}
                return gain.CommitValidated().Outcome==GainOutcome.Failed?RuleResult.Reject("gain"):RuleResult.Accept();
            });
        }
        public Packet[] Offer(CommandContext c)
        {
            return health.Serialize(()=>{
                if(busy||!Host(c)||RecoveryRequired)throw new InvalidOperationException("publication-host-or-journal");
                checkpointContext=c;
                if(!Capture(c))throw new IOException("capture before offer");
                var result=new Packet[pending.Count];pending.Values.CopyTo(result,0);
                if(result.Length>0)offered=Math.Max(offered,result[result.Length-1].Sequence);
                if(!Save())throw new IOException("offer journal");return result;
            });
        }
        public RuleResult Acknowledge(CommandContext c,long sequence)
        {
            return health.Serialize(()=>{
                if(busy||!Host(c)||RecoveryRequired||sequence<ack||sequence>offered)return RuleResult.Reject("publication-ack");
                var remove=new List<long>();foreach(var p in pending)if(p.Key<=sequence)remove.Add(p.Key);
                foreach(long key in remove)pending.Remove(key);ack=sequence;
                return Save()?RuleResult.Accept():RuleResult.Reject("ack-journal");
            });
        }
        string Encode()
        {
            using(var m=new MemoryStream())using(var w=new BinaryWriter(m,Encoding.UTF8)) {
                w.Write(2);w.Write(session);w.Write(epoch);w.Write(host);w.Write(capacity);w.Write(admissionClosed);w.Write(closeNextEpoch);w.Write(closeNextBinding??"");w.Write(dirty);w.Write(ack);w.Write(offered);w.Write(high);w.Write(pending.Count);
                foreach(var p in pending){w.Write(p.Key);w.Write(p.Value.Id);w.Write(p.Value.Payload);}w.Flush();return Convert.ToBase64String(m.ToArray());
            }
        }
        void Decode(string saved)
        {
            if(saved==null||saved.Length>CheckpointWire.MaximumBytes*2)throw new IOException("Journal size");
            using(var m=new MemoryStream(Convert.FromBase64String(saved)))using(var r=new BinaryReader(m,Encoding.UTF8)) {
                int version=r.ReadInt32();
                if((version!=1&&version!=2)||CheckpointWire.Text(r)!=session||r.ReadInt64()!=epoch||CheckpointWire.Text(r)!=host||r.ReadInt32()!=capacity)throw new IOException("Journal identity");
                if(version==2){admissionClosed=r.ReadBoolean();closeNextEpoch=r.ReadInt64();closeNextBinding=CheckpointWire.Text(r,128,true);if(admissionClosed?(closeNextEpoch<=epoch||!CheckpointWire.Binding(closeNextBinding)):(closeNextEpoch!=0||closeNextBinding!=""))throw new IOException("Journal closure");}
                dirty=r.ReadBoolean();ack=r.ReadInt64();offered=r.ReadInt64();high=r.ReadInt64();int count=r.ReadInt32();
                if(ack<0||offered<ack||high<offered||count<0||count>capacity)throw new IOException("Journal bounds");
                long previous=ack;for(int i=0;i<count;i++){long seq=r.ReadInt64();string id=CheckpointWire.Text(r),payload=CheckpointWire.Text(r,16384,false);if(seq<=previous||seq>high||id!=session+"/"+epoch+"/health/"+seq)throw new IOException("Journal sequence");pending.Add(seq,new Packet(seq,id,payload));previous=seq;}
                if(m.Position!=m.Length)throw new IOException("Journal trailing bytes");
            }
        }
        static string Transition(HealthTransition t)
        {
            var s=t.After;using(var m=new MemoryStream())using(var w=new BinaryWriter(m,Encoding.UTF8)) {
                w.Write(1);w.Write(t.SessionId);w.Write(t.AuthorityEpoch);w.Write(t.Sequence);w.Write(t.Kind);w.Write(t.BeforeRevision);
                w.Write(s.Id);w.Write(s.Team);w.Write(s.Deaths);w.Write(s.Revision);w.Write((int)s.Life);w.Write(s.Health);w.Write(s.Shield);
                w.Write(s.DownedUntil);w.Write(s.RespawnAt);w.Write(s.ProtectionUntil);w.Write(s.ReadyUntil);w.Write(s.Connected);w.Write(s.Charge);w.Write(s.LifeGeneration);w.Write(s.ConnectionGeneration);
                w.Flush();return Convert.ToBase64String(m.ToArray());
            }
        }
    }
}



using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Combat;
using Ashfall.Modules.HealthStates;
namespace Ashfall.Integration
{
    public sealed partial class CombatHealthBridge
    {
        private CombatHealthBridge(HealthService service,IHealthWorld world,ICombatEvidence facts,
            IAuthorityGate authority,IInteractionQuery query,string session,long epoch,string host,int cap,
            BinaryReader reader,out HostHit[] bindings):this(service,world,facts,authority,query,session,epoch,host,cap)
        {rules=CombatRules.RestoreArchive(reader,session,epoch,256,cap,authority,this,query,host,out bindings);}
        internal byte[] CreateColdCheckpoint(CommandContext c,string binding,string journal,long generation)
        {
            return health.Serialize(()=>{
                byte[] state=health.CreateCheckpoint(c,binding);
                using(var m=new MemoryStream())using(var w=new BinaryWriter(m,Encoding.UTF8)) {
                    w.Write(3);w.Write(binding);w.Write(generation);w.Write(session);w.Write(epoch);w.Write(host);w.Write(capacity);w.Write(journal);w.Write(state.Length);w.Write(state);
                    var index=rules.CaptureArchive(w,collisionBindings.Keys);
                    w.Write(lastTick);w.Write(offeredSequence);w.Write(collisionBindings.Count);
                    foreach(var p in collisionBindings){w.Write(index[p.Key]);w.Write(p.Value.Life);w.Write(p.Value.Connection);}
                    w.Write(hits.Count);foreach(var p in hits){w.Write(p.Key);w.Write(index[p.Value.Hit]);}
                    w.Write(completed.Count);foreach(var p in completed){w.Write(p.Key);w.Write(completedPrincipals[p.Key]);CheckpointWire.Result(w,p.Value.Result);CheckpointWire.Damage(w,p.Value.Damage);}
                    w.Write(deliveries.Count);foreach(var p in deliveries){w.Write(p.Key);w.Write(p.Value.Hit);w.Write(p.Value.Tick);}
                    w.Write(outbox.Count);foreach(var p in outbox)CheckpointWire.Transition(w,p.Value);
                    w.Flush();if(m.Length>CheckpointWire.MaximumBytes)throw new InvalidOperationException("composed-checkpoint-size");return m.ToArray();
                }
            });
        }
        internal static bool TryColdCheckpoint(CommandContext c,string binding,string digest,byte[] bytes,string expectedJournal,
            int teamCount,bool respawn,IAuthorityGate authority,IHealthWorld world,IInteractionQuery query,ICombatEvidence facts,
            out HealthService healthResult,out CombatHealthBridge bridgeResult,out long generation,double downed=60,double delay=5,double protection=3)
        {
            healthResult=null;bridgeResult=null;generation=0;
            if(c.ActorId!="host"||!c.IsAuthority||authority==null||!authority.Validate(c).Accepted||!CheckpointWire.Matches(bytes,digest))return false;
            try {
                using(var m=new MemoryStream(bytes,false))using(var r=new BinaryReader(m,Encoding.UTF8)) {
                    if(r.ReadInt32()!=3||CheckpointWire.Text(r)!=binding)throw new InvalidDataException("composed-schema-binding");
                    long decodedGeneration=CheckpointWire.Number(r,1,Int64.MaxValue);
                    if(CheckpointWire.Text(r)!=c.SessionId||r.ReadInt64()!=c.AuthorityEpoch||CheckpointWire.Text(r)!=c.ActorId)throw new InvalidDataException("composed-identity");
                    int cap=CheckpointWire.Count(r,8192);if(cap<1||CheckpointWire.Text(r,CheckpointWire.MaximumBytes,false)!=expectedJournal)throw new InvalidDataException("composed-journal");
                    int size=CheckpointWire.Count(r,CheckpointWire.MaximumBytes);byte[] state=r.ReadBytes(size);HealthService s;
                    if(state.Length!=size||!HealthService.TryRestoreCheckpoint(c,binding,CheckpointWire.Hash(state),state,teamCount,respawn,authority,world,query,out s,downed,delay,protection))return false;
                    HostHit[] hs;
                    var b=new CombatHealthBridge(s,world,facts,authority,query,c.SessionId,c.AuthorityEpoch,c.ActorId,cap,r,out hs);b.lastTick=CheckpointWire.Number(r,-1,c.Tick);b.offeredSequence=CheckpointWire.Number(r,0,MatchCheckpointRecords.Sequence(s));
                    int n=CheckpointWire.Count(r,cap);for(int i=0;i<n;i++){var hit=hs[CombatRules.Index(r,hs.Length)];var collision=new Collision{Hit=hit,Life=CheckpointWire.Number(r,1,Int64.MaxValue),Connection=CheckpointWire.Number(r,1,Int64.MaxValue)};if(s.Read(hit.TargetId)==null)throw new InvalidDataException("composed-target");b.collisionBindings.Add(hit,collision);}
                    n=CheckpointWire.Count(r,cap);for(int i=0;i<n;i++){string id=CheckpointWire.Text(r);var hit=hs[CombatRules.Index(r,hs.Length)];Collision collision;if(!b.collisionBindings.TryGetValue(hit,out collision))throw new InvalidDataException("composed-alias");b.hits.Add(id,collision);}
                    n=CheckpointWire.Count(r,8192);for(int i=0;i<n;i++){string id=CheckpointWire.Text(r),principal=CheckpointWire.Text(r);var result=CheckpointWire.Result(r);var d=CheckpointWire.Damage(r);b.completed.Add(id,new Receipt(result,d,new HealthTransition[0]));b.completedPrincipals.Add(id,principal);}
                    n=CheckpointWire.Count(r,4096);for(int i=0;i<n;i++)b.deliveries.Add(CheckpointWire.Text(r),new Delivery{Hit=CheckpointWire.Text(r),Tick=CheckpointWire.Number(r,0,c.Tick)});
                    n=CheckpointWire.Count(r,OutboxCapacity);long prev=0;for(int i=0;i<n;i++){var t=CheckpointWire.Transition(r);if(t.SessionId!=c.SessionId||t.AuthorityEpoch!=c.AuthorityEpoch||t.Sequence<=prev||t.Sequence>MatchCheckpointRecords.Sequence(s))throw new InvalidDataException("composed-outbox");b.outbox.Add(t.Sequence,t);prev=t.Sequence;}
                    if(m.Position!=m.Length)throw new InvalidDataException("composed-trailing");healthResult=s;bridgeResult=b;generation=decodedGeneration;return true;
                }
            }catch(InvalidDataException){return false;}catch(IOException){return false;}catch(ArgumentException){return false;}catch(OverflowException){return false;}
        }
        // Trusted facts adapters bind fresh capability references after cold construction.
        // This does not admit or mint a collision from network data.
        public HostHit FindRestoredHit(CommandContext c,string opaque)
        {return health.Serialize(()=>{Collision collision;return c.ActorId==host&&health.Authorize(c).Accepted&&hits.TryGetValue(opaque,out collision)?collision.Hit:null;});}
    }
}



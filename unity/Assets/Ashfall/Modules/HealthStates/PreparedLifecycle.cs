using System;
using Ashfall.Core;
namespace Ashfall.Modules.HealthStates
{
    public sealed partial class HealthService
    {
        // Bounded retained receipts; never evicted or cleared to admit a reconnect.
        private readonly string[] lifecycleKeys=new string[8192];
        private int lifecycleCount;
        private readonly PreparedRespawn[] respawnReservations=new PreparedRespawn[64];
        private bool LifecycleUsed(string key) { for(int i=0;i<lifecycleCount;i++)if(lifecycleKeys[i]==key)return true;return false; }
        public bool PrepareRespawn(CommandContext c,string target,long revision,long life,long connection,double expiry,out PreparedRespawn token)
        {return PrepareConfiguredRespawn(c,target,revision,life,connection,100,100,expiry,out token);}
        public bool PrepareConfiguredRespawn(CommandContext c,string target,long revision,long life,long connection,float health,float shield,double expiry,out PreparedRespawn token)
        {return PrepareConfiguredLifecycle(c,target,revision,life,connection,health,shield,expiry,false,out token);}
        public bool PrepareConfiguredWarmupSpawn(CommandContext c,string target,long revision,long life,long connection,float health,float shield,double expiry,out PreparedRespawn token)
        {return PrepareConfiguredLifecycle(c,target,revision,life,connection,health,shield,expiry,true,out token);}
        private bool PrepareConfiguredLifecycle(CommandContext c,string target,long revision,long life,long connection,float health,float shield,double expiry,bool warmup,out PreparedRespawn token)
        {
            lock(sync) {
                token=null;Player p;double now=world.Now;
                if(!Authorize(c).Accepted||!Finite(health)||health<1||health>100||!Finite(shield)||shield<0||shield>100||!Finite(now)||now<lastTime||!Finite(expiry)||expiry<=now||expiry-now>5||(warmup?world.InMatch:!world.InMatch||!respawnMode)||
                    !players.TryGetValue(target,out p)||!p.Connected||p.Life!=(warmup?LifeState.Alive:LifeState.Dead)||p.Revision!=revision||p.LifeGeneration!=life||p.ConnectionGeneration!=connection||!warmup&&now<p.RespawnAt||
                    revision==Int64.MaxValue||life==Int64.MaxValue||eventSequence==Int64.MaxValue||lifecycleCount==lifecycleKeys.Length||LifecycleUsed(Key(c.ActorId,c.CommandId))||
                    seen.Count>=4096||gainCommands.ContainsKey(Key(c.ActorId,c.CommandId))||seen.ContainsKey(Key(c.ActorId,c.CommandId))||c.Tick<latestTick-256||transitions.Count>=1024)return false;
                int slot=-1;
                for(int i=0;i<respawnReservations.Length;i++) {
                    PreparedRespawn active=respawnReservations[i];
                    if(active!=null&&active.Active(now)) { if(active.TargetId==target)return false; }
                    else if(slot<0)slot=i;
                }
                if(slot<0)return false;
                PreparedPublicationLease publicationLease;if(!TryReservePublication(HealthReservationFamily.Respawn,expiry,out publicationLease))return false;
                try {token=new PreparedRespawn(this,c,p,now,expiry,publicationLease,health,shield);respawnReservations[slot]=token;return true;}
                catch {publicationLease.Abort();throw;}
            }
        }
        public sealed class PreparedRespawn
        {
            readonly long durableGeneration;
            private readonly HealthService owner;private readonly CommandContext context;private readonly Player p;
            private readonly long revision,life,connection,sequence;private readonly double now,expiry;
            private readonly string key;private readonly HealthTransition publication;private bool aborted,committed;
            private readonly PreparedPublicationLease publicationLease;
            public readonly HealthSnapshot Proposed;
            internal string TargetId { get { return p.Id; } }
            internal bool Active(double time) { return publicationLease.CanCommit&&!aborted&&!committed&&time<expiry; }
            internal PreparedRespawn(HealthService service,CommandContext c,object player,double time,double until,PreparedPublicationLease lease,float health,float shield)
            {
                publicationLease=lease;
                owner=service;durableGeneration=service.durableGeneration;context=c;p=(Player)player;revision=p.Revision;life=p.LifeGeneration;connection=p.ConnectionGeneration;sequence=service.eventSequence;now=time;expiry=until;key=Key(c.ActorId,c.CommandId);
                Proposed=new HealthSnapshot(p.Id,p.Team,p.Deaths,revision+1,LifeState.Alive,health,shield,0,0,time+service.protection,0,true,0,life+1,connection);
                publication=new HealthTransition(service.session,service.epoch,sequence+1,"spawn",revision,Proposed);
            }
            public bool Validate()
            {
                lock(owner.sync) {
                    double time=owner.world.Now;
                    return owner.durableGeneration==durableGeneration&&!aborted&&!committed&&publicationLease.Validate()&&owner.Authorize(context).Accepted&&Finite(time)&&time==now&&time>=owner.lastTime&&time<expiry&&(owner.world.InMatch&&owner.respawnMode&&p.Life==LifeState.Dead&&time>=p.RespawnAt||!owner.world.InMatch&&p.Life==LifeState.Alive)&&
                        p.Connected&&p.Revision==revision&&p.LifeGeneration==life&&p.ConnectionGeneration==connection&&owner.eventSequence==sequence&&
                        owner.lifecycleCount<owner.lifecycleKeys.Length&&!owner.LifecycleUsed(key)&&!owner.gainCommands.ContainsKey(key)&&owner.seen.Count<4096&&!owner.seen.ContainsKey(key)&&context.Tick>=owner.latestTick-256&&owner.transitions.Count<1024;
                }
            }
            // Privileged publication after validate-all under Serialize; durable storage must succeed before the Session partner commits.
            // Queue has 1024 preallocated slots; token and snapshot were allocated in Prepare.
            public void CommitValidated()
            {if(owner.durableGeneration!=durableGeneration)throw new InvalidOperationException("stale durable token");owner.AtomicHealth(delegate{
                lock(owner.sync) {
                    if(committed)return true;
                    if(!publicationLease.CanCommit||aborted)throw new InvalidOperationException("Aborted respawn");
                    owner.seen.Add(key,context.Tick);owner.lifecycleKeys[owner.lifecycleCount++]=key;owner.latestTick=Math.Max(owner.latestTick,context.Tick);owner.lastTime=now;
                    p.Life=LifeState.Alive;p.Health=Proposed.Health;p.Shield=Proposed.Shield;p.LifeGeneration=life+1;p.Revision=revision+1;p.DownedUntil=0;p.RespawnAt=0;p.ProtectionUntil=Proposed.ProtectionUntil;Cancel(p);
                    owner.eventSequence++;owner.transitions.Enqueue(publication);committed=true;publicationLease.CompleteValidated();
                }
            return true;});}
            public void Abort() { lock(owner.sync){if(!committed){aborted=true;publicationLease.Abort();}} }
        }
    }
}

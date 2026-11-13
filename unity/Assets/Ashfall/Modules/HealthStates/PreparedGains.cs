using System;
using System.Collections.Generic;
using Ashfall.Core;
namespace Ashfall.Modules.HealthStates
{
    public enum GainOutcome { Failed, Applied, CappedNoOp }
    public sealed class GainReceipt
    {
        public readonly string LogicalId, TargetId;
        public readonly GainOutcome Outcome;
        public readonly float Health, Shield;
        internal GainReceipt(string id,string target,GainOutcome outcome,float h,float s)
        { LogicalId=id;TargetId=target;Outcome=outcome;Health=h;Shield=s; }
    }
    public sealed partial class HealthService
    {
        private readonly Dictionary<string,bool> gainCommands=new Dictionary<string,bool>(4096,StringComparer.Ordinal);
        private readonly Dictionary<string,GainReceipt> gains=new Dictionary<string,GainReceipt>(4096,StringComparer.Ordinal);
        public GainReceipt FindGain(CommandContext c,string logical,string target)
        { lock(sync) { RequireCommittedRead();GainReceipt r;if(!AuthorizeCore(c).Accepted||String.IsNullOrEmpty(logical)||!gains.TryGetValue(Key(c.ActorId,logical),out r))return null;return r.TargetId==target?r:new GainReceipt(logical,target,GainOutcome.Failed,0,0); } }
        public bool PrepareGain(CommandContext c,string logical,string target,long revision,long life,long connection,float health,float shield,out PreparedGain token)
        {
            lock(sync) {
                token=null;Player p;
                if(!Authorize(c).Accepted||c.ActorId!=target||String.IsNullOrEmpty(logical)||logical.Length>128||(LifecycleUsed(Key(c.ActorId,c.CommandId))||gainCommands.ContainsKey(Key(c.ActorId,c.CommandId))||gains.ContainsKey(Key(c.ActorId,logical)))||seen.Count>=4096||transitions.Count>=1024||seen.ContainsKey(Key(c.ActorId,c.CommandId))||c.Tick<latestTick-256||!Finite(world.Now)||world.Now<lastTime||!Finite(health)||!Finite(shield)||health<0||health>100||shield<0||shield>100||!players.TryGetValue(target,out p)||!Living(p)||p.Revision!=revision||p.LifeGeneration!=life||p.ConnectionGeneration!=connection)return false;
                PreparedPublicationLease lease;
                if(!TryReservePublication(HealthReservationFamily.Gain,world.Now+5,out lease))return false;
                try { token=new PreparedGain(this,c,logical,p,health,shield,lease);return true; }
                catch { lease.Abort();throw; }
            }
        }
        public sealed class PreparedGain
        {
            readonly long durableGeneration;
            private readonly PreparedPublicationLease lease;private readonly HealthService owner;private readonly CommandContext context;private readonly Player player;
            private readonly long revision,life,connection;private readonly float health,shield;private readonly string key,commandKey;
            private readonly float preparedCharge;private readonly double preparedReady,preparedTime;private readonly long sequence;private readonly HealthTransition publication;private bool aborted;private GainReceipt receipt;
            internal PreparedGain(HealthService service,CommandContext c,string logical,object target,float h,float s,PreparedPublicationLease reservation)
            { lease=reservation;owner=service;durableGeneration=service.durableGeneration;context=c;player=(Player)target;revision=player.Revision;life=player.LifeGeneration;connection=player.ConnectionGeneration;health=Math.Min(h,100-player.Health);shield=Math.Min(s,100-player.Shield);key=Key(c.ActorId,logical);commandKey=Key(c.ActorId,c.CommandId);sequence=reservation.BaseSequence;preparedCharge=player.Charge;preparedReady=player.ReadyUntil;preparedTime=service.world.Now;receipt=new GainReceipt(logical,player.Id,health>0||shield>0?GainOutcome.Applied:GainOutcome.CappedNoOp,health,shield);if(receipt.Outcome==GainOutcome.Applied){var after=new HealthSnapshot(player.Id,player.Team,player.Deaths,revision+1,player.Life,player.Health+health,player.Shield+shield,player.DownedUntil,player.RespawnAt,player.ProtectionUntil,player.ReadyUntil,player.Connected,player.Charge,life,connection);publication=new HealthTransition(service.session,service.epoch,reservation.NextSequence,"gain",revision,after);} }
            public GainReceipt Proposed { get { return receipt; } }
            private bool committed;
            public bool Validate()
            { lock(owner.sync) { return owner.durableGeneration==durableGeneration&&!aborted&&!committed&&lease.Validate()&&owner.eventSequence==sequence&&player.Charge==preparedCharge&&player.ReadyUntil==preparedReady&&Finite(owner.world.Now)&&owner.world.Now>=preparedTime&&owner.world.Now>=owner.lastTime&&owner.Authorize(context).Accepted&&owner.Living(player)&&player.Revision==revision&&player.LifeGeneration==life&&player.ConnectionGeneration==connection&&!owner.LifecycleUsed(commandKey)&&!owner.gainCommands.ContainsKey(commandKey)&&!owner.gains.ContainsKey(key)&&owner.seen.Count<4096&&owner.transitions.Count<1024&&!owner.seen.ContainsKey(commandKey)&&context.Tick>=owner.latestTick-256; } }
            // Trusted host after Validate-all inside Serialize. Storage failure aborts before the Session partner can publish.
            public GainReceipt CommitValidated()
            {if(owner.durableGeneration!=durableGeneration)throw new InvalidOperationException("stale durable token");return owner.AtomicHealth(delegate{ lock(owner.sync) { if(committed)return receipt;if(!lease.CanCommit||aborted||player.Revision!=revision||player.LifeGeneration!=life||player.ConnectionGeneration!=connection||owner.gains.ContainsKey(key))throw new InvalidOperationException("Invalid prepared gain");owner.seen.Add(commandKey,context.Tick);owner.gains.Add(key,receipt);owner.gainCommands.Add(commandKey,true);owner.latestTick=Math.Max(owner.latestTick,context.Tick);owner.lastTime=Math.Max(owner.lastTime,preparedTime);if(receipt.Outcome==GainOutcome.Applied){player.Health+=health;player.Shield+=shield;player.Revision++;owner.eventSequence++;owner.transitions.Enqueue(publication);}committed=true;lease.CompleteValidated();return receipt; } });}
            public void Abort() { lock(owner.sync){if(!committed){aborted=true;lease.Abort();}} }
        }
    }
}






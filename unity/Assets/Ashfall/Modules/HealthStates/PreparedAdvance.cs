using System;
using System.Collections.Generic;
using Ashfall.Core;
namespace Ashfall.Modules.HealthStates
{
    public sealed partial class HealthService
    {
        public RuleResult PrepareAdvance(CommandContext c,out PreparedAdvance token)
        {
            lock(sync) {
                token=null;RuleResult r=Authorize(c);if(!r.Accepted)return r;
                double now=world.Now;
                if(!Finite(now)||now<0||now<lastTime)return RuleResult.Reject("clock");
                if(c.Tick<latestTick-256)return RuleResult.Reject("stale-tick");
                string key=Key(c.ActorId,c.CommandId);if(LifecycleUsed(key)||seen.ContainsKey(key))return RuleResult.Reject("replay");
                long next=Math.Max(latestTick,c.Tick);int retained=0;var expired=new List<string>();
                foreach(var x in seen)if(x.Value<next-256)expired.Add(x.Key);else retained++;
                if(retained>=4096)return RuleResult.Reject("command-window-full");
                var observations=new List<AdvanceObservation>();var events=new List<HealthTransition>();long seq=eventSequence;
                foreach(Player p in players.Values) {
                    var o=new AdvanceObservation(p);observations.Add(o);
                    if(p.ReadyUntil>0&&now>=p.ReadyUntil) {
                        if(o.Revision==Int64.MaxValue||seq==Int64.MaxValue)return RuleResult.Reject("sequence-capacity");
                        long before=o.Revision++;o.Ready=0;o.Charge=0;o.HaveSample=false;
                        events.Add(new HealthTransition(session,epoch,++seq,"ready-expired",before,o.Snapshot()));
                    }
                    if(p.Life==LifeState.Downed&&now>=p.DownedUntil) {
                        if(o.Revision==Int64.MaxValue||seq==Int64.MaxValue)return RuleResult.Reject("sequence-capacity");
                        long before=o.Revision++;o.Life=LifeState.Dead;o.Downed=0;o.Ready=0;o.Charge=0;o.HaveSample=false;
                        events.Add(new HealthTransition(session,epoch,++seq,"bleedout",before,o.Snapshot()));
                    }
                }
                if(transitions.Count+events.Count>1024)return RuleResult.Reject("publication-capacity");
                PreparedPublicationLease lease;if(!TryReservePublication(HealthReservationFamily.Advance,now+.5,out lease))return RuleResult.Reject("publication-reserved");
                token=new PreparedAdvance(this,c,key,now,next,expired.ToArray(),observations.ToArray(),events.ToArray(),lease);return RuleResult.Accept();
            }
        }
        private sealed class AdvanceObservation
        {
            internal readonly PlayerState State;
            internal readonly Player Target;
            internal long Revision;internal LifeState Life;internal double Downed,Ready;internal float Charge;internal bool HaveSample;
            internal AdvanceObservation(Player p) {Target=p;State=new PlayerState(p);Revision=p.Revision;Life=p.Life;Downed=p.DownedUntil;Ready=p.ReadyUntil;Charge=p.Charge;HaveSample=p.HaveSample;}
            internal HealthSnapshot Snapshot(){Player p=Target;return new HealthSnapshot(p.Id,p.Team,p.Deaths,Revision,Life,p.Health,p.Shield,Downed,p.RespawnAt,p.ProtectionUntil,Ready,p.Connected,Charge,p.LifeGeneration,p.ConnectionGeneration);}
        }
        // Private complete observation includes unrevisioned paddle sampling state.
        private sealed class PlayerState
        {
            readonly string id;readonly int team,deaths;readonly long revision,lifeGeneration,connectionGeneration;
            readonly LifeState life;readonly float hp,shield,charge;readonly double downed,respawn,protection,ready,sample;
            readonly bool connected,have;readonly Float3 left,right;
            internal PlayerState(Player p){id=p.Id;team=p.Team;deaths=p.Deaths;revision=p.Revision;lifeGeneration=p.LifeGeneration;connectionGeneration=p.ConnectionGeneration;life=p.Life;hp=p.Health;shield=p.Shield;charge=p.Charge;downed=p.DownedUntil;respawn=p.RespawnAt;protection=p.ProtectionUntil;ready=p.ReadyUntil;sample=p.SampleAt;connected=p.Connected;have=p.HaveSample;left=p.LastLeft;right=p.LastRight;}
            internal bool Matches(Player p){return id==p.Id&&team==p.Team&&deaths==p.Deaths&&revision==p.Revision&&lifeGeneration==p.LifeGeneration&&connectionGeneration==p.ConnectionGeneration&&life==p.Life&&hp==p.Health&&shield==p.Shield&&charge==p.Charge&&downed==p.DownedUntil&&respawn==p.RespawnAt&&protection==p.ProtectionUntil&&ready==p.ReadyUntil&&sample==p.SampleAt&&connected==p.Connected&&have==p.HaveSample&&left.Equals(p.LastLeft)&&right.Equals(p.LastRight);}
        }
        public sealed class PreparedAdvance
        {
            readonly long durableGeneration;
            readonly HealthService owner;readonly CommandContext context;readonly string key;readonly double time,baseTime;
            readonly long next,baseTick;readonly int seenCount,pending;readonly string[] expired;readonly AdvanceObservation[] observations;readonly HealthTransition[] events;readonly PreparedPublicationLease lease;
            bool validated,closed,committed;
            internal PreparedAdvance(HealthService s,CommandContext c,string k,double now,long tick,string[] prune,object states,HealthTransition[] output,PreparedPublicationLease reservation)
            {owner=s;durableGeneration=s.durableGeneration;context=c;key=k;time=now;next=tick;baseTick=s.latestTick;baseTime=s.lastTime;seenCount=s.seen.Count;pending=s.transitions.Count;expired=prune;observations=(AdvanceObservation[])states;events=output;lease=reservation;}
            public bool Validate(){lock(owner.sync){validated=false;if(owner.durableGeneration!=durableGeneration||closed||!lease.Validate()||owner.world.Now!=time||owner.latestTick!=baseTick||owner.lastTime!=baseTime||owner.seen.Count!=seenCount||owner.LifecycleUsed(key)||owner.seen.ContainsKey(key)||owner.players.Count!=observations.Length||owner.transitions.Count!=pending||!owner.Authorize(context).Accepted)return false;foreach(var o in observations)if(!o.State.Matches(o.Target))return false;validated=true;return true;}}
            // Trusted host lane after Validate. Durable mode crosses a fallible storage barrier before Session publication.
            public void CommitValidated(){if(owner.durableGeneration!=durableGeneration)throw new InvalidOperationException("stale durable token");owner.AtomicHealth(delegate{lock(owner.sync){if(committed)return true;if(!lease.CanCommit||!validated||closed)throw new InvalidOperationException("unvalidated advance");foreach(string k in expired)owner.seen.Remove(k);owner.seen.Add(key,context.Tick);owner.latestTick=next;owner.lastTime=time;foreach(var o in observations){Player p=o.Target;p.Revision=o.Revision;p.Life=o.Life;p.DownedUntil=o.Downed;p.ReadyUntil=o.Ready;p.Charge=o.Charge;p.HaveSample=o.HaveSample;}foreach(var e in events)owner.transitions.Enqueue(e);owner.eventSequence=lease.BaseSequence+events.Length;lease.CompleteValidated();committed=closed=true;}return true;});}
            public void Abort(){lock(owner.sync){if(closed)return;lease.Abort();closed=true;}}
        }
    }
}

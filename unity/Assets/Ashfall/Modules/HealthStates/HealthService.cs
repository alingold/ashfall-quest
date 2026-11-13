using System;
using System.Collections.Generic;
using Ashfall.Core;

namespace Ashfall.Modules.HealthStates
{
    // All operations, including snapshots, are serialized. No callbacks/events are emitted
    // during a mutation. DrainTransitions after EVERY operation, including rejected intents.
    public sealed partial class HealthService : IHealthMutationPort, ITransactionalHealthPort
    {
        private sealed class Player
        {
            public string Id; public int Team, Deaths; public long Revision;
            public long LifeGeneration=1, ConnectionGeneration=1;
            public LifeState Life; public float Health, Shield, Charge;
            public double DownedUntil, RespawnAt, ProtectionUntil, ReadyUntil, SampleAt;
            public bool Connected=true, HaveSample; public Float3 LastLeft, LastRight;
        }
        public const string ApiVersion="HealthStates/2";
        private readonly object sync=new object();
        private readonly Queue<HealthTransition> transitions=new Queue<HealthTransition>(1024);
        private readonly Dictionary<string,DamageReceipt> damage=new Dictionary<string,DamageReceipt>(StringComparer.Ordinal);
        // Logical receipts live for this authority epoch. A new command can retry
        // an old logical event, so command-window pruning cannot retire receipts.
        // Host ingress rate limits bound growth per second; no fixed lifetime
        // count may silently turn off damage during a normal long round. Durable
        // quotas explicitly fault the whole authority instead of discarding receipts.
        private long eventSequence;
        // Trusted host callback only. World/query/gate must be pure and non-reentrant.
        public T Serialize<T>(Func<T> transaction) { lock(sync){return transaction();} }
        public HealthTransition[] DrainTransitions() { lock(sync){RequireCommittedRead();var result=transitions.ToArray();transitions.Clear();return result;} }
        // Fail before draining when the coordinator's bounded outbox cannot own the batch.
        public HealthTransition[] DrainTransitions(int maximum)
        { lock(sync){RequireCommittedRead();if(maximum<0||transitions.Count>maximum)throw new InvalidOperationException("Transition outbox capacity");return DrainTransitions();} }
        public int PendingTransitionCount { get { lock(sync){RequireCommittedRead();return transitions.Count;} } }
        // Transfer only the prefix for which the publisher has capacity; retain the rest.
        public HealthTransition[] DrainTransitionPrefix(int maximum)
        { lock(sync){RequireCommittedRead();if(maximum<0)throw new ArgumentOutOfRangeException("maximum");int n=Math.Min(maximum,transitions.Count);var batch=new HealthTransition[n];for(int i=0;i<n;i++)batch[i]=transitions.Dequeue();return batch;} }
        private void Publish(Player p,long before,string kind)
        { transitions.Enqueue(new HealthTransition(session,epoch,++eventSequence,kind,before,Read(p.Id))); }
        public RuleResult Synchronize(CommandContext c) {return AtomicHealth(delegate{ lock(sync){return Admit(c,false);} });}
        public RuleResult PrepareDamage(CommandContext c)
        {return AtomicHealth(delegate{
            lock(sync) {
                RuleResult r=Admit(c,false);if(!r.Accepted)return r;
                if(LifecycleUsed(Key(c.ActorId,c.CommandId))||seen.ContainsKey(Key(c.ActorId,c.CommandId)))return RuleResult.Reject("replay");
                int count=0;foreach(var pair in seen)if(pair.Value>=Math.Max(latestTick,c.Tick)-256)count++;
                if(count>=4096)return RuleResult.Reject("capacity");
                return RuleResult.Accept();
            }
        });}
        private static string Key(string actor,string id) { return actor.Length+":"+actor+id; }
        public DamageReceipt FindDamage(CommandContext c,string logicalId,string target)
        {
            lock(sync) {
                if(!AuthorizeCore(c).Accepted||String.IsNullOrEmpty(logicalId))return null;
                RequireCommittedRead();DamageReceipt r;
                if(!damage.TryGetValue(Key(c.ActorId,logicalId),out r))return null;
                return r.TargetId==target?r:new DamageReceipt(logicalId,target,RuleResult.Reject("identity-conflict"),false,null);
            }
        }
        private readonly Dictionary<string,Player> players=new Dictionary<string,Player>(StringComparer.Ordinal);
        private readonly Dictionary<string,long> seen=new Dictionary<string,long>(4096,StringComparer.Ordinal);
        private readonly IAuthorityGate gate; private readonly IHealthWorld world; private readonly IInteractionQuery query;
        private readonly string session; private readonly long epoch;
        private readonly int teams; private readonly bool respawnMode;
        private readonly double downedSeconds, respawnDelay, protection;
        private long latestTick=-1; private double lastTime=-1;
        public HealthService(string sessionId,long authorityEpoch,int teamCount,bool respawn,
            IAuthorityGate authority,IHealthWorld environment,IInteractionQuery interactions,
            double downed=60,double respawnSeconds=5,double protectionSeconds=3)
        {
            if(String.IsNullOrEmpty(sessionId)||authorityEpoch<0||teamCount<0||teamCount>8||authority==null||environment==null||interactions==null
                ||!Finite(downed)||downed<10||downed>180||!Finite(respawnSeconds)||respawnSeconds<1||respawnSeconds>30
                ||!Finite(protectionSeconds)||protectionSeconds<0||protectionSeconds>10) throw new ArgumentException("configuration");
            session=sessionId; epoch=authorityEpoch; teams=teamCount; respawnMode=respawn; gate=authority; world=environment; query=interactions;
            downedSeconds=downed; respawnDelay=respawnSeconds; protection=protectionSeconds;
        }
        private static bool Finite(double x) { return !Double.IsNaN(x)&&!Double.IsInfinity(x); }
        public RuleResult Authorize(CommandContext c){if(durableWriting)return RuleResult.Reject("storage callback reentry");if(durable!=null&&durable.Failure!=null&&durableDepth==0)return RuleResult.Reject("health-storage-fault");return AuthorizeCore(c);}
        private RuleResult AuthorizeCore(CommandContext c)
        {
            if(epochIngressClosed)return RuleResult.Reject("health-epoch-closed");
            if(!c.IsAuthority||c.SessionId!=session||c.AuthorityEpoch!=epoch||String.IsNullOrEmpty(c.ActorId)
                ||String.IsNullOrEmpty(c.CommandId)||c.ActorId.Length>128||c.CommandId.Length>128||c.Tick<0) return RuleResult.Reject("context");
            return gate.Validate(c);
        }
        private RuleResult Admit(CommandContext c,bool record=true)
        {
            RuleResult allowed=Authorize(c);if(!allowed.Accepted)return allowed;
            double now=world.Now;
            if(!Finite(now)||now<lastTime||now<0)return RuleResult.Reject("clock");
            // Never evict commands still inside the admissible tick window. Saturation fails closed.
            if(c.Tick<latestTick-256)return RuleResult.Reject("stale-tick");
            long next=Math.Max(latestTick,c.Tick);
            // Pruning and its rejection watermark are one action, including replay/full exits.
            latestTick=next; lastTime=now;
            var expired=new List<string>(); foreach(var pair in seen)if(pair.Value<next-256)expired.Add(pair.Key);
            foreach(string key in expired)seen.Remove(key);
            if(!record) { latestTick=next; lastTime=now; Expire(now); return RuleResult.Accept(); }
            string identity=c.ActorId.Length+":"+c.ActorId+c.CommandId;
            if(LifecycleUsed(identity)||seen.ContainsKey(identity))return RuleResult.Reject("replay");
            if(seen.Count>=4096)return RuleResult.Reject("command-window-full");
            seen.Add(identity,c.Tick); latestTick=next; lastTime=now; Expire(now); return RuleResult.Accept();
        }
        private void Expire(double now)
        {
            foreach(Player p in players.Values)
            {
                if(p.ReadyUntil>0&&now>=p.ReadyUntil){long before=p.Revision;Cancel(p);p.Revision++;Publish(p,before,"ready-expired");}
                if(p.Life==LifeState.Downed&&now>=p.DownedUntil)
                { long before=p.Revision;p.Life=LifeState.Dead; p.DownedUntil=0; p.Revision++; Cancel(p);Publish(p,before,"bleedout"); }
            }
        }
        private static void Cancel(Player p) { p.Charge=0;p.ReadyUntil=0;p.HaveSample=false; }
        private bool TeamValid(int team) { return teams==0?team==-1:team>=0&&team<teams; }
        private bool Living(Player p) { return p.Connected&&p.Life==LifeState.Alive; }
        public RuleResult Register(CommandContext c,string id,int team,bool spectator)
        {return AtomicHealth(delegate{return RegisterCore(c,id,team,spectator,100,100,false);});}
        // Trusted composed Session spawn supplies its validated match rules.
        // Registration is not a client admission or a fabricated Session life.
        public RuleResult RegisterConfiguredSpawn(CommandContext c,string id,int team,bool spectator,float health,float shield)
        {return AtomicHealth(delegate{return RegisterCore(c,id,team,spectator,health,shield,true);});}
        private RuleResult RegisterCore(CommandContext c,string id,int team,bool spectator,float health,float shield,bool spawnProtection)
        {
            lock(sync)
            {
                RuleResult r=Admit(c);if(!r.Accepted)return r;
                if(String.IsNullOrEmpty(id)||id.Length>128||players.ContainsKey(id)||!TeamValid(team)||players.Count>=64||!Finite(health)||health<1||health>100||!Finite(shield)||shield<0||shield>100)return RuleResult.Reject("player");
                players.Add(id,new Player {Id=id,Team=team,Life=spectator?LifeState.Spectator:LifeState.Alive,Health=spectator?0:health,Shield=spectator?0:shield,ProtectionUntil=!spectator&&spawnProtection?world.Now+protection:0,Revision=1});
                Publish(players[id],0,"registered");return RuleResult.Accept();
            }
        }
        public HealthSnapshot Read(string id)
        {
            lock(sync)
            {
                RequireCommittedRead();Player p;if(!players.TryGetValue(id,out p))return null;
                return new HealthSnapshot(p.Id,p.Team,p.Deaths,p.Revision,p.Life,p.Health,p.Shield,p.DownedUntil,p.RespawnAt,p.ProtectionUntil,p.ReadyUntil,p.Connected,p.Charge,p.LifeGeneration,p.ConnectionGeneration);
            }
        }
        public RuleResult ApplyLoss(CommandContext c,string target,long revision,float healthLoss,float shieldLoss,bool revivable)
        { return ApplyDamage(c,c.CommandId,target,revision,healthLoss,shieldLoss,revivable).Result; }
        public DamageReceipt ApplyDamage(CommandContext c,string logicalId,string target,long revision,float healthLoss,float shieldLoss,bool revivable)
        {var terminal=FindDamage(c,logicalId,target);if(terminal!=null)return terminal;return AtomicHealth(delegate{
            lock(sync)
            {
                RuleResult r=Authorize(c);
                if(!r.Accepted)return new DamageReceipt(logicalId,target,r,false,null);
                if(String.IsNullOrEmpty(logicalId)||logicalId.Length>128||String.IsNullOrEmpty(target))return new DamageReceipt(logicalId,target,RuleResult.Reject("identity"),false,null);
                DamageReceipt cached=FindDamage(c,logicalId,target);if(cached!=null)return cached;
                r=Admit(c,false);if(!r.Accepted)return new DamageReceipt(logicalId,target,r,false,null);
                Player p;
                if(!players.TryGetValue(target,out p))return new DamageReceipt(logicalId,target,RuleResult.Reject("target"),false,null);
                // A stale attempt consumes neither command nor logical identity. Re-evaluate SAME hit.
                if(p.Revision!=revision)return new DamageReceipt(logicalId,target,RuleResult.Reject("stale-revision"),true,Read(target));
                r=Admit(c);if(!r.Accepted)return new DamageReceipt(logicalId,target,r,false,null);
                if(!world.InMatch||!Living(p))r=RuleResult.Reject("state");
                else if(!Finite(healthLoss)||!Finite(shieldLoss)||healthLoss<0||shieldLoss<0||(healthLoss==0&&shieldLoss==0)
                    ||shieldLoss>p.Shield||world.Now<p.ProtectionUntil)r=RuleResult.Reject("loss-or-protection");
                else {
                    long before=p.Revision;
                    p.Health=Math.Max(0,p.Health-healthLoss);p.Shield-=shieldLoss;p.Revision++;
                    if(p.Health==0) {
                        p.Shield=0;p.Deaths++;Cancel(p);
                        p.Life=revivable&&!respawnMode&&teams>0?LifeState.Downed:LifeState.Dead;
                        p.DownedUntil=p.Life==LifeState.Downed?world.Now+downedSeconds:0;
                        p.RespawnAt=respawnMode?world.Now+respawnDelay:0;
                    }
                    Publish(p,before,"damage");
                }
                var receipt=new DamageReceipt(logicalId,target,r,false,Read(target));
                damage.Add(Key(c.ActorId,logicalId),receipt);return receipt;
            }
        });}
        public RuleResult Spawn(CommandContext c,string target,long revision,float health,float shield)
        {return AtomicHealth(delegate{
            lock(sync)
            {
                RuleResult r=Admit(c);if(!r.Accepted)return r;Player p;
                if(!players.TryGetValue(target,out p)||!p.Connected||p.Life==LifeState.Spectator||p.Revision!=revision
                    ||!Finite(health)||!Finite(shield)||health<1||health>100||shield<0||shield>100)return RuleResult.Reject("spawn");
                if(world.InMatch&&(!respawnMode||p.Life!=LifeState.Dead||world.Now<p.RespawnAt))return RuleResult.Reject("respawn-not-due");
                p.LifeGeneration++;p.Life=LifeState.Alive;p.Health=health;p.Shield=shield;p.DownedUntil=0;p.RespawnAt=0;p.ProtectionUntil=world.Now+protection;p.Revision++;Cancel(p);Publish(p,revision,"spawn");
                return RuleResult.Accept();
            }
        });}
        public RuleResult SetTeam(CommandContext c,string target,long revision,int team)
        {return AtomicHealth(delegate{
            lock(sync)
            {
                RuleResult r=Admit(c);if(!r.Accepted)return r;Player p;
                if(!world.TeamChangesAllowed||world.InMatch||!TeamValid(team)||!players.TryGetValue(target,out p)||p.Revision!=revision)return RuleResult.Reject("team");
                p.Team=team;p.Revision++;Cancel(p);Publish(p,revision,"team");return RuleResult.Accept();
            }
        });}
        public RuleResult SetConnected(CommandContext c,string target,long revision,bool connected)
        {return AtomicHealth(delegate{
            lock(sync)
            {
                RuleResult r=Admit(c);if(!r.Accepted)return r;Player p;
                if(!players.TryGetValue(target,out p)||p.Revision!=revision)return RuleResult.Reject("state");
                if(p.Connected!=connected)p.ConnectionGeneration++;
                p.Connected=connected;p.Revision++;Cancel(p);Publish(p,revision,"connection");return RuleResult.Accept();
            }
        });}
        // Called on authority pose arrival. Local hands must be transformed by authoritative
        // root inverse, so locomotion cannot charge. Facing comes from validated electrode normals.
        public RuleResult SamplePaddles(CommandContext c)
        {return AtomicHealth(delegate{
            lock(sync)
            {
                RuleResult r=Admit(c);if(!r.Accepted)return r;Player p;RescuePose pose;
                if(!players.TryGetValue(c.ActorId,out p))return RuleResult.Reject("actor");
                if(!Living(p)||!world.InMatch||!world.TryGetPose(c.ActorId,out pose)||!Usable(pose))
                { Cancel(p);return RuleResult.Reject("interaction-cancelled"); }
                if(p.ReadyUntil>world.Now)return RuleResult.Accept();
                double dt=p.HaveSample?Math.Min(.10,world.Now-p.SampleAt):0;
                if(dt>0)
                {
                    double speed=pose.Desktop&&pose.Facing?.5:Math.Max(Distance(pose.LocalLeft,p.LastLeft),Distance(pose.LocalRight,p.LastRight))/dt;
                    if(pose.Facing&&speed<=8)p.Charge=Math.Min(1,p.Charge+(float)(dt/(10-4.5*Math.Min(1,speed/.5))));
                    else if(!pose.Facing)p.Charge=Math.Max(0,p.Charge-(float)(dt/2.5));
                    if(p.Charge>=.999999f){p.Charge=0;p.ReadyUntil=world.Now+3.5;}
                }
                p.HaveSample=true;p.SampleAt=world.Now;p.LastLeft=pose.LocalLeft;p.LastRight=pose.LocalRight;
                return RuleResult.Accept();
            }
        });}
        private static bool Usable(RescuePose p) { return p.Tracked&&!p.Paused&&p.PaddlesActive&&p.Left.IsFinite&&p.Right.IsFinite&&p.LocalLeft.IsFinite&&p.LocalRight.IsFinite; }
        private static double Distance(Float3 a,Float3 b) { double x=(double)a.X-b.X,y=(double)a.Y-b.Y,z=(double)a.Z-b.Z;return Math.Sqrt(x*x+y*y+z*z); }
        public RuleResult CancelRevive(CommandContext c)
        {return AtomicHealth(delegate{
            lock(sync){RuleResult r=Admit(c);if(!r.Accepted)return r;Player p;if(!players.TryGetValue(c.ActorId,out p))return RuleResult.Reject("actor");Cancel(p);return RuleResult.Accept();}
        });}
        public RuleResult Revive(CommandContext c,string target,long revision)
        {return AtomicHealth(delegate{
            lock(sync)
            {
                RuleResult r=Admit(c);if(!r.Accepted)return r;Player actor,p;RescuePose a,t;
                if(!world.InMatch||!players.TryGetValue(c.ActorId,out actor)||!players.TryGetValue(target,out p)||!Living(actor)
                    ||!p.Connected||p.Life!=LifeState.Downed||p.Revision!=revision||teams==0||actor.Team!=p.Team||actor==p)return RuleResult.Reject("revive-state");
                if(!world.TryGetPose(c.ActorId,out a)||!Usable(a)){Cancel(actor);return RuleResult.Reject("interaction-cancelled");}
                if(actor.ReadyUntil<=world.Now||!world.TryGetPose(target,out t)||!t.Chest.IsFinite)return RuleResult.Reject("not-ready-or-pose");
                if(Distance(a.Left,t.Chest)>=1||Distance(a.Right,t.Chest)>=1
                    ||!query.HasLineOfSight(c.ActorId,target,a.Left,t.Chest,1)||!query.HasLineOfSight(c.ActorId,target,a.Right,t.Chest,1))return RuleResult.Reject("contacts-or-occlusion");
                p.LifeGeneration++;p.Life=LifeState.Alive;p.Health=50;p.Shield=0;p.DownedUntil=0;p.RespawnAt=0;p.ProtectionUntil=world.Now+protection;p.Revision++;
                Cancel(p);Publish(p,revision,"revive");return RuleResult.Accept(); // Actor's original ready deadline remains unchanged.
            }
        });}
        public RuleResult Advance(CommandContext c) {return AtomicHealth(delegate{ lock(sync){return Admit(c);} });}
        public bool TeamHasLivingMember(int team)
        { lock(sync){RequireCommittedRead();foreach(Player p in players.Values)if(p.Team==team&&Living(p))return true;return false;} }
    }
}



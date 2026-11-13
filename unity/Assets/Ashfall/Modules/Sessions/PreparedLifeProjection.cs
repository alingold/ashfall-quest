using System;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
namespace Ashfall.Modules.Sessions
{
    public sealed class LifeProjection
    {
        public readonly string CanonicalActorId;
        public readonly long Epoch, HealthRevision, LifeGeneration, ConnectionGeneration;
        public readonly LifeState Life;
        public readonly int Team;
        internal LifeProjection(string id,long epoch,long revision,long life,long connection,LifeState state,int team)
        {CanonicalActorId=id;Epoch=epoch;HealthRevision=revision;LifeGeneration=life;ConnectionGeneration=connection;Life=state;Team=team;}
        internal LifeProjection(long epoch,HealthSnapshot h)
        { Epoch=epoch;CanonicalActorId=h.Id;HealthRevision=h.Revision;LifeGeneration=h.LifeGeneration;ConnectionGeneration=h.ConnectionGeneration;Life=h.Life;Team=h.Team; }
    }
    public sealed partial class Session
    {
        private readonly LifeProjection[] lifeProjections=new LifeProjection[256];
        private int projectionCount;
        public LifeProjection ProjectedLife(string canonical)
        { for(int i=0;i<projectionCount;i++)if(lifeProjections[i].CanonicalActorId==canonical&&lifeProjections[i].Epoch==Epoch)return lifeProjections[i];return null; }
        public bool PrepareLifeProjection(CommandContext c,string connection,string canonical,HealthSnapshot health,double expiry,SpawnSite spawn,out PreparedLifeProjection token)
        {return PrepareLifeProjectionCore(c,connection,canonical,health,expiry,spawn,false,out token);}
        public bool PrepareWarmupReplacement(CommandContext c,string connection,string canonical,HealthSnapshot health,double expiry,SpawnSite spawn,out PreparedLifeProjection token)
        {return PrepareLifeProjectionCore(c,connection,canonical,health,expiry,spawn,true,out token);}
        private bool PrepareLifeProjectionCore(CommandContext c,string connection,string canonical,HealthSnapshot health,double expiry,SpawnSite spawn,bool warmup,out PreparedLifeProjection token)
        {
            token=null;Member p;double now=clock.Now;
            if(!c.IsAuthority||c.SessionId!=Id||c.AuthorityEpoch!=Epoch||c.Tick<0||String.IsNullOrEmpty(c.CommandId)||!gate.Validate(c).Accepted||!management.CanManage(c)||
                Double.IsNaN(now)||Double.IsInfinity(now)||now<lastTime||Double.IsNaN(expiry)||Double.IsInfinity(expiry)||expiry<=now||expiry-now>5||
                (warmup?Stage!=SessionStage.Lobby:Stage!=SessionStage.Match&&Stage!=SessionStage.Results)||health==null||!health.Connected||health.Id!=canonical||!players.TryGetValue(connection,out p)||p.CanonicalId!=canonical||
                p.Team!=health.Team||p.Waiting||p.Spectator!=(health.Life==LifeState.Spectator)||!p.Loaded||!worldReady)return false;
            int index=-1;for(int i=0;i<projectionCount;i++)if(lifeProjections[i].CanonicalActorId==canonical){index=i;break;}
            LifeProjection prior=index<0?null:lifeProjections[index];
            if(index<0&&projectionCount==lifeProjections.Length)return false;
            if(prior!=null&&prior.Epoch==Epoch&&(health.Revision<prior.HealthRevision||health.LifeGeneration<prior.LifeGeneration||health.ConnectionGeneration<prior.ConnectionGeneration||
                (health.Revision==prior.HealthRevision&&(health.Life!=prior.Life||health.Team!=prior.Team))))return false;
            if(warmup){
                if(spawn==null||p.Life!=SessionLife.Alive||!p.HasPosition||p.Spectator||p.Revision==Int32.MaxValue||health.Life!=LifeState.Alive||health.Deaths!=p.Deaths||!ApprovedSpawn(p,spawn,true))return false;
            }else if(spawn!=null) {
                if(Stage!=SessionStage.Match||Rules.Mode!="respawn"||p.Life!=SessionLife.Eliminated||now<p.RespawnAt||p.Revision==Int32.MaxValue||health.Life!=LifeState.Alive||!ApprovedSpawn(p,spawn))return false;
            } else if(health.Life==LifeState.Alive&&p.Life==SessionLife.Eliminated&&(prior==null||prior.Epoch!=Epoch||prior.Life!=LifeState.Downed))return false;
            token=new PreparedLifeProjection(this,c,p,health,index,prior,expiry,spawn);checkpointReservations.Add(token);return true;
        }
        private bool ApprovedSpawn(Member p,SpawnSite site,bool warmup=false)
        {
            if(site==null||String.IsNullOrEmpty(site.Id)||!site.Feet.IsFinite||(site.Team!=-1&&site.Team!=p.Team)||site.CapsulePlatform)return false;
            bool indoor=Rules.Indoor||Rules.MapId=="archive:battlefield-paintball-gods-pitdevil"||Rules.MapId=="archive:skb-paintball-gods-skblong";
            if(indoor&&!site.Interior)return false;
            SpawnSite[] sites=world.Sites(Rules.MapId,warmup);int matches=0;
            if(sites!=null)foreach(SpawnSite s in sites)if(s!=null&&s.Id==site.Id){if(s.Team!=site.Team||s.Interior!=site.Interior||s.CapsulePlatform!=site.CapsulePlatform||!Equal(s.Feet,site.Feet))return false;matches++;}
            return matches==1&&world.IsSafe(Rules.MapId,p.Id,site.Feet);
        }
        private static bool Equal(Float3 a,Float3 b) { return a.X==b.X&&a.Y==b.Y&&a.Z==b.Z; }
        public sealed class PreparedLifeProjection
        {
            private readonly Session owner;private readonly Member p;private readonly string connection;
            private readonly CommandContext context;private readonly int count;
            private readonly long epoch;private readonly int revision,deaths,index;private readonly SessionLife life;
            private readonly SessionStage stage;private readonly SessionRules rules;private readonly LifeProjection prior,next;
            private readonly HealthSnapshot health;private readonly double expiry,preparedAt,respawnAt;private readonly SpawnSite spawn;
            private double validatedAt;private bool validated;
            private readonly object connectionIdentity;
            private readonly bool hasPosition;private readonly Float3 position;private bool aborted,committed;
            internal PreparedLifeProjection(Session s,CommandContext c,Member member,HealthSnapshot h,int slot,LifeProjection previous,double until,SpawnSite site)
            { owner=s;context=c;count=s.projectionCount;p=member;connection=p.Id;connectionIdentity=p.ConnectionIdentity;epoch=s.Epoch;revision=p.Revision;deaths=p.Deaths;life=p.Life;stage=s.Stage;rules=s.Rules;prior=previous;index=slot;health=h;next=new LifeProjection(epoch,h);expiry=until;preparedAt=s.clock.Now;spawn=site;respawnAt=p.RespawnAt;hasPosition=p.HasPosition;position=p.Position; }
            public bool Validate()
            {
                validated=false;Member current;double now=owner.clock.Now;
                bool ok=!aborted&&!committed&&owner.gate.Validate(context).Accepted&&owner.management.CanManage(context)&&owner.projectionCount==count&&owner.Epoch==epoch&&owner.Stage==stage&&ReferenceEquals(owner.Rules,rules)&&owner.players.TryGetValue(connection,out current)&&ReferenceEquals(current,p)&&p.Id==connection&&p.CanonicalId==health.Id&&p.Team==health.Team&&
                    ReferenceEquals(p.ConnectionIdentity,connectionIdentity)&&p.Loaded&&!p.Waiting&&p.Spectator==(health.Life==LifeState.Spectator)&&owner.worldReady&&p.Revision==revision&&p.Deaths==deaths&&p.Life==life&&p.RespawnAt==respawnAt&&p.HasPosition==hasPosition&&Equal(position,p.Position)&&
                    !Double.IsNaN(now)&&!Double.IsInfinity(now)&&now>=preparedAt&&now>=owner.lastTime&&now<expiry&&(index<0?owner.projectionCount<owner.lifeProjections.Length:ReferenceEquals(owner.lifeProjections[index],prior))&&
                    (spawn==null||owner.ApprovedSpawn(p,spawn,stage==SessionStage.Lobby));
                if(ok){validatedAt=now;validated=true;}return ok;
            }
            public void CommitValidated()
            {
                if(committed)return;if(!validated)throw new InvalidOperationException("Projection not validated");if(aborted)throw new InvalidOperationException("Aborted projection");
                owner.lastTime=Math.Max(owner.lastTime,validatedAt);
                owner.lifeProjections[index<0?owner.projectionCount++:index]=next;
                p.Life=health.Life==LifeState.Alive?SessionLife.Alive:health.Life==LifeState.Spectator?SessionLife.Spectator:SessionLife.Eliminated;
                p.Deaths=health.Deaths;p.RespawnAt=health.RespawnAt;
                if(spawn!=null){p.Position=spawn.Feet;p.HasPosition=true;p.Revision++;p.RespawnAt=0;}
                committed=true;owner.checkpointReservations.Remove(this);
            }
            public void Abort() { if(!committed){aborted=true;owner.checkpointReservations.Remove(this);} }
        }
    }
}

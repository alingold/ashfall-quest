using System;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Sessions;
namespace Ashfall.Integration.LifecycleTransactions
{
    // Provisioned by coordinator, never client-supplied. Validate authenticates current
    // lease/principal and binds canonical identity; no inferred connection->actor alias.
    public interface IAuthenticatedLifecycleIdentity
    { bool Validate(CommandContext host,string connection,string canonical,long healthConnectionGeneration); }
    public sealed class RootPlacement
    {
        public readonly CommandContext Context;
        public readonly string Connection,Canonical,MapId,MapHash;
        public readonly int Team,SpawnRevision;
        public readonly long HealthRevision,LifeGeneration,ConnectionGeneration;
        public readonly double Expiry;
        public readonly SpawnSite Site;
        public RootPlacement(CommandContext c,string connection,HealthSnapshot h,SessionRules rules,int spawnRevision,SpawnSite site,double expiry)
        { Context=c;Connection=connection;Canonical=h.Id;Team=h.Team;HealthRevision=h.Revision;LifeGeneration=h.LifeGeneration;ConnectionGeneration=h.ConnectionGeneration;MapId=rules.MapId;MapHash=rules.MapHash;SpawnRevision=spawnRevision;Site=site;Expiry=expiry; }
    }
    // Must reserve exact approved destination + authoritative root revision, then
    // validate pure/stably and commit without allocation/failure/tracked child writes.
    public interface IPreparedRootPlacement { bool Validate();void CommitValidated();void Abort(); }
    public interface ILifecycleRootPort
    { bool Prepare(RootPlacement exact,out IPreparedRootPlacement prepared); }
    public sealed class Coordinator
    {
        private readonly HealthService health;private readonly Session session;
        private readonly IAuthenticatedLifecycleIdentity identity;private readonly ILifecycleRootPort roots;
        private bool busy;
        public Coordinator(HealthService h,Session s,IAuthenticatedLifecycleIdentity auth,ILifecycleRootPort root)
        { if(h==null||s==null||auth==null||root==null)throw new ArgumentNullException();health=h;session=s;identity=auth;roots=root; }
        // Every writer of Session/root and all external ports must share this lane.
        public T Serialize<T>(Func<T> operation) { return health.Serialize(operation); }
        public bool PrepareRespawn(CommandContext c,string connection,SpawnSite approved,double expiry,out PreparedLifecycle token)
        {
            PreparedLifecycle result=null;
            bool ok=health.Serialize(delegate {
                if(busy)return false;busy=true;
                try {
                    MemberSnapshot m=session.Get(connection);HealthSnapshot h=m==null?null:health.Read(m.CanonicalActorId);
                    if(h==null||m.Team!=h.Team||!identity.Validate(c,connection,h.Id,h.ConnectionGeneration)||!ProjectionMatches(session.ProjectedLife(h.Id),h,session.Epoch))return false;
                    HealthService.PreparedRespawn hp;
                    if(!health.PrepareRespawn(c,h.Id,h.Revision,h.LifeGeneration,h.ConnectionGeneration,expiry,out hp))return false;
                    Session.PreparedLifeProjection sp;
                    if(approved==null||!session.PrepareLifeProjection(c,connection,h.Id,hp.Proposed,expiry,approved,out sp)){hp.Abort();return false;}
                    IPreparedRootPlacement rp;
                    var exact=new RootPlacement(c,connection,hp.Proposed,session.Rules,m.SpawnRevision+1,approved,expiry);
                    if(!roots.Prepare(exact,out rp)||rp==null){hp.Abort();sp.Abort();return false;}
                    result=new PreparedLifecycle(this,c,connection,h,hp,sp,rp);return true;
                } finally { busy=false; }
            });token=result;return ok;
        }
        private static bool ProjectionMatches(LifeProjection p,HealthSnapshot h,long epoch)
        { return p!=null&&p.Epoch==epoch&&p.CanonicalActorId==h.Id&&p.HealthRevision==h.Revision&&p.LifeGeneration==h.LifeGeneration&&p.ConnectionGeneration==h.ConnectionGeneration&&p.Life==h.Life&&p.Team==h.Team; }
        // Outbox payload is only a wakeup. Project the exact current canonical state;
        // old payloads cannot roll back revive/respawn or reconnect generations.
        public bool ProjectCurrent(CommandContext c,string connection,double expiry)
        {
            return health.Serialize(delegate {
                if(busy)return false;busy=true;
                try {
                    MemberSnapshot m=session.Get(connection);HealthSnapshot h=m==null?null:health.Read(m.CanonicalActorId);
                    if(h==null||!health.Authorize(c).Accepted||!identity.Validate(c,connection,h.Id,h.ConnectionGeneration))return false;
                    Session.PreparedLifeProjection sp;
                    if(!session.PrepareLifeProjection(c,connection,h.Id,h,expiry,null,out sp))return false;
                    if(!sp.Validate()){sp.Abort();return false;}sp.CommitValidated();return true;
                } finally { busy=false; }
            });
        }
        public sealed class PreparedLifecycle
        {
            private readonly Coordinator owner;private readonly CommandContext context;private readonly string connection;
            private readonly HealthSnapshot before;private readonly HealthService.PreparedRespawn health;
            private readonly object connectionIdentity;
            private readonly Session.PreparedLifeProjection session;private readonly IPreparedRootPlacement root;
            private bool aborted,committed;
            internal PreparedLifecycle(Coordinator o,CommandContext c,string actor,HealthSnapshot h,HealthService.PreparedRespawn hp,Session.PreparedLifeProjection sp,IPreparedRootPlacement rp)
            { owner=o;context=c;connection=actor;before=h;health=hp;session=sp;root=rp;connectionIdentity=o.session.GetConnectionIdentity(actor); }
            private bool Valid() { return !aborted&&!committed&&connectionIdentity!=null&&ReferenceEquals(connectionIdentity,owner.session.GetConnectionIdentity(connection))&&ProjectionMatches(owner.session.ProjectedLife(before.Id),before,owner.session.Epoch)&&owner.identity.Validate(context,connection,before.Id,before.ConnectionGeneration)&&health.Validate()&&session.Validate()&&root.Validate(); }
            public bool Validate() { return owner.Serialize(delegate {return !owner.busy&&Valid();}); }
            // Revalidates ALL immediately before publication. External root commit is
            // a trusted no-fail port contract; a throwing implementation violates it.
            public bool CommitValidated()
            {
                return owner.Serialize(delegate {
                    if(committed)return true;if(owner.busy||aborted)return false;owner.busy=true;
                    try { if(!Valid())return false;health.CommitValidated();session.CommitValidated();root.CommitValidated();committed=true;return true; }
                    finally { owner.busy=false; }
                });
            }
            public void Abort() { owner.Serialize(delegate {if(!committed){aborted=true;health.Abort();session.Abort();root.Abort();}return true;}); }
        }
    }
}

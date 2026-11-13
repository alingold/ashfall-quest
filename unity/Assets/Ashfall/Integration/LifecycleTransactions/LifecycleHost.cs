using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Sessions;
namespace Ashfall.Integration.LifecycleTransactions
{
    // Explicit host entry points; no Update/RPC/static activation. The caller shares
    // Health.Serialize with input, Director, root owner and all producer publication.
    public sealed class LifecycleHost : IAuthenticatedLifecycleIdentity
    {
        readonly HealthService health;readonly Session session;readonly IAuthorityGate gate;
        readonly string host;readonly SourceSpawnWorld spawns;readonly Coordinator coordinator;
        readonly Dictionary<string,SessionLifecycleIdentity> bindings=new Dictionary<string,SessionLifecycleIdentity>(StringComparer.Ordinal);
        readonly Dictionary<string,string> connections=new Dictionary<string,string>(StringComparer.Ordinal);
        public LifecycleHost(HealthService health,Session session,IAuthorityGate gate,string host,SourceSpawnWorld spawns,ILifecycleRootPort root)
        {
            if(health==null||session==null||gate==null||String.IsNullOrEmpty(host)||spawns==null||root==null)throw new ArgumentException("host lifecycle dependencies");
            this.health=health;this.session=session;this.gate=gate;this.host=host;this.spawns=spawns;
            coordinator=new Coordinator(health,session,this,new SourceRootPlacement(spawns,root));
        }
        bool Host(CommandContext c){return c.IsAuthority&&c.ActorId==host&&c.SessionId==session.Id&&c.AuthorityEpoch==session.Epoch&&c.Tick>=0&&!String.IsNullOrEmpty(c.CommandId)&&health.Authorize(c).Accepted&&gate.Validate(c).Accepted;}
        // Called only AFTER actual trusted Session and Health admission is complete.
        // This consumes Session's local token, never a client generation number.
        public RuleResult BindCurrent(CommandContext c,string connection)
        {
            return health.Serialize(delegate {
                if(!Host(c))return RuleResult.Reject("host-required");
                var m=session.Get(connection);var h=m==null?null:health.Read(m.CanonicalActorId);
                if(h==null||!h.Connected||m.Team!=h.Team||session.GetConnectionIdentity(connection)==null)return RuleResult.Reject("admission-incomplete");
                if((!bindings.ContainsKey(connection)&&bindings.Count==256)||(!connections.ContainsKey(h.Id)&&connections.Count==256))return RuleResult.Reject("binding-capacity");
                bindings[connection]=new SessionLifecycleIdentity(session,health,gate,host,connection);connections[h.Id]=connection;return RuleResult.Accept();
            });
        }
        bool IAuthenticatedLifecycleIdentity.Validate(CommandContext c,string connection,string canonical,long generation)
        {SessionLifecycleIdentity binding;string current;return connections.TryGetValue(canonical,out current)&&current==connection&&bindings.TryGetValue(connection,out binding)&&binding.Validate(c,connection,canonical,generation);}
        public MatchDirectorProjection CreateMatchProjection(Func<CommandContext> context,Func<double> expiry)
        {return new MatchDirectorProjection(coordinator,session,health,context,expiry);}
        public RuleResult Project(CommandContext c,HealthTransition wake,double expiry)
        {
            return health.Serialize(delegate {
                string connection;
                if(!Host(c)||wake==null||wake.After==null||wake.SessionId!=session.Id||wake.AuthorityEpoch!=session.Epoch||!connections.TryGetValue(wake.After.Id,out connection))return RuleResult.Reject("projection-binding");
                // Disconnected/removed members require retained host disposition; do not
                // ACK their event as successfully projected or resurrect membership.
                return coordinator.ProjectCurrent(c,connection,expiry)?RuleResult.Accept():RuleResult.Reject("projection-not-ready");
            });
        }
        public bool PrepareRespawn(CommandContext c,string connection,SpawnSite site,double expiry,out Coordinator.PreparedLifecycle prepared)
        {
            Coordinator.PreparedLifecycle result=null;
            bool ok=health.Serialize(delegate {
                SourceSpawnEvidence evidence;
                var m=session.Get(connection);
                if(!Host(c)||m==null||!spawns.TryEvidence(session.Rules.MapId,m.CanonicalActorId,site,out evidence)||evidence.Content.PayloadSha256!=session.Rules.MapHash)return false;
                return coordinator.PrepareRespawn(c,connection,evidence.Site,expiry,out result);
            });prepared=result;return ok;
        }
    }
}

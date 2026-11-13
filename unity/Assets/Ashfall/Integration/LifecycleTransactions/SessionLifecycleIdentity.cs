using System;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Sessions;
namespace Ashfall.Integration.LifecycleTransactions
{
    // Consumes actual host Session admission identity. Does not authenticate a transport
    // principal or mint a network lease. Construct after trusted admission on the host lane.
    // A replacement connection requires a replacement binding; never follow a new identity.
    public sealed class SessionLifecycleIdentity : IAuthenticatedLifecycleIdentity
    {
        readonly Session session;readonly HealthService health;readonly IAuthorityGate gate;
        readonly string host,connection,canonical;readonly long epoch,generation;
        readonly object identity;
        public SessionLifecycleIdentity(Session session,HealthService health,IAuthorityGate gate,string host,string connection)
        {
            if(session==null||health==null||gate==null||String.IsNullOrEmpty(host))throw new ArgumentException("host binding");
            var m=session.Get(connection);var h=m==null?null:health.Read(m.CanonicalActorId);
            if(h==null||!h.Connected||m.Team!=h.Team||session.GetConnectionIdentity(connection)==null)throw new ArgumentException("admitted health connection required");
            this.session=session;this.health=health;this.gate=gate;this.host=host;this.connection=connection;
            canonical=h.Id;epoch=session.Epoch;generation=h.ConnectionGeneration;identity=session.GetConnectionIdentity(connection);
        }
        public bool Validate(CommandContext c,string connection,string canonical,long healthConnectionGeneration)
        {
            if(c.ActorId!=host||!c.IsAuthority||c.Tick<0||String.IsNullOrEmpty(c.CommandId)||c.SessionId!=session.Id||c.AuthorityEpoch!=epoch||session.Epoch!=epoch||
                this.connection!=connection||this.canonical!=canonical||generation!=healthConnectionGeneration||!ReferenceEquals(identity,session.GetConnectionIdentity(connection))||!gate.Validate(c).Accepted)return false;
            var m=session.Get(connection);var h=health.Read(canonical);
            return m!=null&&h!=null&&m.CanonicalActorId==canonical&&m.Team==h.Team&&h.Connected&&h.ConnectionGeneration==generation;
        }
    }
}

using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Sessions;
using Ashfall.Integration.Sessions;
using Ashfall.Integration.SessionReplication;
namespace Ashfall.NetworkRuntimeFixtures
{
    // SYNTHETIC authentication/geometry only. Not production provisioning.
    public sealed class SyntheticSessionFixture : ISessionClock,IReconnectTokens,ISpawnWorld,ISessionManagement,IAuthorityGate,ISessionPrincipal,ISessionPlacement,IAuthenticatedPeerPort
    {
        public readonly Dictionary<string,AuthenticatedPeer> Records=new Dictionary<string,AuthenticatedPeer>();
        private int tokens;
        public double Time;
        public double Now { get { return Time; } }
        public bool TryResolve(string c,out AuthenticatedPeer p) { return Records.TryGetValue(c,out p); }
        public bool Resolve(string c,out string actor) { AuthenticatedPeer p; bool ok=TryResolve(c,out p); actor=ok?p.Actor:null; return ok; }
        public string Issue() { return "synthetic-only-"+(++tokens); }
        public RuleResult Validate(CommandContext c) { return c.IsAuthority && c.ActorId=="host"?RuleResult.Accept():RuleResult.Reject("authority"); }
        public bool CanManage(CommandContext c) { return c.ActorId=="host"; }
        public SpawnSite[] Sites(string map,bool warmup) { return new[]{new SpawnSite("synthetic-floor",new Float3(0,0,0),-1,true,false)}; }
        public bool IsSafe(string map,string actor,Float3 p) { return p.IsFinite && p.Y==0; }
        public void Publish(string actor,long epoch,MemberSnapshot member) { }
    }
}

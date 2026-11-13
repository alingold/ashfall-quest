using System;
using System.Collections.Generic;
using Ashfall.Core;

using Ashfall.Modules.Modes;
namespace Ashfall.Integration.MatchRounds
{
    // Bounded fail-closed journal: never evicts accepted IDs and thereby re-admits old commands.
    // Separate per service; external authority gate authenticates sender and server tick window.
    internal sealed partial class CommandLedger
    {
        private readonly string session;
        private readonly long epoch;
        private readonly IAuthorityGate authority;
        private readonly HashSet<string> seen=new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string,long> ticks=new Dictionary<string,long>(StringComparer.Ordinal);
        private readonly int capacity;
        public CommandLedger(string session,long epoch,IAuthorityGate authority,int capacity)
        {
            if(String.IsNullOrEmpty(session)||epoch<0||authority==null||capacity<1) throw new ArgumentException("authority/session/epoch/capacity");
            this.session=session; this.epoch=epoch; this.authority=authority; this.capacity=capacity;
        }
        public RuleResult Check(CommandContext c)
        {
            if(!c.IsAuthority||c.SessionId!=session||c.AuthorityEpoch!=epoch) return RuleResult.Reject("authority_or_epoch");
            if(String.IsNullOrEmpty(c.ActorId)||String.IsNullOrEmpty(c.CommandId)||c.ActorId.Length>128||c.CommandId.Length>128||c.Tick<0) return RuleResult.Reject("command");
            RuleResult gate=authority.Validate(c); if(!gate.Accepted) return gate;
            string key=Key(c); long last;
            if(seen.Contains(key)) return RuleResult.Reject("replay");
            if(ticks.TryGetValue(c.ActorId,out last)&&c.Tick<last) return RuleResult.Reject("stale_tick");
            if(seen.Count>=capacity) return RuleResult.Reject("journal_full");
            return RuleResult.Accept();
        }
        public bool Contains(CommandContext c) { return seen.Contains(Key(c)); }
        private static string Key(CommandContext c) { return c.ActorId.Length+":"+c.ActorId+c.CommandId; }
        public void Commit(CommandContext c) { seen.Add(Key(c)); ticks[c.ActorId]=c.Tick; }
    }
}

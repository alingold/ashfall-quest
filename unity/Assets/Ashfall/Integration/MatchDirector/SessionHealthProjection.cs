using System;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Sessions;
namespace Ashfall.Integration.MatchDirector
{
    // Candidate adapter against pinned PreparedLifeProjection. Host owns serialization.
    // Disconnected snapshots need the separate lifecycle transaction; never fake success.
    public sealed partial class SessionHealthProjection : ISessionHealthProjection
    {
        private readonly Session session;private readonly IMatchClock clock;private readonly string host;
        private long command;
        internal long ArchiveCommand {get{return command;}}
        internal void RestoreArchiveCommand(long value){if(value<0)throw new ArgumentException("projection-command");command=value;}
        public SessionHealthProjection(Session session,IMatchClock clock,string host)
        {if(session==null||clock==null||String.IsNullOrEmpty(host))throw new ArgumentException("projection");this.session=session;this.clock=clock;this.host=host;}
        public RuleResult Project(string id,long epoch,string connection,HealthSnapshot state)
        {
            if(id!=session.Id||epoch!=session.Epoch||state==null)return RuleResult.Reject("projection-scope");
            if(!state.Connected)return RuleResult.Reject("disconnected-projection-api-required");
            if(command==Int64.MaxValue)return RuleResult.Reject("projection-exhausted");
            var c=new CommandContext(id,host,"matchprojection:"+(++command).ToString(System.Globalization.CultureInfo.InvariantCulture),clock.Tick,epoch,true);
            Session.PreparedLifeProjection p;
            if(!session.PrepareLifeProjection(c,connection,state.Id,state,clock.Now+5,null,out p))return RuleResult.Reject("projection-prepare");
            if(!p.Validate()){p.Abort();return RuleResult.Reject("projection-stale");}
            p.CommitValidated();return RuleResult.Accept();
        }
    }
}

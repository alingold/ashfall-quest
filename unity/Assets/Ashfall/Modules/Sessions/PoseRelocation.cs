using System;using Ashfall.Core;
namespace Ashfall.Modules.Sessions
{
    public sealed partial class Session
    {
        // Trusted host safety correction only, absent from client dispatch. It
        // changes the authoritative root/revision while preserving life/counters.
        public RuleResult RelocateRoot(CommandContext context,string actor,int expectedRevision,Float3 acceptedPosition)
        {
            var result=Validate(context);if(!result.Accepted)return result;
            if(!management.CanManage(context))return RuleResult.Reject("manager-required");
            Member member;if(Stage!=SessionStage.Match||!worldReady||actor==null||!players.TryGetValue(actor,out member)||!member.Loaded||!member.HasPosition||member.Waiting||member.Spectator||member.Life==SessionLife.Pending||member.Life==SessionLife.Spectator)return RuleResult.Reject("relocation-state");
            if(expectedRevision<1||member.Revision!=expectedRevision)return RuleResult.Reject("relocation-revision");
            if(!acceptedPosition.IsFinite||Math.Abs(acceptedPosition.X)>100000||Math.Abs(acceptedPosition.Y)>100000||Math.Abs(acceptedPosition.Z)>100000)return RuleResult.Reject("relocation-position");
            if(member.Revision==Int32.MaxValue)return RuleResult.Reject("spawn-revision-exhausted");
            member.Position=acceptedPosition;member.Revision++;return RuleResult.Accept();
        }
    }
}

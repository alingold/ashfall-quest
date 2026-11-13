using System;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
namespace Ashfall.Tools.HealthStates
{
    // Callable by a root-owned native runner. No engine/bootstrap/import side effects.
    public static class HealthEpochNativeFixture
    {
        sealed class Ports : IAuthorityGate,IHealthWorld,IInteractionQuery
        {
            public RuleResult Validate(CommandContext c){return RuleResult.Accept();}
            public double Now {get{return 0;}}public bool InMatch {get{return true;}}public bool TeamChangesAllowed{get{return false;}}
            public bool TryGetPose(string actor,out RescuePose pose){pose=new RescuePose();return false;}
            public bool HasLineOfSight(string actor,string target,Float3 a,Float3 b,float range){return true;}
        }
        public static void Run()
        {
            var p=new Ports();HealthService.EpochClosure close;
            var health=HealthService.CreateEpochParticipant("fixture",0,0,true,p,p,p,out close);
            var host=new CommandContext("fixture","host","register",1,0,true);
            if(!health.Register(host,"actor",-1,false).Accepted)throw new InvalidOperationException("register");
            HealthService.PreparedAdvance token;
            if(!health.PrepareAdvance(new CommandContext("fixture","host","advance",2,0,true),out token).Accepted||!token.Validate())throw new InvalidOperationException("prepare");
            int pending=health.PendingTransitionCount;close.Close();bool blocked=false;
            try{token.CommitValidated();}catch(InvalidOperationException){blocked=true;}
            if(!blocked||health.PendingTransitionCount!=pending||health.EpochAccounting.Publications!=0||health.Advance(host).Accepted)throw new InvalidOperationException("revocation");
            close.CaptureClosedCheckpoint(host,new string('a',64));
        }
    }
}

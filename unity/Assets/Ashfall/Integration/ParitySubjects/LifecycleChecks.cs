using System;
using Ashfall.Core;
using Ashfall.Modules.ParityHarness;
using Ashfall.Modules.Flight;
namespace Ashfall.Integration.ParitySubjects
{
    public static class LifecycleChecks
    {
#if PARITY_CURRENT
        sealed class Transition : IFlightLifecycleAuthorization
        {
            public RuleResult ValidateTransition(CommandContext c,long generation,FlightLifecycle reason)
            {return c.IsAuthority&&c.ActorId=="a"&&generation>0?RuleResult.Accept():RuleResult.Reject("lifecycle principal");}
        }
#endif
        public static Scenario Create()
        {
            return new Scenario("EXP-CAPSULE-RESET","Capsules.cs:ResetActor; LifecyclePorts.cs:IFlightLifecycleAuthorization","Respawn frees only one rider reservation and preserves replay fences and other rider",delegate(Subjects ignored){
#if PARITY_CURRENT
                var q=new Sequence();var world=new AnalyticWorld();var fleet=new CapsuleFleet("s",2,new Gate());var auth=new Transition();CapsuleUpdate u;
                var eligible=new CapsuleEligibility{MapId="archive:alpha",Mode="revive",CapsulesAllowed=true,Match=true,Alive=true,OnDeck=true,Team=0,TeamCount=2};
                Func<string,CapsulePlan> plan=id=>new CapsulePlan(id,0,0,new Float3(),new Float3(0,10,0),new Float3(10,10,0),new Float3(10,0,0),new Float3(12,.3f,0),new Float3(0,0,-1),3);
                var old=q.Next("a");Verify.That(fleet.Begin(old,eligible,plan("one"),0,world,out u).Accepted,"first rider");Verify.That(fleet.Begin(q.Next("b"),eligible,plan("two"),0,world,out u).Accepted,"second rider");
                Verify.That(!fleet.ResetActor(new CommandContext("s","a","client-reset",10,2,false),1,FlightLifecycle.Respawn,auth).Accepted,"unauthorized reset");
                var reset=q.Next("a");Verify.That(fleet.ResetActor(reset,1,FlightLifecycle.Respawn,auth).Accepted,"respawn reset");Verify.That(!fleet.ResetActor(reset,2,FlightLifecycle.Respawn,auth).Accepted,"reset replay");
                Verify.That(!fleet.Begin(old,eligible,plan("one"),0,world,out u).Accepted,"old command replay");Verify.That(fleet.Begin(q.Next("a"),eligible,plan("one"),0,world,out u).Accepted,"own reservation not freed");
                Verify.That(!fleet.Begin(q.Next("a"),eligible,plan("two"),0,world,out u).Accepted,"other reservation lost");Verify.That(fleet.Advance(q.Next("b"),3,true,true,false,world,out u).Accepted&&u.State==CapsuleState.Landed,"other rider lost");
#else
                throw new UnknownEvidenceException("Frozen initial candidate lacks CapsuleFleet.ResetActor; use separately pinned --current input set");
#endif
            });
        }
    }
}

#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Presentation;
using Ashfall.Integration.PersonalSettings;
namespace Ashfall.Tools.Presentation
{
    // Coordinator-only engine_lane.py fixture. These are explicit host/world fixtures,
    // not authentication, geometry/anchor provisioning or rendered-quality evidence.
    public static class PresentationNativeR7Gates
    {
        public static void Run(string newSettingsPath)
        {
            PresentationNativeR6Gates.Run(newSettingsPath); // actual native menu adapter
            var ports=new HostFixture();var hp=new HealthService("r7-native",0,0,true,ports,ports,ports);
            var register=new CommandContext("r7-native","p","register",1,0,true);
            if(!hp.Register(register,"p",-1,false).Accepted)throw new Exception("registration");
            hp.DrainTransitions();var hud=new HudModel();hud.Update(new HudInput(),false,0);
            var sink=new Sink();var binding=new TargetFeedbackBinding(hp,"r7-native",0,"p",hud,sink);
            Hit(hp,2,7);foreach(var change in hp.DrainTransitions())binding.ObserveTransition(change);
            if(sink.Count!=0)throw new Exception("foreign feedback");
            Hit(hp,3,11);foreach(var change in hp.DrainTransitions()) {
                binding.ObserveTransition(change);binding.OnAuthorizedOutgoingTransition(change);
                binding.OnAuthorizedOutgoingTransition(change);
            }
            if(sink.Count!=1||sink.Amount!=11)throw new Exception("exact native fanout");
        }
        private static void Hit(HealthService hp,long tick,float amount)
        {
            var h=hp.Read("p");
            if(!hp.ApplyLoss(new CommandContext("r7-native","p","hit-"+tick,tick,0,true),"p",h.Revision,amount,0,false).Accepted)throw new Exception("actual loss");
        }
        private sealed class Sink:ITargetFeedbackPort
        { public int Count;public float Amount;public void Emit(string target,float amount,bool shield){Count++;Amount=amount;} }
        private sealed class HostFixture:IAuthorityGate,IHealthWorld,IInteractionQuery
        {
            public double Now {get{return 0;}}public bool InMatch {get{return true;}}public bool TeamChangesAllowed {get{return false;}}
            public RuleResult Validate(CommandContext c){return c.IsAuthority?RuleResult.Accept():RuleResult.Reject("fixture-authority");}
            public bool TryGetPose(string id,out RescuePose pose){pose=new RescuePose();return false;}
            public bool HasLineOfSight(string actor,string target,Float3 a,Float3 b,float range){return false;}
        }
    }
}
#endif

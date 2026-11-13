#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
using Ashfall.Modules.Presentation;
using Ashfall.Integration.PersonalSettings.Engine;
using Ashfall.Integration.PersonalSettings;
using Ashfall.Modules.HealthStates;
using Ashfall.Core;
namespace Ashfall.Tools.Presentation
{
    // Staged for root engine_lane only. Source compilation is not execution.
    public static class PresentationNativeR9Gates
    {
        public static void Run(string freshDestination)
        {
            PresentationNativeR7Gates.Run(freshDestination);
            var go=new GameObject("QuarantinedR9TargetFixture");
            try{
                var label=go.AddComponent<TextMesh>();var hud=new HudModel();hud.Update(new HudInput(),false,0);
                var view=new NativeTargetFeedbackView(hud);view.BindTarget("target",label);
                var world=new HostFixture();var hp=new HealthService("r9-native",0,0,true,world,world,world);
                if(!hp.Register(new CommandContext("r9-native","target","reg",1,0,true),"target",-1,false).Accepted)throw new Exception("register");hp.DrainTransitions();
                var target=new TargetFeedbackBinding(hp,"r9-native",0,"target",hud,view);
                for(int i=0;i<2;i++){
                    var before=hp.Read("target");if(!hp.ApplyLoss(new CommandContext("r9-native","target","hit"+i,2+i,0,true),"target",before.Revision,i==0?7:11,0,false).Accepted)throw new Exception("actual loss");
                    foreach(var e in hp.DrainTransitions()){target.ObserveTransition(e);if(i==1){target.OnAuthorizedOutgoingTransition(e);target.OnAuthorizedOutgoingTransition(e);}}
                    if(label.text!=(i==0?"":"11"))throw new Exception("foreign7/local11 exact caption");
                }
                hud.Update(new HudInput(),true,0);hud.Update(new HudInput(),false,0);view.Refresh(0);
                if(label.text!="")throw new Exception("reset hide/reopen");
                view.Emit("target",11,true);view.Refresh(1.16f);if(label.text!="")throw new Exception("expiry");
                view.Emit("foreign",7,false);if(label.text!="")throw new Exception("unknown anchor");
                view.Emit("target",11,false);view.Clear();if(label.text!="")throw new Exception("disable clear");
            }finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        sealed class HostFixture:IAuthorityGate,IHealthWorld,IInteractionQuery
        {
            public double Now{get{return 0;}}public bool InMatch{get{return true;}}public bool TeamChangesAllowed{get{return false;}}
            public RuleResult Validate(CommandContext c){return c.IsAuthority?RuleResult.Accept():RuleResult.Reject("fixture");}
            public bool TryGetPose(string id,out RescuePose pose){pose=new RescuePose();return false;}
            public bool HasLineOfSight(string a,string b,Float3 c,Float3 d,float range){return false;}
        }
    }
}
#endif

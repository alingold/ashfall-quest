using System;
using Ashfall.Core;
namespace Ashfall.Modules.Climbing
{
    public sealed class PreparedClimbMotion
    {
        internal readonly ClimbController Owner,Staged;
        internal readonly long Revision;
        internal bool Finished;
        public ClimbStep Step {get;private set;}
        internal PreparedClimbMotion(ClimbController owner,ClimbController staged,long revision,ClimbStep step)
        {Owner=owner;Staged=staged;Revision=revision;Step=step;}
        public RuleResult Validate(){return Owner.ValidatePrepared(this);}
        public void CommitValidated(){Owner.Publish(this);}
        public void Abort(){Owner.Abort(this);}
    }
    public sealed partial class ClimbController
    {
        long revision;
        PreparedClimbMotion pending;
        static bool SupportedFields()
        {
            string[] expected="world,held,anchors,releaseVelocity,route,segment,elapsed,travelled,length,sequence,tick,revision,pending".Split(',');
            var fields=typeof(ClimbController).GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public);
            if(fields.Length!=expected.Length)return false;
            foreach(var field in fields)if(Array.IndexOf(expected,field.Name)<0)return false;
            return true;
        }
        ClimbController CopyState()
        {
            var copy=new ClimbController(world);
            Array.Copy(held,copy.held,held.Length);Array.Copy(anchors,copy.anchors,anchors.Length);
            copy.releaseVelocity=releaseVelocity;
            copy.route=route==null?null:(Float3[])route.Clone();
            copy.segment=segment;copy.elapsed=elapsed;copy.travelled=travelled;copy.length=length;
            copy.sequence=sequence;copy.tick=tick;
            return copy;
        }
        // Local actual controller preparation. Collision queries must be pure on the
        // host lane; root must separately reserve their geometry/source revision.
        public RuleResult PrepareStep(InputFrame input,Float3 root,float seconds,ClimbOptions options,
            bool desktop,bool climbPressed,Float3 forward,bool reset,out PreparedClimbMotion plan)
        {
            plan=null;if(!SupportedFields())return RuleResult.Reject("unaccounted climb fields");if(pending!=null)return RuleResult.Reject("climb reservation outstanding");
            long before=revision;var staged=CopyState();if(reset)staged.Reset();
            var result=desktop?staged.StepDesktop(input,root,seconds,options,climbPressed,forward):staged.Step(input,root,seconds,options);
            if(!result.Accepted)return RuleResult.Reject(result.Reason??"climb preparation refused");
            if(revision!=before||pending!=null)return RuleResult.Reject("climb changed during preparation");
            plan=new PreparedClimbMotion(this,staged,before,result);pending=plan;return RuleResult.Accept();
        }
        public RuleResult CheckpointReady()
        {return !SupportedFields()?RuleResult.Reject("unaccounted climb fields"):pending==null?RuleResult.Accept():RuleResult.Reject("climb reservation outstanding");}
        internal RuleResult ValidatePrepared(PreparedClimbMotion plan)
        {return plan!=null&&plan==pending&&!plan.Finished&&plan.Revision==revision?RuleResult.Accept():RuleResult.Reject("climb reservation/revision");}
        internal void Publish(PreparedClimbMotion plan)
        {
            if(!ValidatePrepared(plan).Accepted)throw new InvalidOperationException("stale climb publication");
            var staged=plan.Staged;
            Array.Copy(staged.held,held,held.Length);Array.Copy(staged.anchors,anchors,anchors.Length);
            releaseVelocity=staged.releaseVelocity;route=staged.route;segment=staged.segment;
            elapsed=staged.elapsed;travelled=staged.travelled;length=staged.length;
            sequence=staged.sequence;tick=staged.tick;revision++;plan.Finished=true;pending=null;
        }
        internal void Abort(PreparedClimbMotion plan)
        {if(plan.Finished)return;plan.Finished=true;if(pending==plan)pending=null;}
    }
}

from pathlib import Path
R=Path.cwd(); p=R/'unity/Assets/Ashfall/Modules/Flight/Capsules.cs';s=p.read_text();f=(R/'unity/Assets/Ashfall/Modules/Flight/FlightMachine.cs').read_text();f=f[f.index('    internal sealed class CommandFence'):f.index('    public enum FlightMode')];f=f.replace('internal sealed class CommandFence','internal sealed class CapsuleCommandFence').replace('internal CommandFence(', 'internal CapsuleCommandFence(');at=f.index('        // Validation never')
f=f[:at]+'''        internal CapsuleCommandFence Copy()
        {
            var copy=new CapsuleCommandFence(session,actor,epoch,gate);
            copy.newest=newest;
            foreach(var entry in seen)copy.seen.Add(entry.Key,entry.Value);
            return copy;
        }
'''+f[at:]
s=s.replace('    public sealed class CapsuleFleet',f+'    public sealed class CapsuleFleet').replace('Dictionary<string,CommandFence>','Dictionary<string,CapsuleCommandFence>').replace('new CommandFence(', 'new CapsuleCommandFence(').replace('out CommandFence','out CapsuleCommandFence').replace('CommandFence fence','CapsuleCommandFence fence').replace('CommandFence ordinary','CapsuleCommandFence ordinary').replace('internal CommandFence Fence','internal CapsuleCommandFence Fence')
s=s.replace('readonly Dictionary<string,','Dictionary<string,').replace('readonly HashSet<string>','HashSet<string>')
s=s.replace('{fence.Commit(c);','{revision++;fence.Commit(c);').replace('            life.Fence.Commit(c);','            revision++;life.Fence.Commit(c);')
# add owner API within actual fleet
pos=s.index('        public CapsuleFleet(')
s=s[:pos]+'''        long revision;
        PreparedCapsuleUpdate pending;
        CapsuleFleet CopyState()
        {
            var copy=new CapsuleFleet(session,epoch,gate);
            foreach(var entry in commands)copy.commands.Add(entry.Key,entry.Value.Copy());
            foreach(var entry in lives)copy.lives.Add(entry.Key,new Life{Generation=entry.Value.Generation,Tick=entry.Value.Tick,Inactive=entry.Value.Inactive,Fence=entry.Value.Fence.Copy()});
            foreach(var entry in rides)copy.rides.Add(entry.Key,new Ride{Plan=entry.Value.Plan,Start=entry.Value.Start,Last=entry.Value.Last,State=entry.Value.State});
            foreach(var pod in pods)copy.pods.Add(pod);
            return copy;
        }
        // Local owner checkpoint readiness only; no global retirement assertion.
        public RuleResult CheckpointReady()
        {return pending!=null?RuleResult.Reject("capsule reservation outstanding"):RuleResult.Accept();}
        RuleResult Reserve(CapsuleFleet staged,long original,RuleResult result,CapsuleUpdate update,out PreparedCapsuleUpdate plan)
        {
            plan=null;
            if(!result.Accepted)return result;
            if(original!=revision||pending!=null)return RuleResult.Reject("capsule changed during preparation");
            plan=new PreparedCapsuleUpdate(this,staged,revision,update);pending=plan;return result;
        }
        public RuleResult PrepareBegin(CommandContext c,CapsuleEligibility eligibility,CapsulePlan route,double now,ICapsuleRouteValidation validation,out PreparedCapsuleUpdate plan)
        {
            plan=null;if(pending!=null)return RuleResult.Reject("capsule reservation outstanding");
            long before=revision;var staged=CopyState();CapsuleUpdate update;
            var result=staged.Begin(c,eligibility,route,now,validation,out update);
            return Reserve(staged,before,result,update,out plan);
        }
        public RuleResult PrepareAdvance(CommandContext c,double now,bool match,bool alive,bool eject,IFlightWorld world,out PreparedCapsuleUpdate plan)
        {
            plan=null;if(pending!=null)return RuleResult.Reject("capsule reservation outstanding");
            long before=revision;var staged=CopyState();CapsuleUpdate update;
            var result=staged.Advance(c,now,match,alive,eject,world,out update);
            return Reserve(staged,before,result,update,out plan);
        }
        public RuleResult PrepareResetActor(CommandContext c,long generation,FlightLifecycle reason,IFlightLifecycleAuthorization authorization,out PreparedCapsuleUpdate plan)
        {
            plan=null;if(pending!=null)return RuleResult.Reject("capsule reservation outstanding");
            long before=revision;var staged=CopyState();
            var result=staged.ResetActor(c,generation,reason,authorization);
            return Reserve(staged,before,result,new CapsuleUpdate(),out plan);
        }
        internal RuleResult ValidatePrepared(PreparedCapsuleUpdate plan)
        {return plan!=null&&plan==pending&&plan.Revision==revision&&!plan.Finished?RuleResult.Accept():RuleResult.Reject("capsule reservation/revision");}
        internal void Publish(PreparedCapsuleUpdate plan)
        {
            if(!ValidatePrepared(plan).Accepted)throw new InvalidOperationException("stale capsule publication");
            commands=plan.Staged.commands;rides=plan.Staged.rides;pods=plan.Staged.pods;lives=plan.Staged.lives;
            revision++;plan.Finished=true;pending=null;
        }
        internal void Abort(PreparedCapsuleUpdate plan)
        {if(plan.Finished)return;plan.Finished=true;if(pending==plan)pending=null;}
'''+s[pos:]
pos=s.index('    public sealed class CapsuleFleet')
s=s[:pos]+'''    // Host-lane owner mechanics capability. Geometry/life/connection must remain
    // reserved by the root composition; this token does not certify those domains.
    public sealed class PreparedCapsuleUpdate
    {
        internal readonly CapsuleFleet Owner,Staged;internal readonly long Revision;internal bool Finished;
        public CapsuleUpdate Update {get;private set;}
        internal PreparedCapsuleUpdate(CapsuleFleet owner,CapsuleFleet staged,long revision,CapsuleUpdate update)
        {Owner=owner;Staged=staged;Revision=revision;Update=update;}
        public RuleResult Validate(){return Owner.ValidatePrepared(this);}
        public void CommitValidated(){Owner.Publish(this);}
        public void Abort(){Owner.Abort(this);}
    }
'''+s[pos:]
p.write_text(s)
p=R/'unity/Assets/Ashfall/Modules/Climbing/ClimbController.cs';s=p.read_text().replace('public sealed class ClimbController','public sealed partial class ClimbController').replace('public void Reset() { Cancel();','public void Reset() { revision++; Cancel();').replace('            ClimbStep result=new ClimbStep();','            revision++;\n            ClimbStep result=new ClimbStep();');p.write_text(s)

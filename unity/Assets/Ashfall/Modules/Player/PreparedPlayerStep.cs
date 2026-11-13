using System;
using Ashfall.Core;
namespace Ashfall.Modules.Player
{
    // Single trusted host lane. Query ports must be immutable, pure and stable through
    // validate-all/publication. The token never restores previously published state.
    public sealed class PreparedPlayerStep
    {
        internal readonly Locomotion Owner, Next;
        internal readonly long Revision, Reservation;
        internal readonly float Walk, Snap, Smooth;
        internal readonly bool Analog, Ease;
        internal readonly TurnStyle Turning;
        internal bool Finished;
        public MotionStep Motion {get; private set;}
        public Float3 Velocity {get{return Next.Velocity;}}
        internal PreparedPlayerStep(Locomotion owner,Locomotion next,long revision,long reservation,MotionStep motion,PlayerTuning tuning)
        {Owner=owner;Next=next;Revision=revision;Reservation=reservation;Motion=motion;
         Walk=tuning.WalkMultiplier;Snap=tuning.SnapRadians;Smooth=tuning.SmoothRadiansPerSecond;
         Analog=tuning.SmoothAnalog;Ease=tuning.SmoothEaseIn;Turning=tuning.Turning;}
        public RuleResult Validate(){return Owner.ValidatePrepared(this);}
        // Caller validates all participants immediately before publication; no callbacks
        // between validation and commits. A stale caller is refused before Player effects.
        public void CommitValidated(){Owner.CommitPrepared(this);}
        public void Abort(){Finished=true;}
    }
    public sealed partial class Locomotion
    {
        long revision, reservation;
        public RuleResult PrepareStep(InputFrame input,MotionEnvironment world,float seconds,out PreparedPlayerStep plan)
        {
            plan=null;reservation++;long sourceRevision=revision,sourceReservation=reservation;
            // A private detached instance runs the actual production algorithm, including
            // input edges, stamina, and turn policy. Nothing is published during calculation.
            Locomotion next=(Locomotion)MemberwiseClone();
            try
            {
                MotionStep motion=next.CalculateStep(input,world,seconds);
                if(!next.velocity.IsFinite||Math.Abs(next.velocity.X)>10000||Math.Abs(next.velocity.Y)>10000||Math.Abs(next.velocity.Z)>10000)
                    return RuleResult.Reject("prepared player output");
                plan=new PreparedPlayerStep(this,next,sourceRevision,sourceReservation,motion,tuning);
                return RuleResult.Accept();
            }
            catch(ArgumentException){return RuleResult.Reject("prepared player input/settings");}
        }
        internal RuleResult ValidatePrepared(PreparedPlayerStep p)
        {
            return p.Finished||!Object.ReferenceEquals(p.Owner,this)||p.Revision!=revision||p.Reservation!=reservation||
                p.Walk!=tuning.WalkMultiplier||p.Snap!=tuning.SnapRadians||p.Smooth!=tuning.SmoothRadiansPerSecond||
                p.Analog!=tuning.SmoothAnalog||p.Ease!=tuning.SmoothEaseIn||p.Turning!=tuning.Turning
                ?RuleResult.Reject("prepared player revision/reservation"):RuleResult.Accept();
        }
        internal void CommitPrepared(PreparedPlayerStep p)
        {
            if(!ValidatePrepared(p).Accepted)throw new InvalidOperationException("stale Player publication");
            Locomotion n=p.Next;
            velocity=n.velocity;lastTick=n.lastTick;lastSequence=n.lastSequence;
            jumpHeld=n.jumpHeld;sprintHeld=n.sprintHeld;turnNeedsNeutral=n.turnNeedsNeutral;snapReady=n.snapReady;
            sprintTier=n.sprintTier;stamina=n.stamina;snapTimer=n.snapTimer;turnRate=n.turnRate;
            p.Finished=true;revision++;
        }
    }
}

using Ashfall.Core;
using Ashfall.Modules.Climbing;
using Ashfall.Modules.Player;
using Ashfall.Modules.Flight;
namespace Ashfall.Integration.ClimbingMotor
{
    // Publication policy only: never steps gravity or applies a second displacement.
    public static class ReleaseFeedback
    {
        public static FlightCollisionResult Resolve(bool owns,ClimbStep step,PlayerMotorResult applied,float seconds)
        {
            if(applied.HasPhaseFeedback)
            {
                if(!applied.AppliedPhysicalTranslation.IsFinite||float.IsNaN(seconds)||seconds<=0||seconds>.1f)
                    throw new System.ArgumentException("physical phase feedback");
                var p=applied.AppliedPhysicalTranslation;
                applied.Velocity=new Float3(p.X/seconds,p.Y/seconds,p.Z/seconds);
            }
            return Resolve(owns,step,applied);
        }
        public static FlightCollisionResult Resolve(bool owns,ClimbStep step,PlayerMotorResult applied)
        {
            bool release=owns&&step.Launch&&applied.Complete;
            var velocity=release?step.Velocity:applied.Velocity;
            // Ground contact describes the move just completed. A new positive launch
            // is an airborne handoff; a ceiling still clips it even on a zero move.
            bool grounded=applied.Grounded&&!(release&&velocity.Y>0&&!applied.CeilingBlocked);
            if((grounded||applied.CeilingBlocked)&&velocity.Y>0)
                velocity=new Float3(velocity.X,0,velocity.Z);
            return new FlightCollisionResult{Velocity=velocity,Grounded=grounded,
                CeilingBlocked=applied.CeilingBlocked,ExternalMotionOwner=owns};
        }
    }
}

using System;
using Ashfall.Core;
namespace Ashfall.Modules.Player
{
    public static class PlayerMotorFeedback
    {
        // Ground/ceiling flags belong to the physical phase, never the pivot phase.
        public static PlayerMotorResult ResolvePhases(RootMotionProposal p,Float3 physical,Float3 pivot,float seconds,bool grounded,bool ceiling)
        {
            if(!pivot.IsFinite)throw new ArgumentException("pivot feedback");
            RootMotionProposal physicalProposal=p;physicalProposal.PivotCorrection=new Float3();
            PlayerMotorResult result=Resolve(physicalProposal,physical,seconds,grounded,ceiling);
            double x=(double)pivot.X-p.PivotCorrection.X,y=(double)pivot.Y-p.PivotCorrection.Y,z=(double)pivot.Z-p.PivotCorrection.Z;
            result.Complete=result.Complete&&x*x+y*y+z*z<=1e-10;
            result.HasPhaseFeedback=true;result.AppliedPhysicalTranslation=physical;result.AppliedPivotCorrection=pivot;
            result.AppliedDisplacement=new Float3(physical.X+pivot.X,physical.Y+pivot.Y,physical.Z+pivot.Z);
            return result;
        }
        public static PlayerMotorResult Resolve(RootMotionProposal p,Float3 applied,float seconds,bool grounded,bool ceiling)
        {
            if(!applied.IsFinite||!PlayerInput.Finite(seconds)||seconds<=0||seconds>.1f)throw new ArgumentException("motor feedback");
            Float3 requested=new Float3(p.Translation.X+p.PivotCorrection.X,p.Translation.Y+p.PivotCorrection.Y,p.Translation.Z+p.PivotCorrection.Z);
            double x=(double)applied.X-requested.X,y=(double)applied.Y-requested.Y,z=(double)applied.Z-requested.Z;
            // A walkable floor may consume downward grounding motion. Horizontal
            // translation and the full pivot correction must still have completed.
            bool floorResolved=grounded&&requested.Y<=0&&applied.Y>=requested.Y&&applied.Y<=.000001f;
            bool complete=x*x+z*z+(floorResolved?0:y*y)<=1e-10;
            if(!complete&&(p.PivotCorrection.X!=0||p.PivotCorrection.Y!=0||p.PivotCorrection.Z!=0))return new PlayerMotorResult{AppliedDisplacement=applied,Complete=false,Grounded=grounded,CeilingBlocked=ceiling};
            Float3 velocity=new Float3((applied.X-(complete?p.PivotCorrection.X:0))/seconds,(applied.Y-(complete?p.PivotCorrection.Y:0))/seconds,(applied.Z-(complete?p.PivotCorrection.Z:0))/seconds);
            if(ceiling&&velocity.Y>0)velocity=new Float3(velocity.X,0,velocity.Z);
            if(!velocity.IsFinite||Math.Abs(velocity.X)>10000||Math.Abs(velocity.Y)>10000||Math.Abs(velocity.Z)>10000)throw new ArgumentException("motor velocity");
            return new PlayerMotorResult{AppliedDisplacement=applied,Velocity=velocity,Complete=complete,Grounded=grounded,CeilingBlocked=ceiling};
        }
    }
}

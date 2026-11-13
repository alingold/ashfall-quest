using System;
using Ashfall.Core;
namespace Ashfall.Integration.FlightMotor
{
    public static class FlightPhysicalFeedback
    {
        // Solver must report locomotion translation separately from turn-pivot correction,
        // especially for partial collision. Total root delta cannot recover that split.
        public static Ashfall.Modules.Flight.FlightCollisionResult Resolve(Float3 appliedPhysicalTranslation,float seconds,bool grounded,bool ceiling,bool externalOwner)
        {
            if(!appliedPhysicalTranslation.IsFinite||Single.IsNaN(seconds)||Single.IsInfinity(seconds)||seconds<=0||seconds>.1f)throw new ArgumentException("physical solver output");
            Float3 v=new Float3(appliedPhysicalTranslation.X/seconds,appliedPhysicalTranslation.Y/seconds,appliedPhysicalTranslation.Z/seconds);
            if((grounded||ceiling)&&v.Y>0)v=new Float3(v.X,0,v.Z);
            if(!v.IsFinite||Math.Abs(v.X)>10000||Math.Abs(v.Y)>10000||Math.Abs(v.Z)>10000)throw new ArgumentException("physical solver velocity");
            return new Ashfall.Modules.Flight.FlightCollisionResult{Velocity=v,Grounded=grounded,CeilingBlocked=ceiling,ExternalMotionOwner=externalOwner};
        }
    }
}

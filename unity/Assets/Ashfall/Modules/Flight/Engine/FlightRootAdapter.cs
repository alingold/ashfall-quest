#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
using Ashfall.Core;
namespace Ashfall.Modules.Flight
{
    // Explicitly driven by the central host loop; no Update, input sampling, camera or child writes.
    public sealed class FlightRootAdapter : MonoBehaviour, IFlightRoot, IFlightWorld
    {
        public LayerMask StructuralMask;
        public float RootRadius=.27f, StandingHeight=1.75f;
        public event Action<Float3> FlightVelocity;
        public event Action<Float3> RootRelocation;
        public void SetFlightVelocity(Float3 velocity)
        {
            if(!velocity.IsFinite)throw new ArgumentException("velocity");
            // The central Player root owner subscribes; this component never runs a second motor.
            if(FlightVelocity==null)throw new InvalidOperationException("Bind the authoritative Player velocity sink");
            FlightVelocity(velocity);
        }
        public void RelocateFlightRoot(Float3 destination)
        {
            if(!destination.IsFinite)throw new ArgumentException("destination");
            if(RootRelocation==null)throw new InvalidOperationException("Bind the authoritative Player relocation sink");
            RootRelocation(destination);
        }
        public bool ClearPadContact(string padId,Float3 face,Float3 contact)
        {
            if(!face.IsFinite||!contact.IsFinite||StructuralMask.value==0)return false;
            Vector3 a=ToUnity(face),b=ToUnity(contact);
            return (b-a).sqrMagnitude<.01f||!Physics.Linecast(a,b,StructuralMask,QueryTriggerInteraction.Ignore);
        }
        public bool ClearCapsuleExit(string actorId,Float3 destination)
        {
            if(!destination.IsFinite||StructuralMask.value==0||RootRadius<=0||StandingHeight<RootRadius*2)return false;
            Vector3 at=ToUnity(destination);
            return !Physics.CheckCapsule(at+Vector3.up*(RootRadius+.02f),at+Vector3.up*(StandingHeight-RootRadius+.02f),RootRadius,StructuralMask,QueryTriggerInteraction.Ignore);
        }
        public RuleResult ApplyStep(FlightMachine machine,CommandContext context,FlightFrame frame)
        {
            return RuleResult.Reject("Migrate to exclusive PlayerFlightMotor Step before root application");
        }
        public RuleResult ApplyCapsule(CapsuleFleet fleet,CommandContext context,double now,bool match,bool alive,bool eject)
        { return RuleResult.Reject("Prepared exclusive Player capsule relocation API required"); }
        public RuleResult ApplyPad(PadContacts contacts,FlightMachine machine,LaunchPad pad,CommandContext context,Float3 feet,Float3 previous,bool hasPrevious,double now,bool ready,bool climbing,bool mantling,bool ghost)
        { return RuleResult.Reject("Migrate to exclusive PlayerFlightMotor LaunchPad"); }
        public RuleResult BeginCapsule(CapsuleFleet fleet,CommandContext context,CapsuleEligibility eligibility,CapsulePlan plan,double now,ICapsuleRouteValidation routes)
        { return RuleResult.Reject("Prepared exclusive Player capsule relocation API required"); }
        static Vector3 ToUnity(Float3 p){return new Vector3(p.X,p.Y,p.Z);}
    }
}
#endif


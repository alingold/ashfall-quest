#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
using Ashfall.Modules.Player;
using Ashfall.Modules.Climbing;
using Ashfall.Integration.ClimbingMotor;
using Ashfall.Integration.MotionAuthority;
public static class CertifiedMotionR4NativeFixture
{
    // Root supplies actual approved body, canonical authorization and real participant.
    // This fixture is staged/compiled only. It certifies no geometry or gameplay content.
    public static void AssertPreparationPreservesActualBody(CharacterController body,
        MotionPublicationPreparation participant,PreparedMotionLease lease,MotorFrame valid,
        ClimbController climb)
    {
        if(body==null||participant==null||lease==null||climb==null)throw new ArgumentException("actual bindings");
        Vector3 before=body.transform.position;Quaternion rotation=body.transform.rotation;
        bool left=climb.LeftAnchored,right=climb.RightAnchored,mantle=climb.Mantling;
        var malformed=valid;malformed.Environment.HeadingRadians=Single.NaN;
        PreparedMotionPublication plan;
        if(participant.Prepare(lease,malformed,out plan).Accepted||plan!=null)throw new Exception("invalid frame admitted");
        if(!lease.Validate().Accepted)throw new Exception("rejected frame consumed lease");
        if(!participant.Prepare(lease,valid,out plan).Accepted)throw new Exception("corrected frame not retryable");
        using(plan)
        {
            if(!plan.Validate().Accepted)throw new Exception("actual preflight refused");
            if(plan.Publish().Accepted)throw new Exception("unsupported physical certificate accepted");
            if(body.transform.position!=before||body.transform.rotation!=rotation||climb.LeftAnchored!=left||
                climb.RightAnchored!=right||climb.Mantling!=mantle)throw new Exception("detached preparation leaked effects");
        }
        if(!climb.CheckpointReady().Accepted)throw new Exception("actual climb reservation leaked");
        if(!participant.Prepare(lease,valid,out plan).Accepted)throw new Exception("abort lost retry");
        plan.Abort();
    }
}
#endif

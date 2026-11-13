#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Modules.Player;
using Ashfall.Modules.Climbing;
using Ashfall.Integration.ClimbingMotor;
using Ashfall.Integration.MotionAuthority;
using UnityEngine;
public static class TransactionsR2NativeFixture
{
    // Coordinator invokes under engine_lane.py, with the actual actor/controller.
    // This tests refusal only. It does not establish a certified solver exists.
    public static void AssertLegacyRefusal(HostMotionArbiter arbiter,CharacterController controller,MotorFrame frame)
    {
        Vector3 position=controller.transform.position;
        Quaternion rotation=controller.transform.rotation;
        PreparedMotionLease lease;
        if(!arbiter.Leases.Prepare(arbiter.Leases.Binding,frame.Context,frame.Input.Sequence,MotionOwner.Locomotion,out lease).Accepted)
            throw new InvalidOperationException("fixture lease admission");
        ClimbStep climbing;PlayerMotorResult result;
        if(arbiter.StepLocomotion(lease,frame,out climbing,out result).Accepted||!lease.Validate().Accepted||
            controller.transform.position!=position||controller.transform.rotation!=rotation)
            throw new InvalidOperationException("legacy refusal moved actual controller or consumed lease");
    }
}
#endif

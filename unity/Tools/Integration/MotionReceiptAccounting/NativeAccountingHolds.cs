#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Modules.Climbing;
using Ashfall.Integration.FlightMotor;
using Ashfall.Integration.MotionAuthority;
using UnityEngine;
public static class NativeAccountingHolds
{
    static void Hold(bool ok,string name){if(!ok)throw new Exception(name);}
    // Root-only native invocation. Each fixture uses an isolated empty envelope.
    public static void Run(bool revoke)
    {
        var go=new GameObject("MotionReceiptAccountingR5 isolated fixture");
        try {
            go.transform.position=new Vector3(10000,10000,10000);
            var body=go.AddComponent<CharacterController>();var owner=NativeRootPublication.CreateIsolated(body);
            var ports=new AccountingTests.Ports();var motor=new PlayerFlightMotor("s","a",2,ports,new PlayerTuning(),1);
            var climb=new ClimbController(new AccountingTests.Collision());var leaseHost=new HostMotionLease(AccountingTests.Binding(),ports);
            var preparation=new MotionPublicationPreparation(leaseHost,climb,motor,ports,ports,ports,ports);
            var f=AccountingTests.Frame(1);f.Root=new Float3(10000,10000,10000);f.Input.HeadPosition=new Float3(10000,10001.75f,10000);f.Input.MoveY=1;f.Flight.InputEnabled=true;
            PreparedMotionLease lease;PreparedMotionPublication mechanics;NativeMotionReceiptAssociation association;
            Hold(leaseHost.Prepare(AccountingTests.Binding(),f.Context,1,MotionOwner.Locomotion,out lease).Accepted,"lease");
            Hold(preparation.Prepare(lease,f,out mechanics).Accepted,"mechanics");
            Hold(NativeMotionReceiptAssociation.PrepareIsolated(owner,mechanics,out association).Accepted,"native association");
            Hold(association.Publish()==NativeRootStatus.AppliedPendingAccounting,"native applied");
            var receipt=association.Result;Hold(receipt!=null&&receipt.PhysicalDelta.Z>0,"nonzero actual physical motion");
            mechanics.Abort();association.Dispose();Hold(association.Result==receipt,"abort retains applied result");
            NativeRootPublication.Prepared another;Hold(!owner.PrepareIsolated(mechanics.Proposal,.02f,out another).Accepted,"body blocked");
            Hold(association.Complete(null)==ReceiptAccountingState.RetainedPending,"foreign/null receipt refused");
            if(revoke)leaseHost.Invalidate();
            var state=association.Complete(receipt);
            Hold(state==(revoke?ReceiptAccountingState.RetainedPending:ReceiptAccountingState.MechanicsCommittedPendingOwner),"actual accounting disposition");
            var velocity=motor.Velocity;Hold(association.Complete(receipt)==state&&motor.Velocity.Z==velocity.Z,"idempotent completion");
            Hold(!owner.Account(receipt).Accepted&&owner.PendingResult==receipt,"missing owner acknowledgement explicit");
            var terminal=association.Retire(NativeRootTerminalReason.UnrecoverableAccounting);
            Hold(terminal!=null&&terminal.Result==receipt,"exact terminal receipt");
            Hold(association.Complete(receipt)==ReceiptAccountingState.Terminal,"terminal cannot replay");
        }finally{UnityEngine.Object.DestroyImmediate(go);}
    }
}
#endif

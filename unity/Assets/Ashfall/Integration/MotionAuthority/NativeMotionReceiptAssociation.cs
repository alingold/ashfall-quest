#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Integration.FlightMotor;
using Ashfall.Integration.ClimbingMotor;
using UnityEngine;
namespace Ashfall.Integration.MotionAuthority
{
    public enum ReceiptAccountingState { Prepared, Aborted, RetainedPending, MechanicsCommittedPendingOwner, Terminal }
    // Isolated host-lane association only. Canonical production admission is absent.
    // No public constructor accepts a receipt, result, completion flag or callback.
    public sealed class NativeMotionReceiptAssociation : IDisposable
    {
        readonly NativeRootPublication owner;
        readonly PreparedMotionPublication mechanics;
        readonly NativeRootPublication.Prepared native;
        readonly NativeReceiptAccounting accounting;
        readonly int thread;
        NativeRootPublication.Receipt receipt;
        bool busy, folded;
        public ReceiptAccountingState State {get;private set;}
        public NativeRootPublication.Receipt Result {get{return receipt;}}
        public const string MissingOwnerPort="NativeRootPublication.Account(Receipt) has no successful prepared-mechanics acknowledgement; canonical production binding unavailable";
        NativeMotionReceiptAssociation(NativeRootPublication owner,PreparedMotionPublication mechanics,NativeRootPublication.Prepared native)
        {this.owner=owner;this.mechanics=mechanics;this.native=native;accounting=new NativeReceiptAccounting(mechanics.Motor);thread=System.Threading.Thread.CurrentThread.ManagedThreadId;}
        bool Lane {get{return thread==System.Threading.Thread.CurrentThread.ManagedThreadId;}}
        public static RuleResult PrepareIsolated(NativeRootPublication owner,PreparedMotionPublication mechanics,out NativeMotionReceiptAssociation association)
        {
            association=null;if(owner==null||mechanics==null||mechanics.Applied)return RuleResult.Reject("association binding");
            var r=mechanics.Validate();if(!r.Accepted)return r;
            NativeRootPublication.Prepared native;
            r=owner.PrepareIsolated(mechanics.Proposal,mechanics.Frame.Flight.DeltaSeconds,out native);if(!r.Accepted)return r;
            try{association=new NativeMotionReceiptAssociation(owner,mechanics,native);return RuleResult.Accept();}
            finally{if(association==null)native.Dispose();}
        }
        public NativeRootStatus Publish()
        {
            if(!Lane||busy||State!=ReceiptAccountingState.Prepared)return mechanics.Applied||receipt!=null?NativeRootStatus.AppliedPendingAccounting:NativeRootStatus.Refused;
            busy=true;
            try
            {
                if(!mechanics.Validate().Accepted)return NativeRootStatus.Refused;
                mechanics.Applied=true;
                State=ReceiptAccountingState.RetainedPending;
                NativeRootPublication.Receipt result;var status=owner.Publish(native,out result);
                if(status==NativeRootStatus.Refused){mechanics.Applied=false;State=ReceiptAccountingState.Prepared;return status;}
                receipt=result;State=ReceiptAccountingState.RetainedPending;
                return NativeRootStatus.AppliedPendingAccounting;
            }
            finally{busy=false;}
        }
        public ReceiptAccountingState Complete(NativeRootPublication.Receipt exact)
        {
            if(!Lane||busy||receipt==null||!System.Object.ReferenceEquals(exact,receipt)||!System.Object.ReferenceEquals(owner.PendingResult,receipt))return State;
            if(owner.Terminal!=null){State=ReceiptAccountingState.Terminal;return State;}
            if(State==ReceiptAccountingState.Terminal||State==ReceiptAccountingState.MechanicsCommittedPendingOwner)return State;
            busy=true;
            try
            {
                // Interrupted/unknown native phases cannot invent collision feedback.
                if(receipt.Interrupted||!receipt.PoseKnown||!receipt.PhysicalFlagsKnown||receipt.Phase!=NativeRootPhase.Completed)return State;
                if(!mechanics.Owner.ValidateAccounting(mechanics).Accepted||!mechanics.Climb.Validate().Accepted||!accounting.Validate().Accepted)return State;
                if(!folded)
                {
                    var flags=receipt.PhysicalFlags; // Pivot contacts never become physical velocity or grounding.
                    var applied=PlayerMotorFeedback.ResolvePhases(receipt.Proposal,receipt.PhysicalDelta,receipt.PivotDelta,receipt.Seconds,
                        (flags&CollisionFlags.Below)!=0,(flags&CollisionFlags.Above)!=0);
                    bool owns=mechanics.ClimbOwnsMotion;
                    var feedback=ReleaseFeedback.Resolve(owns,mechanics.Climbing,applied,receipt.Seconds);
                    if(!accounting.PrepareCollision(feedback).Accepted)return State;folded=true;
                }
                if(!accounting.Validate().Accepted||!mechanics.Climb.Validate().Accepted)return State;
                accounting.CommitValidated();mechanics.Climb.CommitValidated();
                State=ReceiptAccountingState.MechanicsCommittedPendingOwner;
                // Deliberately do not call the universally refusing Account, erase the
                // native result, free the body, abort or rerun any physical phase.
                return State;
            }
            catch(InvalidOperationException){return State;}
            finally{busy=false;}
        }
        public NativeRootPublication.TerminalRecord Retire(NativeRootTerminalReason reason)
        {
            if(!Lane||busy)return null;var terminal=owner.Retire(reason);
            if(terminal!=null){if(receipt==null)Dispose();State=ReceiptAccountingState.Terminal;}return terminal;
        }
        public void Dispose()
        {
            if(!Lane||busy||receipt!=null||mechanics.Applied)return;
            native.Dispose();mechanics.Abort();State=ReceiptAccountingState.Aborted;
        }
    }
}
#endif

using System;
using Ashfall.Core;
using Ashfall.Modules.Flight;
namespace Ashfall.Integration.FlightMotor
{
    // Concrete detached mechanics consumer. This does not certify physical output
    // or acknowledge a native owner. Only the receipt association supplies feedback.
    public sealed class NativeReceiptAccounting
    {
        readonly PreparedPlayerFlightStep step;
        bool folded, committed;
        public bool MechanicsCommitted {get{return committed;}}
        public NativeReceiptAccounting(PreparedPlayerFlightStep step)
        {if(step==null)throw new ArgumentNullException("step");this.step=step;}
        public RuleResult Validate(){return committed?RuleResult.Accept():step.Validate();}
        public RuleResult PrepareCollision(FlightCollisionResult feedback)
        {
            if(committed||folded)return RuleResult.Reject("receipt feedback already folded");
            var r=step.PrepareCollision(feedback);if(r.Accepted)folded=true;return r;
        }
        public void CommitValidated()
        {
            if(committed)return;
            if(!folded)throw new InvalidOperationException("receipt feedback missing");
            step.CommitValidated();committed=true;
        }
    }
}

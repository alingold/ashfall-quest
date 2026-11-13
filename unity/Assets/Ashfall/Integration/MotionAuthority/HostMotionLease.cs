using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
namespace Ashfall.Integration.MotionAuthority
{
    public enum MotionOwner { Locomotion, Capsule }
    public struct MotionBinding
    {
        public string Session,Actor;
        public long Life,Connection,Epoch,Generation;
        public bool Valid { get { return !String.IsNullOrEmpty(Session)&&!String.IsNullOrEmpty(Actor)&&Life>0&&Connection>0&&Epoch>=0&&Generation>0; } }
        public bool Same(MotionBinding b)
        {return Session==b.Session&&Actor==b.Actor&&Life==b.Life&&Connection==b.Connection&&Epoch==b.Epoch&&Generation==b.Generation;}
    }
    // All ports run on the coordinator's single serialized host lane. Pure stable
    // validation, no reentry; invalidation callbacks only revoke this lane's binding.
    public interface IMotionLeaseAuthorization
    { RuleResult Validate(MotionBinding binding,CommandContext context,long sequence,MotionOwner owner); }
    public interface IMotionInvalidation
    { event Action Invalidated; }
    public interface IMotionCollisionReceipts
    {
        RuleResult ValidateBinding(MotionBinding binding,long frame,long sequence);
        // Must verify exact physical and pivot phase outputs from the bound solver,
        // including partial moves. Never authorizes an invented velocity-only result.
        RuleResult ValidateResult(MotionBinding binding,long frame,long sequence,RootMotionProposal proposal,PlayerMotorResult result);
    }
    public sealed class PreparedMotionLease
    {
        internal readonly HostMotionLease Host;
        internal readonly long Revision;
        internal bool Done;
        public readonly MotionBinding Binding;
        public readonly CommandContext Context;
        public readonly long Sequence;
        public readonly MotionOwner Owner;
        internal PreparedMotionLease(HostMotionLease host,long revision,MotionBinding binding,CommandContext context,long sequence,MotionOwner owner)
        {Host=host;Revision=revision;Binding=binding;Context=context;Sequence=sequence;Owner=owner;}
        public RuleResult Validate(){return Host.Validate(this);}
        public void Abort(){Done=true;}
    }
    public sealed class HostMotionLease
    {
        readonly MotionBinding binding;readonly IMotionLeaseAuthorization authorization;
        long revision,lastFrame=-1,lastSequence=-1;
        bool invalid,executing;
        PreparedMotionLease active;
        public MotionOwner Owner {get;private set;}
        public bool Invalid {get{return invalid;}}
        public MotionBinding Binding {get{return binding;}}
        public HostMotionLease(MotionBinding binding,IMotionLeaseAuthorization authorization)
        {if(!binding.Valid||authorization==null)throw new ArgumentException("motion binding");this.binding=binding;this.authorization=authorization;}
        public RuleResult Prepare(MotionBinding requested,CommandContext c,long sequence,MotionOwner owner,out PreparedMotionLease plan)
        {
            plan=null;
            if(invalid||executing||active!=null||!binding.Same(requested)||!Enum.IsDefined(typeof(MotionOwner),owner)||
                !c.IsAuthority||c.SessionId!=binding.Session||c.ActorId!=binding.Actor||c.AuthorityEpoch!=binding.Epoch||String.IsNullOrEmpty(c.CommandId)||
                c.Tick<0||c.Tick<=lastFrame||sequence<=lastSequence)return RuleResult.Reject("motion binding/frame/owner");
            var r=authorization.Validate(binding,c,sequence,owner);if(!r.Accepted)return r;
            plan=new PreparedMotionLease(this,revision,binding,c,sequence,owner);return r;
        }
        internal RuleResult Validate(PreparedMotionLease p)
        {
            if(p==null||p.Host!=this||p.Done||invalid||executing||active!=null||p.Revision!=revision||!binding.Same(p.Binding)||
                p.Context.Tick<=lastFrame||p.Sequence<=lastSequence)return RuleResult.Reject("stale motion lease");
            return authorization.Validate(binding,p.Context,p.Sequence,p.Owner);
        }
        internal bool Commit(PreparedMotionLease p,out bool transition)
        {
            transition=false;if(!Validate(p).Accepted)return false;
            transition=Owner!=p.Owner;Owner=p.Owner;active=p;p.Done=true;revision++;
            lastFrame=p.Context.Tick;lastSequence=p.Sequence;return true;
        }
        internal bool Enter(PreparedMotionLease p)
        {if(invalid||executing||active!=p)return false;executing=true;return true;}
        internal bool IsActive(PreparedMotionLease p){return !invalid&&executing&&active==p;}
        internal void Finish(){executing=false;active=null;}
        public void Invalidate(){invalid=true;revision++;active=null;}
    }
}

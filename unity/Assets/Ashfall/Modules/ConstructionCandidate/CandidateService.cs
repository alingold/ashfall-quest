using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Construction;
namespace Ashfall.Modules.ConstructionCandidate
{
    internal sealed class CandidateDecision
    {
        public readonly bool Accepted;public readonly string Reason;public readonly int Cost;
        readonly Panel[] panels;public Panel[] Panels {get{return (Panel[])panels.Clone();}}
        internal CandidateDecision(string reason,List<Panel> plan,int cost){Reason=reason;Accepted=reason.Length==0;Cost=cost;panels=plan.ToArray();}
    }
    // Cost-only owner. Every mutation is computed on a private next state during Prepare.
    // No public legacy Place/Damage/Advance can bypass its reservation.
    public sealed class CandidateService
    {
        public const string ApiVersion="ConstructionCandidate/4";
        sealed class Pose:IConstructionActors
        {internal string Actor;internal BuilderState State;public bool TryRead(string actor,out BuilderState state){state=State;return actor==Actor&&state!=null;}}
        readonly string session;readonly long epoch;readonly IAuthorityGate authority;
        CandidateState state;PreparedChange pending;long revision;
        public long Revision {get{return revision;}}
        public CandidateService(string session,long epoch,IAuthorityGate authority,IConstructionWorld world,bool infiniteBuilds)
        {this.session=session;this.epoch=epoch;this.authority=authority;state=new CandidateState(session,epoch,authority,new Pose(),world,infiniteBuilds);}
        internal bool ReconcileNetworkPreview(PlacedPanel[] panels){if(pending!=null||panels==null||panels.Length>256)return false;state.ReconcileNetworkPreview(panels);revision++;return true;}
        public PlacedPanel[] Snapshot(){return state.Snapshot();}
        public sealed class PreparedChange
        {
            readonly CandidateService owner;readonly CandidateState next;readonly long revision;
            readonly PlacedPanel[] panels;readonly int cost;bool committed,aborted;
            internal PreparedChange(CandidateService owner,CandidateState next,int cost){this.owner=owner;this.next=next;revision=owner.revision;panels=next.Snapshot();this.cost=cost;}
            public PlacedPanel[] Panels {get{return (PlacedPanel[])panels.Clone();}}
            public int Cost {get{return cost;}}
            public bool Validate(){return !aborted&&!committed&&Object.ReferenceEquals(owner.pending,this)&&owner.revision==revision;}
            // Only after validate-all on the shared host lane. Repeated commit is inert.
            public void CommitValidated(){if(committed||aborted)return;owner.state=next;owner.revision=revision+1;owner.pending=null;committed=true;}
            public void Abort(){if(committed||aborted)return;aborted=true;if(Object.ReferenceEquals(owner.pending,this))owner.pending=null;}
        }
        bool Available(){return pending==null&&revision<long.MaxValue;}
        RuleResult Reserve(CandidateState next,int cost,out PreparedChange prepared)
        {prepared=new PreparedChange(this,next,cost);pending=prepared;return RuleResult.Accept();}
        public RuleResult PreparePlace(ConstructionCommand command,BuilderState pose,out PreparedChange prepared)
        {
            prepared=null;if(!Available())return RuleResult.Reject("structure_busy/exhausted");
            var next=state.Copy(new Pose{Actor=command.Context.ActorId,State=pose});var decision=next.Place(command);
            if(!decision.Accepted)return RuleResult.Reject(decision.Reason);return Reserve(next,decision.Cost,out prepared);
        }
        public PlacedPanel DismantleTarget(string actor,BuilderState pose){return state.DismantleTarget(actor,pose);}
        public RuleResult PrepareDismantle(ConstructionCommand command,BuilderState pose,out PreparedChange prepared)
        {
            prepared=null;if(!Available())return RuleResult.Reject("structure_busy/exhausted");
            var next=state.Copy(new Pose{Actor=command.Context.ActorId,State=pose});
            var target=next.DismantleTarget(command.Context.ActorId,pose);if(target==null)return RuleResult.Reject("dismantle_target");
            var result=next.Dismantle(command,target.Id);if(!result.Accepted)return result;
            return Reserve(next,next.InfiniteBuilds?0:-(target.Shape.Cost/2),out prepared);
        }
        public RuleResult PrepareDamage(CommandContext context,string piece,float amount,long expectedRevision,out PreparedChange prepared)
        {
            prepared=null;if(!Available()||expectedRevision!=revision)return RuleResult.Reject("structure_revision/busy");
            var next=state.Copy(new Pose());var result=next.Damage(context,piece,amount);
            if(!result.Accepted)return result;return Reserve(next,0,out prepared);
        }
        public RuleResult PrepareAdvance(float seconds,bool paused,out PreparedChange prepared)
        {
            prepared=null;if(!Available()||Single.IsNaN(seconds)||Single.IsInfinity(seconds)||seconds<0)return RuleResult.Reject("advance");
            var next=state.Copy(new Pose());next.Advance(seconds,paused);return Reserve(next,0,out prepared);
        }
        public bool Authorize(CommandContext context)
        {return context.IsAuthority&&context.SessionId==session&&context.AuthorityEpoch==epoch&&authority.Validate(context).Accepted;}
    }
}

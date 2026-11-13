using System;
using Ashfall.Core;
namespace Ashfall.Modules.WeaponHandling
{
    public sealed partial class WeaponLifetimes
    {
        public RuleResult PrepareTransfer(CommandContext c,string instanceId,string targetActor,long targetEpoch,out IPreparedWeaponTransfer transfer)
        {
            transfer=null;Entry e;
            if(!Valid(c,instanceId)||String.IsNullOrEmpty(targetActor)||targetEpoch<c.AuthorityEpoch||
                !entries.TryGetValue(instanceId,out e)||e.Active==null||e.Actor!=c.ActorId||e.Epoch!=c.AuthorityEpoch)
                return RuleResult.Reject("invalid prepared ownership");
            if(e.Active.FireReservation!=null)return RuleResult.Reject("prepared operation busy");
            RuleResult r=e.Active.ValidatePreparation(c);if(!r.Accepted)return r;
            var target=new CommandContext(session,targetActor,c.CommandId,c.Tick,targetEpoch,true);
            r=e.Active.AuthorizeRestore(target);if(!r.Accepted)return r;
            r=approval.AuthorizeRestore(target,instanceId,e.Actor,e.Epoch);if(!r.Accepted)return r;
            IRevisionedAmmoReserve reserve=e.Active.VersionedReserve;
            if(reserve==null)return RuleResult.Reject("prepared transfer requires revisioned reserve");
            var token=new Prepared(this,e,c,target,instanceId,reserve);
            if(!e.Active.ReserveOperation(token,long.MaxValue,c.Tick))return RuleResult.Reject("prepared operation busy");
            transfer=token;return RuleResult.Accept();
        }
        sealed class Prepared : IPreparedWeaponTransfer
        {
            readonly WeaponLifetimes ledger;readonly Entry entry;readonly WeaponHandling original,replacement;
            readonly CommandContext source,target;readonly string id;readonly long version,reserveVersion;
            readonly IRevisionedAmmoReserve reserve;readonly PreparedWeaponResult result;
            bool validated,committed,aborted;
            internal Prepared(WeaponLifetimes ledger,Entry entry,CommandContext source,CommandContext target,string id,IRevisionedAmmoReserve reserve)
            {
                this.ledger=ledger;this.entry=entry;this.source=source;this.target=target;this.id=id;this.reserve=reserve;
                original=entry.Active;version=original.Revision;reserveVersion=reserve.Revision;
                replacement=original.Resume(target);result=new PreparedWeaponResult(id,target.ActorId,target.AuthorityEpoch,replacement);
            }
            public PreparedWeaponResult Result {get{return committed?result:null;}}
            public RuleResult Validate()
            {
                validated=false;
                if(aborted||committed||original.FireReservation!=this||entry.Active!=original||!original.Live||original.Revision!=version||reserve.Revision!=reserveVersion||entry.Actor!=source.ActorId||entry.Epoch!=source.AuthorityEpoch)
                    return RuleResult.Reject("stale prepared transfer");
                RuleResult r=original.ValidatePreparation(source);if(!r.Accepted)return r;
                r=original.AuthorizeRestore(target);if(!r.Accepted)return r;
                r=ledger.approval.AuthorizeRestore(target,id,entry.Actor,entry.Epoch);if(!r.Accepted)return r;
                validated=true;return RuleResult.Accept();
            }
            // Final Validate-all and this publication MUST share the host lane.
            // No authority/reserve calls, allocations or fallible external work here.
            public void CommitValidated()
            {
                if(!validated||committed||aborted||original.FireReservation!=this||entry.Active!=original||original.Revision!=version)return;
                original.RetirePrepared();original.ReleaseOperation(this);entry.Active=replacement;entry.Actor=target.ActorId;entry.Epoch=target.AuthorityEpoch;entry.Pending=null;committed=true;
            }
            public void Abort(){if(!committed){aborted=true;validated=false;original.ReleaseOperation(this);}}
        }
    }
}

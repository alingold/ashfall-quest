using System;
using Ashfall.Core;
namespace Ashfall.Modules.WeaponHandling
{
 public sealed partial class WeaponLifetimes
 {
  // Trusted lifecycle lane calls this BEFORE closing its admission gate. It
  // cancels the actual registered capability, including retained validated refs.
  // This local cancellation neither certifies global drain nor changes ammo.
  public RuleResult CancelSourceDisposableSelection(CommandContext host,string instanceId)
  {
   Entry e;
   if(!Valid(host,instanceId)||!entries.TryGetValue(instanceId,out e)||e.Active==null||
    e.Actor!=host.ActorId||e.Epoch!=host.AuthorityEpoch)return RuleResult.Reject("wrong cancellation owner");
   var r=e.Active.ValidatePreparation(host);if(!r.Accepted)return r;
   var p=e.Active.FireReservation as SourcePreparation;
   if(p==null||p.disposition==null||!p.BelongsTo(this))return RuleResult.Reject("no source disposition reservation");
   p.disposition.Abort();return RuleResult.Accept();
  }
  // Concrete capability: only this registry's private, still-reserved launch qualifies.
  public RuleResult PrepareSourceDisposableSelection(IPreparedSourceLaunch launch,string successor,
   long successorEquip,IWeaponFireBinding successorBinding,out SourceDispositionReservation token)
  {
   token=null;var p=launch as SourcePreparation;Entry entry=null;
   if(p==null||p.Descriptor==null||p.disposition!=null||!p.Current()||p.next.Revision==long.MaxValue||p.Descriptor.ExpiresTick==long.MaxValue||
    !p.BelongsTo(this)||
    (p.Descriptor.Before.Profile!="frag"&&p.Descriptor.Before.Profile!="zone")||
    p.Descriptor.Before.Loaded!=1||p.Descriptor.After.Loaded!=0||p.Descriptor.After.Reloads!=0||successor==null||successor==p.Descriptor.InstanceId)
    return RuleResult.Reject("not actual exhausted source reservation");
   if(successor.Length>0&&(!entries.TryGetValue(successor,out entry)||entry.Active==null||
    entry.Actor!=p.c.ActorId||entry.Epoch!=p.c.AuthorityEpoch||successorEquip<0||successorBinding==null||
    entry.Active.Revision==long.MaxValue||entry.Active.VersionedReserve==null||!entry.Active.Live))return RuleResult.Reject("successor owner unavailable");
   var n=new SourceDispositionReservation(p,entry,successor,successorEquip,successorBinding);
   if(entry!=null&&!entry.Active.ReserveOperation(n,p.expires,p.c.Tick))return RuleResult.Reject("successor busy");
   p.disposition=n;token=n;return RuleResult.Accept();
  }
  public sealed class SourceDispositionReservation
  {
   public const string ApiVersion="SourceDisposableDisposition/1";
   readonly SourcePreparation launch;readonly Entry entry;readonly WeaponHandling successor,next,exhausted;
   readonly long revision,reserveRevision,equip;readonly string selected;readonly IWeaponFireBinding binding;
   bool validated,done,aborted;
   internal SourceDispositionReservation(IPreparedSourceLaunch source,object ownerEntry,string id,long equip,IWeaponFireBinding binding)
   {var p=(SourcePreparation)source;var e=(Entry)ownerEntry;launch=p;entry=e;selected=id;this.equip=equip;this.binding=binding;exhausted=p.next.SuspendedCandidate();
    if(e!=null){successor=e.Active;revision=successor.Revision;reserveRevision=successor.VersionedReserve.Revision;next=successor.SuspendedCandidate();}}
   public SourceLaunchDescriptor Launch {get{return launch.Descriptor;}}
   public string SelectedInstanceId {get{return selected;}}
   public bool Committed {get{return done;}}
   public bool ReadyToCommit {get{return validated&&!done&&!aborted&&Current();}}
   bool Current(){return launch.disposition==this&&launch.Current()&&
    (entry==null||(entry.Active==successor&&entry.Actor==Launch.Command.ActorId&&entry.Epoch==Launch.Command.AuthorityEpoch&&
     successor.Live&&successor.Revision==revision&&successor.VersionedReserve.Revision==reserveRevision&&successor.FireReservation==this));}
   public RuleResult Validate(long now)
   {
    validated=false;if(now>Launch.ExpiresTick){Abort();return RuleResult.Reject("expired disposition");}
    if(aborted||done||!Current())return RuleResult.Reject("stale disposition");
    var r=launch.Validate(now);
    if(r.Accepted&&successor!=null)r=successor.ValidatePreparation(Launch.Command);
    if(r.Accepted&&successor!=null)r=binding.Validate(Launch.Command,selected,equip,Launch.LifeGeneration,Launch.ConnectionGeneration,Launch.After.Frame);
    if(!r.Accepted||!Current())return r.Accepted?RuleResult.Reject("changed disposition"):r;
    validated=true;return RuleResult.Accept();
   }
   // Shared exclusive host lane: all validation precedes these owner assignments.
   public void CommitValidated()
   {
    if(!ReadyToCommit)return;
    launch.original.PublishFireState(exhausted);
    if(successor!=null){successor.PublishFireState(next);successor.ReleaseOperation(this);}
    launch.original.ReleaseOperation(launch);launch.committed=true;done=true;
   }
   public void Abort(){if(done||aborted)return;aborted=true;validated=false;
    if(successor!=null)successor.ReleaseOperation(this);launch.Abort();}
  }
 }
}

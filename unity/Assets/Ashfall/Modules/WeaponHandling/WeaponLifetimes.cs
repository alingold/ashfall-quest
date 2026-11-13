using System;
using System.Collections.Generic;
using Ashfall.Core;
namespace Ashfall.Modules.WeaponHandling
{
    // Host-owned, session-lifetime service. Keep one ledger across equip/drop/reconnect;
    // persist the ledger/tombstones centrally before permitting process recovery.
    // This in-process opaque receipt is deliberately not a client-save DTO.
    public interface IWeaponLifetimeAuthority
    {
        RuleResult AuthorizeNew(CommandContext context, string instanceId, WeaponMechanism mechanism, int initialRounds);
        RuleResult AuthorizeRestore(CommandContext context, string instanceId, string previousActor, long previousEpoch);
    }
    public sealed class WeaponTransfer
    {
        internal readonly WeaponHandling Frozen;
        internal readonly WeaponLifetimes Ledger;
        public string SessionId { get; private set; }
        public string InstanceId { get; private set; }
        public string PreviousActor { get; private set; }
        public long PreviousEpoch { get; private set; }
        public HandlingSnapshot Snapshot { get { return Frozen.Snapshot; } }
        internal WeaponTransfer(WeaponLifetimes ledger, WeaponHandling frozen, string session, string instance, string actor, long epoch)
        { Ledger=ledger; Frozen=frozen; SessionId=session; InstanceId=instance; PreviousActor=actor; PreviousEpoch=epoch; }
    }
    public sealed partial class WeaponLifetimes
    {
        sealed class Entry { public WeaponHandling Active; public WeaponTransfer Pending; public string Actor; public long Epoch; }
        readonly string session;
        readonly IWeaponLifetimeAuthority approval;
        readonly Dictionary<string,Entry> entries = new Dictionary<string,Entry>(StringComparer.Ordinal);
        public WeaponLifetimes(string sessionId, IWeaponLifetimeAuthority approval)
        { if(String.IsNullOrEmpty(sessionId)||approval==null) throw new ArgumentException("Session and lifetime authority required"); session=sessionId; this.approval=approval; }
        bool Valid(CommandContext c, string id)
        { return c.IsAuthority && c.SessionId==session && !String.IsNullOrEmpty(c.ActorId) && !String.IsNullOrEmpty(c.CommandId) && c.Tick>=0 && c.AuthorityEpoch>=0 && !String.IsNullOrEmpty(id); }
        public RuleResult CreateNew(CommandContext c, string instanceId, WeaponMechanism mechanism, int initialRounds,
            IAmmoReserve reserve, IAuthorityGate authority, out WeaponHandling weapon)
        {
            weapon=null;
            int capacity=WeaponHandling.MagazineCapacity(mechanism);
            if(!Valid(c,instanceId)||entries.ContainsKey(instanceId)||!Enum.IsDefined(typeof(WeaponMechanism),mechanism)||initialRounds<0||initialRounds>capacity||reserve==null||authority==null) return RuleResult.Reject("invalid or existing lifetime");
            RuleResult r=authority.Validate(c); if(!r.Accepted) return r;
            r=approval.AuthorizeNew(c,instanceId,mechanism,initialRounds); if(!r.Accepted) return r;
            weapon=new WeaponHandling(session,c.ActorId,c.AuthorityEpoch,mechanism,reserve,authority);
            weapon.InitializeMagazine(initialRounds);
            weapon.RecordEnvelope(c);
            entries.Add(instanceId,new Entry { Active=weapon, Actor=c.ActorId, Epoch=c.AuthorityEpoch });
            return RuleResult.Accept();
        }
        public RuleResult Capture(CommandContext c, string instanceId, out WeaponTransfer transfer)
        {
            transfer=null; Entry e;
            if(!Valid(c,instanceId)||!entries.TryGetValue(instanceId,out e)||e.Active==null||e.Actor!=c.ActorId||e.Epoch!=c.AuthorityEpoch) return RuleResult.Reject("inactive lifetime");
            if(e.Active.FireReservation!=null)return RuleResult.Reject("prepared operation busy");
            RuleResult r=e.Active.AuthorizeCapture(c); if(!r.Accepted) return r;
            if(e.Active.FireReservation!=null)return RuleResult.Reject("prepared operation busy");
            transfer=new WeaponTransfer(this,e.Active.Freeze(),session,instanceId,e.Actor,e.Epoch);
            e.Pending=transfer; e.Active=null;
            return RuleResult.Accept();
        }
        public RuleResult Restore(CommandContext c, string instanceId, WeaponTransfer transfer, out WeaponHandling weapon)
        {
            weapon=null; Entry e;
            if(!Valid(c,instanceId)||transfer==null||transfer.Ledger!=this||transfer.InstanceId!=instanceId||transfer.SessionId!=session||!entries.TryGetValue(instanceId,out e)||e.Pending!=transfer||e.Active!=null||c.AuthorityEpoch<e.Epoch) return RuleResult.Reject("invalid, stale or consumed transfer");
            RuleResult r=transfer.Frozen.AuthorizeRestore(c); if(!r.Accepted) return r;
            r=approval.AuthorizeRestore(c,instanceId,e.Actor,e.Epoch); if(!r.Accepted) return r;
            weapon=transfer.Frozen.Resume(c);
            e.Actor=c.ActorId; e.Epoch=c.AuthorityEpoch; e.Active=weapon; e.Pending=null;
            return RuleResult.Accept();
        }
    }
}

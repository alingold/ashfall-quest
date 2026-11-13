using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.InventoryItems;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration.InventoryTransfers;
namespace Ashfall.Integration.WorldWeaponTransfers
{
    // In-process candidate. Root owns and shares this lane with all module mutation.
    // Restart is forbidden: no cold mechanical/replay restoration is claimed.
    public sealed partial class WorldWeaponCoordinator
    {
        readonly object lane;readonly SessionWeapons weapons;readonly IWeaponHostClock clock;readonly IWeaponGeometry geometry;readonly long maxLease;
        Dictionary<string,WorldWeaponRecord> world=new Dictionary<string,WorldWeaponRecord>(StringComparer.Ordinal);
        readonly WeaponTransferJournal journal;bool recoveryRequired;long intent;
        public bool RecoveryRequired {get{lock(lane)return recoveryRequired;}}
        List<WeaponWorldEvent> pending=new List<WeaponWorldEvent>();long sequence;Operation outstanding;
        public WorldWeaponCoordinator(object hostLane,SessionWeapons weapons,IWeaponHostClock clock,IWeaponGeometry geometry,long maximumLeaseTicks)
        {if(hostLane==null||weapons==null||clock==null||geometry==null||maximumLeaseTicks<=0)throw new ArgumentException();lane=hostLane;this.weapons=weapons;this.clock=clock;this.geometry=geometry;maxLease=maximumLeaseTicks;}
        public WorldWeaponCoordinator(object hostLane,SessionWeapons weapons,IWeaponHostClock clock,IWeaponGeometry geometry,long maximumLeaseTicks,WeaponTransferJournal journal)
            :this(hostLane,weapons,clock,geometry,maximumLeaseTicks)
        {if(journal==null)throw new ArgumentNullException("journal");if(journal.HasHistory||journal.Faulted||!journal.Claim())throw new InvalidOperationException("Cold module restoration unavailable; retained journal requires recovery, never reopen empty");this.journal=journal;}
        internal bool OwnsLane(object candidate){return Object.ReferenceEquals(lane,candidate);}
        void Expire(){if(outstanding!=null&&clock.Tick>=outstanding.Expiry)outstanding.Abort();}
        public WorldWeaponRecord[] Records{get{lock(lane){var a=new WorldWeaponRecord[world.Count];world.Values.CopyTo(a,0);return a;}}}
        public WeaponWorldEvent[] Pending{get{lock(lane)return pending.ToArray();}}
        public bool Ack(long seq){lock(lane){Expire();if(recoveryRequired||outstanding!=null)return false;
            int index=pending.FindIndex(delegate(WeaponWorldEvent e){return e.Sequence==seq;});if(index<0)return false;
            if(journal!=null){try{journal.Append(WeaponTransferJournal.Marker(WeaponJournalStage.Ack,seq));}catch{recoveryRequired=true;return false;}}
            pending.RemoveAt(index);return true;}}
        public bool PrepareDrop(CommandContext c,IWeaponPrincipal principal,long deadline,Inventory inventory,string[] wholeSlot,out Operation token)
        {return Prepare(c,principal,deadline,inventory,wholeSlot,null,out token);}
        public bool PrepareCollect(CommandContext c,IWeaponPrincipal principal,long deadline,Inventory inventory,WorldWeaponRecord[] exactRecords,out Operation token)
        {return Prepare(c,principal,deadline,inventory,null,exactRecords,out token);}
        bool Prepare(CommandContext c,IWeaponPrincipal p,long deadline,Inventory inventory,string[] dropIds,WorldWeaponRecord[] collect,out Operation token)
        {
            lock(lane){token=null;Expire();long now=clock.Tick;
                if(recoveryRequired||intent==long.MaxValue||outstanding!=null||inventory==null||p==null||!c.IsAuthority||String.IsNullOrEmpty(p.CanonicalActor)||p.CanonicalActor!=c.ActorId||p.Life<0||p.ConnectionLife<0||now<0||c.Tick!=now||deadline<=now||deadline-now>maxLease||sequence==long.MaxValue||pending.Count>=256||!p.Validate(c,deadline))return false;
                bool drop=dropIds!=null;if((drop&&dropIds.Length==0)||(!drop&&(collect==null||collect.Length==0)))return false;
                var moving=new HashSet<string>(StringComparer.Ordinal);var members=new List<ItemInstance>();
                if(drop){var owned=inventory.Items;foreach(string id in dropIds){ItemInstance item=Array.Find(owned,delegate(ItemInstance i){return i.InstanceId==id;});if(item==null||world.ContainsKey(id)||!moving.Add(id))return false;members.Add(item);}}
                else foreach(var record in collect){WorldWeaponRecord actual;if(record==null||!world.TryGetValue(record.Item.InstanceId,out actual)||!Object.ReferenceEquals(actual,record)||now>=record.Deadline||!moving.Add(record.Item.InstanceId))return false;members.Add(record.Item);}
                long effectiveDeadline=deadline;if(!drop)foreach(var record in collect)effectiveDeadline=Math.Min(effectiveDeadline,record.Deadline);
                var op=new Operation(this,c,p,effectiveDeadline);bool success=false;
                try {
                    if(!inventory.PrepareWeaponHandoff(c,drop?dropIds:new string[0],drop?new ItemInstance[0]:members.ToArray(),out op.inventory))return false;
                    if(!geometry.Prepare(c,p,deadline,drop?members.ToArray():new ItemInstance[0],drop?new WorldWeaponRecord[0]:(WorldWeaponRecord[])collect.Clone(),out op.geometry)||op.geometry==null)return false;
                    var placements=op.geometry.Placements;if(placements==null||placements.Length!=(drop?members.Count:0))return false;
                    var after=new Dictionary<string,WorldWeaponRecord>(world,StringComparer.Ordinal);var result=new List<WorldWeaponRecord>();
                    var bodies=new HashSet<string>(StringComparer.Ordinal);foreach(var existing in world.Values)bodies.Add(existing.Placement.BodyId);
                    for(int n=0;n<members.Count;n++){
                        var item=members[n];string actor;long epoch;HandlingSnapshot snapshot;int reserve;long reserveRevision;
                        if(!weapons.WorldState(item,!drop,out actor,out epoch,out snapshot,out reserve,out reserveRevision))return false;
                        if(!drop&&(collect[n].ReserveRevision!=reserveRevision||collect[n].Reserve!=reserve))return false;
                        if(drop&&(actor!=c.ActorId||epoch!=c.AuthorityEpoch))return false;
                        var source=new CommandContext(c.SessionId,actor,c.CommandId,c.Tick,epoch,true);IPreparedWeaponTransfer transfer;
                        if(!weapons.PrepareWorldMove(source,item,c.ActorId,c.AuthorityEpoch,drop,out transfer).Accepted)return false;op.transfers.Add(transfer);
                        if(drop){var place=placements[n];if(place==null||place.InstanceId!=item.InstanceId||!bodies.Add(place.BodyId))return false;
                            var record=new WorldWeaponRecord(item,place,snapshot,reserve,c,p,deadline,sequence+1,reserveRevision);after.Add(item.InstanceId,record);result.Add(record);}
                        else {after.Remove(item.InstanceId);result.Add(collect[n]);}
                    }
                    if(!weapons.PrepareWorldSuspensions(c.ActorId,moving,out op.suspensions))return false;
                    op.after=after;op.events=new List<WeaponWorldEvent>(pending);op.events.Add(new WeaponWorldEvent(sequence+1,drop,result.ToArray()));
                    if(journal!=null){
                        var bytes=WeaponTransferJournal.Prepare(intent+1,c,p,deadline,op.events[op.events.Count-1],inventory);
                        // Reserve Decision + Published/Aborted + ACK space before retirement.
                        try{if(!journal.CanAppend(bytes.Length+3*45))return false;journal.Append(bytes);op.journaled=true;op.intent=++intent;}catch{recoveryRequired=true;return false;}
                    }
                    outstanding=op;token=op;success=true;return true;
                }finally{if(!success)op.Abort();}
            }
        }
        public sealed class Operation
        {
            readonly WorldWeaponCoordinator owner;readonly CommandContext context;readonly IWeaponPrincipal principal;readonly long deadline,life,connection;
            internal Inventory.PreparedWeaponHandoff inventory;internal IWeaponGeometryLease geometry;
            internal readonly List<IPreparedWeaponTransfer> transfers=new List<IPreparedWeaponTransfer>();internal List<IPreparedWeaponSuspension> suspensions=new List<IPreparedWeaponSuspension>();
            internal Dictionary<string,WorldWeaponRecord> after;internal List<WeaponWorldEvent> events;bool done,aborted;internal bool journaled;internal long intent;internal long Expiry {get{return deadline;}}
            internal Operation(WorldWeaponCoordinator o,CommandContext c,IWeaponPrincipal p,long d){owner=o;context=c;principal=p;deadline=d;life=p.Life;connection=p.ConnectionLife;}
            public bool TryCommit(){lock(owner.lane){if(done)return true;if(aborted||owner.recoveryRequired||owner.outstanding!=this)return false;
                // Durable decision precedes final validation. Failure/uncertain write
                // quarantines this coordinator; the original owners are still intact.
                if(journaled){try{owner.journal.Append(WeaponTransferJournal.Marker(WeaponJournalStage.Decision,intent));}catch{owner.recoveryRequired=true;Abort();return false;}}
                if(owner.clock.Tick<context.Tick||owner.clock.Tick>=deadline||principal.CanonicalActor!=context.ActorId||principal.Life!=life||principal.ConnectionLife!=connection||!principal.Validate(context,deadline)||!inventory.Validate()||!geometry.Validate()){Abort();return false;}
                foreach(var t in transfers)if(!t.Validate().Accepted){Abort();return false;}
                foreach(var t in suspensions)if(!t.Validate().Accepted){Abort();return false;}
                if(owner.clock.Tick>=deadline){Abort();return false;}
                // Stable trusted lane: publication through sequence assignment has no IO, callbacks or fallible authorization.
                foreach(var t in transfers)t.CommitValidated();foreach(var t in suspensions)t.CommitValidated();geometry.CommitValidated();inventory.CommitValidated();owner.weapons.PublishWorldHolster(context.ActorId);
                owner.world=after;owner.pending=events;owner.sequence++;done=true;owner.outstanding=null;
                if(journaled){try{owner.journal.Append(WeaponTransferJournal.Marker(WeaponJournalStage.Published,intent));}catch{owner.recoveryRequired=true;}}
                return true;
            }}
            public void Abort(){lock(owner.lane){if(done||aborted)return;aborted=true;foreach(var t in transfers)t.Abort();foreach(var t in suspensions)t.Abort();if(geometry!=null)geometry.Abort();if(inventory!=null)inventory.Abort();if(owner.outstanding==this)owner.outstanding=null;
                if(journaled&&!owner.recoveryRequired){try{owner.journal.Append(WeaponTransferJournal.Marker(WeaponJournalStage.Aborted,intent));}catch{owner.recoveryRequired=true;}}}}
        }
    }
}


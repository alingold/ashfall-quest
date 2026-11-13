using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.InventoryItems;
using Ashfall.Modules.WeaponHandling;
using System.IO;
using Ashfall.RecoveryContracts;
using Wire = Ashfall.RecoveryContracts.RecoveryWire;
namespace Ashfall.Integration.InventoryTransfers
{
    // One registry for the entire live session. No implicit creation on draw/restore.
    // Grant and migration are explicit host lifecycle operations, NOT transactions.
    public sealed partial class SessionWeapons
    {
        sealed class Entry {public bool World;public WeaponHandling Handler;public string Actor;public string Definition;public int Stars;public long Epoch;public WeaponTransfer Pending;}
        sealed class Draw {public string Id;public bool Holstered,Released;public long Ready,Start,LastTick=-1,Sequence=-1;}
        readonly WeaponLifetimes lifetimes;readonly IAuthorityGate gate; readonly int hz;
        readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>();
        readonly Dictionary<string,Draw> draws=new Dictionary<string,Draw>();
        object actionLane;Func<string,string,bool> actionOwner;
        internal bool BindActionOwner(object lane,Func<string,string,bool> owner)
        {if(actionLane!=null||lane==null||owner==null)return false;actionLane=lane;actionOwner=owner;return true;}
        public SessionWeapons(string session,int tickRate,IWeaponLifetimeAuthority approval,IAuthorityGate gate)
        {if(tickRate<1||gate==null)throw new ArgumentException();lifetimes=new WeaponLifetimes(session,approval);this.gate=gate;hz=tickRate;}
        public RuleResult Grant(CommandContext c,ItemInstance item,WeaponMechanism mechanism,int rounds,IAmmoReserve reserve)
        {
            if(item==null||!((item.DefinitionId=="longwatch"&&item.Stars==1&&mechanism==WeaponMechanism.PrecisionRifle)||
                (item.DefinitionId=="cairn6"&&item.Stars>=1&&item.Stars<=5&&mechanism==WeaponMechanism.MagnumRevolver)))return RuleResult.Reject("unsupported-profile");
            string id=item.InstanceId;WeaponHandling handler;RuleResult r=lifetimes.CreateNew(c,id,mechanism,rounds,reserve,gate,out handler);
            if(r.Accepted)entries.Add(id,new Entry{Handler=handler,Actor=c.ActorId,Epoch=c.AuthorityEpoch,Definition=item.DefinitionId,Stars=item.Stars});return r;
        }
        public bool IsActive(ItemInstance item,string actor,long epoch) {Entry e;return item!=null&&entries.TryGetValue(item.InstanceId,out e)&&e.Handler!=null&&!e.World&&e.Actor==actor&&e.Epoch==epoch&&e.Definition==item.DefinitionId&&e.Stars==item.Stars;}
        public void Selection(InventorySnapshot snapshot,long tick)
        { Selection(snapshot,tick,false,false); }
        public void Selection(InventorySnapshot snapshot,long tick,bool suppressed,bool forceFreshDraw)
        {
            Draw d;
            if(!draws.TryGetValue(snapshot.ActorId,out d)){d=new Draw{Id="",Holstered=true};draws.Add(snapshot.ActorId,d);}
            bool holstered=snapshot.Holstered||suppressed;
            if(forceFreshDraw||d.Id!=snapshot.SelectedInstanceId||d.Holstered!=holstered) {
                SuspendActor(snapshot.ActorId);
                d.Id=snapshot.SelectedInstanceId;d.Holstered=holstered;d.Released=false;d.Start=tick;d.Ready=tick+(long)Math.Ceiling(.25*hz);
            }
        }
        public RuleResult Apply(InventorySnapshot snapshot,CommandContext c,string id,HandlingAction action,HandlingInput input)
        {
            if(actionLane==null)return RuleResult.Reject("unbound-action-owner");
            lock(actionLane){if(!actionOwner(c.ActorId,id))return RuleResult.Reject("not-action-owner");return ApplyOwned(snapshot,c,id,action,input);}
        }
        RuleResult ApplyOwned(InventorySnapshot snapshot,CommandContext c,string id,HandlingAction action,HandlingInput input)
        {
            Entry e;Draw d;
            if(snapshot.SessionId!=c.SessionId||snapshot.ActorId!=c.ActorId||snapshot.Epoch!=c.AuthorityEpoch||snapshot.Holstered||snapshot.SelectedInstanceId!=id||
                !entries.TryGetValue(id,out e)||e.World||e.Handler==null||e.Actor!=c.ActorId||e.Epoch!=c.AuthorityEpoch||!draws.TryGetValue(c.ActorId,out d)||d.Id!=id||d.Holstered)
                return RuleResult.Reject("not-selected-lifetime");
            RuleResult allowed=gate.Validate(c);if(!allowed.Accepted)return allowed;
            if(c.Tick<d.Start||c.Tick<d.LastTick||input.Sequence<=d.Sequence)return RuleResult.Reject("stale-draw-input");
            d.LastTick=c.Tick;d.Sequence=input.Sequence;
            if(!input.Trigger&&input.Enabled&&input.PrimaryTracked&&!input.Paused&&!input.PrimaryClimbing&&!input.HeadObstructed)d.Released=true;
            if(action!=HandlingAction.Sample&&(!d.Released||c.Tick<d.Ready))return RuleResult.Reject("draw-delay-or-fresh-trigger");
            if(c.Tick<d.Ready){if(action==HandlingAction.Sample)e.Handler.Apply(c,action,input);return RuleResult.Reject("drawing");}
            return e.Handler.Apply(c,action,input);
        }
        public HandlingSnapshot Inspect(string id) {Entry e=entries[id];return e.Handler!=null?e.Handler.Snapshot:e.Pending.Snapshot;}
        public RuleResult Capture(CommandContext c,string id)
        {
            Entry e;if(!entries.TryGetValue(id,out e)||e.World)return RuleResult.Reject("missing-instance-or-world-custody");
            WeaponTransfer transfer;RuleResult r=lifetimes.Capture(c,id,out transfer);
            if(r.Accepted){e.Handler=null;e.Pending=transfer;}return r;
        }
        public RuleResult Restore(CommandContext c,string id)
        {
            Entry e;if(!entries.TryGetValue(id,out e)||e.World)return RuleResult.Reject("missing-instance-or-world-custody");
            WeaponHandling handler;RuleResult r=lifetimes.Restore(c,id,e.Pending,out handler);
            if(r.Accepted){e.Handler=handler;e.Pending=null;e.Actor=c.ActorId;e.Epoch=c.AuthorityEpoch;draws.Remove(c.ActorId);}return r;
        }
        SessionWeapons(WeaponLifetimes restored,int tickRate,IAuthorityGate authority)
        {lifetimes=restored;hz=tickRate;gate=authority;}
        public bool TryReadCustodyReserve(string instanceId,out int available,out long revision)
        {
            available=0;revision=0;Entry entry;
            return instanceId!=null&&entries.TryGetValue(instanceId,out entry)&&entry.Handler!=null&&entry.Handler.TryReadReserve(out available,out revision);
        }

        // Local capture on root's already frozen host lane; no global freeze receipt.
        // Root authenticates the enclosing image and binds actual Inventories on adoption.
        static void ValidateCheckpointLayout()
        {
            Wire.Fields(typeof(SessionWeapons),"actionLane:System.Object\nactionOwner:System.Func`3[System.String,System.String,System.Boolean]\ndraws:System.Collections.Generic.Dictionary`2[System.String,Ashfall.Integration.InventoryTransfers.SessionWeapons+Draw]\nentries:System.Collections.Generic.Dictionary`2[System.String,Ashfall.Integration.InventoryTransfers.SessionWeapons+Entry]\ngate:Ashfall.Core.IAuthorityGate\nhz:System.Int32\nlifetimes:Ashfall.Modules.WeaponHandling.WeaponLifetimes");
            Wire.Fields(typeof(Entry),"Actor:System.String\nDefinition:System.String\nEpoch:System.Int64\nHandler:Ashfall.Modules.WeaponHandling.WeaponHandling\nPending:Ashfall.Modules.WeaponHandling.WeaponTransfer\nStars:System.Int32\nWorld:System.Boolean");
            Wire.Fields(typeof(Draw),"Holstered:System.Boolean\nId:System.String\nLastTick:System.Int64\nReady:System.Int64\nReleased:System.Boolean\nSequence:System.Int64\nStart:System.Int64");
        }

        public byte[] CaptureCustodyCheckpoint(CheckpointBinding binding,LifetimeCheckpointIdentity[] identities)
        {
            Wire.Require(binding!=null&&entries.Count<=64&&draws.Count<=64);
            ValidateCheckpointLayout();
            using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){
                w.Write(0x31535741);w.Write(1);binding.Write(w);w.Write(hz);
                Wire.Blob(w,lifetimes.CaptureRegistryCheckpoint(binding,identities));w.Write(entries.Count);
                foreach(var pair in entries){var e=pair.Value;WeaponHandling active;WeaponTransfer pending;string actor;long epoch;
                    Wire.Require(lifetimes.TryReadCheckpointMember(pair.Key,out active,out pending,out actor,out epoch)&&Object.ReferenceEquals(active,e.Handler)&&Object.ReferenceEquals(pending,e.Pending)&&actor==e.Actor&&epoch==e.Epoch);
                    Wire.Require(!e.World||active!=null);
                    Wire.Text(w,pair.Key);Wire.Text(w,e.Definition);w.Write(e.Stars);w.Write(e.World);
                }
                Wire.Require(m.Position<=Wire.Limit);
                w.Write(draws.Count);foreach(var pair in draws){var d=pair.Value;Wire.Text(w,pair.Key);Wire.Text(w,d.Id);w.Write(d.Holstered);w.Write(d.Released);w.Write(d.Ready);w.Write(d.Start);w.Write(d.LastTick);w.Write(d.Sequence);Wire.Require(m.Position<=Wire.Limit);}
                w.Flush();var image=m.ToArray();Wire.Require(image.Length<=Wire.Limit);return image;
            }
        }
        public static SessionWeapons RestoreQuarantinedCustody(byte[] image,CheckpointBinding binding,IWeaponLifetimeAuthority approval,IAuthorityGate authority)
        {
            Wire.Require(binding!=null&&approval!=null&&authority!=null);
            ValidateCheckpointLayout();
            using(var r=Wire.Reader(image)){
                Wire.Require(r.ReadInt32()==0x31535741&&r.ReadInt32()==1);binding.Check(r);int rate=r.ReadInt32();Wire.Require(rate>0);
                var ledger=WeaponLifetimes.RestoreQuarantinedRegistry(Wire.Blob(r),binding,approval,authority);
                var result=new SessionWeapons(ledger,rate,authority);int count=Wire.Count(r,64);
                for(int i=0;i<count;i++){string id=Wire.Text(r),definition=Wire.Text(r);int stars=r.ReadInt32();bool world=Wire.Bool(r);
                    WeaponHandling active;WeaponTransfer pending;string actor;long epoch;
                    Wire.Require(!result.entries.ContainsKey(id)&&ledger.TryReadCheckpointMember(id,out active,out pending,out actor,out epoch)&&
                        ((definition=="longwatch"&&stars==1)||(definition=="cairn6"&&stars>=1&&stars<=5)));
                    ledger.TryReadCheckpointMember(id,out active,out pending,out actor,out epoch);
                    Wire.Require(ledger.MatchesCheckpointMechanism(id,definition=="longwatch"?WeaponMechanism.PrecisionRifle:WeaponMechanism.MagnumRevolver));
                    Wire.Require(!world||active!=null);result.entries.Add(id,new Entry{Actor=actor,Epoch=epoch,Handler=active,Pending=pending,Definition=definition,Stars=stars,World=world});
                }
                // Reject hidden registry members as well as missing mechanic members.
                // The registry validates complete membership on capture; its source image
                // count must match the exact custody set even when no capture is requested.
                Wire.Require(ledger.CheckpointMemberCount==count);
                int drawCount=Wire.Count(r,64);for(int i=0;i<drawCount;i++){
                    string actor=Wire.Text(r);var d=new Draw{Id=Wire.Text(r),Holstered=Wire.Bool(r),Released=Wire.Bool(r),Ready=r.ReadInt64(),Start=r.ReadInt64(),LastTick=r.ReadInt64(),Sequence=r.ReadInt64()};
                    Wire.Require(actor.Length>0&&!result.draws.ContainsKey(actor)&&d.Ready>=d.Start&&d.Start>=0&&d.LastTick>=-1&&d.Sequence>=-1);
                    Entry selected;Wire.Require(d.Id.Length==0||result.entries.TryGetValue(d.Id,out selected));result.draws.Add(actor,d);
                }
                Wire.End(r);return result;
            }
        }
    }
}




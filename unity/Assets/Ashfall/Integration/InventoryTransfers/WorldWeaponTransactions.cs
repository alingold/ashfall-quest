using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.InventoryItems;
using Ashfall.Modules.WeaponHandling;
namespace Ashfall.Integration.InventoryTransfers
{
    public sealed partial class SessionWeapons
    {
        // Root binds this once, using the very same lane and real inventory resolver.
        public bool BindPreparedWorldActionOwner(object lane,Func<string,Inventory> resolve)
        {if(resolve==null)return false;return BindActionOwner(lane,delegate(string actor,string id){var i=resolve(actor);return i!=null&&!i.Holstered&&i.SelectedInstanceId==id;});}
        internal bool WorldState(ItemInstance item,bool world,out string actor,out long epoch,out HandlingSnapshot snapshot,out int reserve,out long reserveRevision)
        {
            Entry e;actor=null;epoch=0;snapshot=new HandlingSnapshot();reserve=0;reserveRevision=0;
            if(item==null||!entries.TryGetValue(item.InstanceId,out e)||e.Handler==null||e.World!=world||e.Definition!=item.DefinitionId||e.Stars!=item.Stars||!e.Handler.TryReadReserve(out reserve,out reserveRevision))return false;
            actor=e.Actor;epoch=e.Epoch;snapshot=e.Handler.Snapshot;return true;
        }
        internal RuleResult PrepareWorldMove(CommandContext source,ItemInstance item,string target,long epoch,bool toWorld,out IPreparedWeaponTransfer token)
        {
            token=null;Entry e;if(!entries.TryGetValue(item.InstanceId,out e)||e.World==toWorld||e.Definition!=item.DefinitionId||e.Stars!=item.Stars)return RuleResult.Reject("world custody mismatch");
            IPreparedWeaponTransfer inner;var r=PrepareTransfer(source,item.InstanceId,target,epoch,out inner);if(!r.Accepted)return r;
            token=new WorldMove(e,inner,toWorld);return r;
        }
        sealed class WorldMove:IPreparedWeaponTransfer
        {
            readonly Entry entry;readonly IPreparedWeaponTransfer inner;readonly bool world;bool done;
            internal WorldMove(Entry e,IPreparedWeaponTransfer p,bool w){entry=e;inner=p;world=w;}
            public PreparedWeaponResult Result{get{return inner.Result;}}
            public RuleResult Validate(){return inner.Validate();}
            public void CommitValidated(){if(done)return;inner.CommitValidated();if(inner.Result==null)return;entry.World=world;done=true;}
            public void Abort(){inner.Abort();}
        }
        internal bool PrepareWorldSuspensions(string actor,HashSet<string> moving,out List<IPreparedWeaponSuspension> tokens)
        {
            tokens=new List<IPreparedWeaponSuspension>();
            foreach(var pair in entries)if(pair.Value.Actor==actor&&!pair.Value.World&&!moving.Contains(pair.Key)){
                IPreparedWeaponSuspension t;if(!pair.Value.Handler.PrepareSuspension(out t).Accepted){foreach(var p in tokens)p.Abort();return false;}tokens.Add(t);}
            return true;
        }
        internal void PublishWorldHolster(string actor)
        {Draw d;if(draws.TryGetValue(actor,out d)){d.Holstered=true;d.Released=false;}}
    }
}

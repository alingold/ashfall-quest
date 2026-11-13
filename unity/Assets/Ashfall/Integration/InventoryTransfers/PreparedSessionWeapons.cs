using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.WeaponHandling;
namespace Ashfall.Integration.InventoryTransfers
{
    public sealed partial class SessionWeapons
    {
        // Invoked only after successful inventory/pickup selection under the host lane.
        // All owned actor handlers lose old physical contacts without ammo/replay changes.
        void SuspendActor(string actor)
        {
            var prepared=new List<IPreparedWeaponSuspension>();
            foreach(Entry e in entries.Values)if(e.Actor==actor&&e.Handler!=null){IPreparedWeaponSuspension p;if(!e.Handler.PrepareSuspension(out p).Accepted){foreach(var prior in prepared)prior.Abort();return;}prepared.Add(p);}
            foreach(var p in prepared)if(!p.Validate().Accepted){foreach(var prior in prepared)prior.Abort();return;}
            foreach(var p in prepared)p.CommitValidated();
        }
        // Internal bridge port. Caller must hold the bound action lane throughout
        // prepare-all, final validation and publication of inventory/world ownership.
        internal RuleResult PrepareTransfer(CommandContext c,string id,string target,long epoch,out IPreparedWeaponTransfer prepared)
        {
            prepared=null;Entry e;
            if(!entries.TryGetValue(id,out e)||e.Handler==null)return RuleResult.Reject("missing active instance");
            IPreparedWeaponTransfer lifetime;RuleResult r=lifetimes.PrepareTransfer(c,id,target,epoch,out lifetime);
            if(!r.Accepted)return r;
            prepared=new RegistryTransfer(e,lifetime);return r;
        }
        sealed class RegistryTransfer : IPreparedWeaponTransfer
        {
            readonly Entry entry;readonly IPreparedWeaponTransfer lifetime;bool done;
            internal RegistryTransfer(Entry entry,IPreparedWeaponTransfer lifetime){this.entry=entry;this.lifetime=lifetime;}
            public PreparedWeaponResult Result {get{return lifetime.Result;}}
            public RuleResult Validate(){return lifetime.Validate();}
            public void CommitValidated(){if(done)return;lifetime.CommitValidated();var r=lifetime.Result;if(r==null)return;entry.Handler=r.Handler;entry.Actor=r.ActorId;entry.Epoch=r.Epoch;entry.Pending=null;done=true;}
            public void Abort(){lifetime.Abort();}
        }
    }
}

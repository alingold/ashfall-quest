using System;
using Ashfall.Core;
namespace Ashfall.Modules.Pickups
{
    // Trusted offline map composition only. This changes one existing, available world
    // source position; it never creates inventory, revives a consumed item or grants effects.
    public sealed partial class PickupRules
    {
        // Trusted map teardown may remove an unowned source, including a spent
        // one. Held, owned, reserved and prepared-operation sources are retained.
        public bool TryRetireLocalWorldItem(string id)
        {
            lock(sync)
            {
                Item item;
                if(quarantined||pendingSupply!=null||executing||revision==long.MaxValue||
                    id==null||!items.TryGetValue(id,out item)||item.Owner!=null||
                    (item.Reserved!=null&&item.Until>now))return false;
                foreach(var user in users.Values)if(user.Held==id)return false;
                items.Remove(id);revision++;return true;
            }
        }
        public bool TryRelocateLocalWorldItem(string id,Float3 position)
        {
            lock(sync)
            {
                Item item;
                if(quarantined||pendingSupply!=null||executing||revision==long.MaxValue||!position.IsFinite||
                    Math.Abs(position.X)>100000||Math.Abs(position.Y)>100000||Math.Abs(position.Z)>100000||
                    id==null||!items.TryGetValue(id,out item)||item.Removed||item.Owner!=null||
                    (item.Reserved!=null&&item.Until>now))return false;
                item.Position=position;revision++;return true;
            }
        }
    }
}

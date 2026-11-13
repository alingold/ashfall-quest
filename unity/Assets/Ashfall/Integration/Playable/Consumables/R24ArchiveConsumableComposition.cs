#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
using Ashfall.Integration.Playable.Consumables.WorldPickupLayout;
namespace Ashfall.Integration.Playable.Consumables
{
    public sealed partial class R23VRConsumables
    {
        // archive_map1003._place_loot runs AFTER its initial physics wait. It
        // assigns these existing props directly; unlike spawn_box, no NEW settle
        // is queued. Cancel any stale base checkpoint callback before assignment.
        public R24PickupLayoutResult AssignArchiveFoodPositions(Vector3[] validatedWorldLoot)
        {
            ValidateArchivePoints(validatedWorldLoot);sourceLayoutGeneration++;sourceSettleResults.Clear();int created=0,moved=0,preserved=0;
            foreach(var row in R24WorldPickupLayout.FoodsAndBackpacks())
            {
                if(row.IsSupply)continue;Vector3 at=validatedWorldLoot[R24ArchivePickupLayout.FoodLootIndex(row.SourceIndex,validatedWorldLoot.Length)];WorldItem item;
                if(!items.TryGetValue(row.Id,out item)){if(!Spawn(row.Id,row.Kind,at)){preserved++;continue;}created++;item=items[row.Id];}
                else{if(item.Kind!=row.Kind)throw new InvalidOperationException("Archive food identity changed");if(!RepositionWorldItem(row.Id,at,item.View.transform.rotation)){preserved++;sourceSettleResults[row.Id]=new R24PickupSettleResult(row.Id,R24PickupSettleOutcome.Preserved,F(at),F(item.View.transform.position));continue;}moved++;}
                sourceSettleResults[row.Id]=new R24PickupSettleResult(row.Id,R24PickupSettleOutcome.ArchiveAssigned,F(at),F(item.View.transform.position));
            }
            return new R24PickupLayoutResult(created,moved,preserved);
        }
        static void ValidateArchivePoints(Vector3[] points)
        {if(points==null||points.Length==0)throw new ArgumentException("Actual validated archive loot required");foreach(var p in points)if(!F(p).IsFinite)throw new ArgumentException("Actual finite archive point required");}
    }
}
#endif

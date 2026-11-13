using System;
using System.Collections.Generic;
using Ashfall.Integration.Playable.Activities;

namespace Ashfall.Integration.Playable.World.Loot
{
    public enum R24CacheCategory { Gun, Melee, Grenade, Heal }
    public struct R24CachePickup
    {
        public readonly R24CacheCategory Category;
        public readonly int Catalog,Stars,Slot;
        public readonly string FoodKind;
        public R24CachePickup(R24CacheCategory category,int catalog,int stars,int slot,string foodKind=null)
        {Category=category;Catalog=catalog;Stars=stars;Slot=slot;FoodKind=foodKind;}
    }
    // Working Unity policy requested after the R6 port: probabilistic six-item
    // caches average 3 guns, 1 melee, 1 grenade and 1 heal. Source Cache remains
    // available unchanged as the reference recipe and for Magnum-only maps.
    public static class R24MixedLootRecipe
    {
        static readonly int[] melee={14,15,16,29},grenades={26,28};
        static readonly string[] foods={"soda","shaker","banana"};
        static readonly int[] guns=GunCatalog();
        static int[] GunCatalog()
        {
            var result=new List<int>();
            foreach(int id in R23LootRecipe.ReviewCatalogIds)
                if(Array.IndexOf(melee,id)<0&&Array.IndexOf(grenades,id)<0)result.Add(id);
            return result.ToArray();
        }
        static int Below(ref R23BuildLightningRecipe.SourceRandom rng,int count)
        {uint bound=(uint)count,threshold=unchecked(0u-bound)%bound,draw;do{draw=rng.Next();}while(draw<threshold);return (int)(draw%bound);}
        // Independent bonus stream: preserves every existing six-item draw exactly.
        public static bool Backpack(string mapId,int siteIndex){if(mapId==null)throw new ArgumentNullException("mapId");if(siteIndex<0||siteIndex>65535)throw new ArgumentOutOfRangeException("siteIndex");if(R24ArchiveMapPolicy.MagnumOnly(mapId))return false;var rng=new R23BuildLightningRecipe.SourceRandom((ulong)R23LootRecipe.SourceHash(mapId)+(ulong)siteIndex*7907UL+0x4241434b5041434bUL);return Below(ref rng,4)==0;}
        public static R24CachePickup[] Cache(string mapId,int siteIndex)
        {
            if(mapId==null)throw new ArgumentNullException("mapId");
            if(siteIndex<0||siteIndex>65535)throw new ArgumentOutOfRangeException("siteIndex");
            var result=new R24CachePickup[6];
            if(R24ArchiveMapPolicy.MagnumOnly(mapId))
            {
                var original=R23LootRecipe.Cache(mapId,siteIndex);
                for(int i=0;i<original.Length;i++)result[i]=new R24CachePickup(R24CacheCategory.Gun,original[i].Catalog,original[i].Stars,original[i].Slot);
                return result;
            }
            var rng=new R23BuildLightningRecipe.SourceRandom((ulong)R23LootRecipe.SourceHash(mapId)+(ulong)siteIndex*7907UL+0x4d49584544UL);
            for(int slot=0;slot<result.Length;slot++)
            {
                int choice=Below(ref rng,6);R24CacheCategory category=choice<3?R24CacheCategory.Gun:choice==3?R24CacheCategory.Melee:choice==4?R24CacheCategory.Grenade:R24CacheCategory.Heal;
                if(category==R24CacheCategory.Heal){result[slot]=new R24CachePickup(category,-1,1,slot,foods[Below(ref rng,foods.Length)]);continue;}
                int[] pool=category==R24CacheCategory.Gun?guns:category==R24CacheCategory.Melee?melee:grenades;
                int catalog=pool[Below(ref rng,pool.Length)];
                int stars=1+(rng.Value()>.45?1:0)+(rng.Value()>.75?1:0)+(rng.Value()>.91?1:0)+(rng.Value()>.98?1:0);
                result[slot]=new R24CachePickup(category,catalog,stars,slot);
            }
            var toolRng=new R23BuildLightningRecipe.SourceRandom((ulong)R23LootRecipe.SourceHash(mapId)+(ulong)siteIndex*7907UL+0x47524150504c45UL);
            if(Below(ref toolRng,8)==0)for(int i=0;i<result.Length;i++)if(result[i].Category==R24CacheCategory.Gun){result[i]=new R24CachePickup(R24CacheCategory.Gun,32,2,i);break;}
            return result;
        }
    }
}

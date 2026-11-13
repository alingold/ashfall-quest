using System;
using Ashfall.Core;
namespace Ashfall.Integration.Playable.Consumables.WorldPickupLayout
{
    public static class R24ArchivePickupLayout
    {
        public const int FoodCount=12,AmmoCount=72;
        public static int FoodLootIndex(int sourceIndex,int lootCount)
        {if(sourceIndex<0||sourceIndex>=FoodCount||lootCount<1)throw new ArgumentOutOfRangeException();return (sourceIndex*19+11)%lootCount;}
        public static int AmmoLootIndex(int sourceIndex,int lootCount)
        {if(sourceIndex<0||sourceIndex>=AmmoCount||lootCount<1)throw new ArgumentOutOfRangeException();return (sourceIndex*13+5)%lootCount;}
        public static string AmmoFamily(int sourceIndex)
        {if(sourceIndex<0||sourceIndex>=AmmoCount)throw new ArgumentOutOfRangeException();return new[]{"rifle","pistol","shells","smg","sniper","builds"}[sourceIndex%6];}
        public static string AmmoId(string mapId,int sourceIndex)
        {
            if(String.IsNullOrEmpty(mapId)||!mapId.StartsWith("archive:",StringComparison.Ordinal)||mapId.Length>100||mapId.IndexOfAny(new[]{'\r','\n','\0'})>=0)throw new ArgumentException("Actual archive map ID required");
            AmmoFamily(sourceIndex);return "r24-archive/"+mapId.Substring(8)+"/ammo/"+sourceIndex;
        }
    }
}

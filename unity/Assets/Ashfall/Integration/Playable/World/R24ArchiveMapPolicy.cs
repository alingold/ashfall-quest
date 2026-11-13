using System;
namespace Ashfall.Integration.Playable.World
{
    // Frozen R6 archive_map1003.gd:238. These are the only source Magnum maps.
    public static class R24ArchiveMapPolicy
    {
        public static bool MagnumOnly(string mapId)
        {return String.Equals(mapId,"archive:magnum-chaosvr",StringComparison.Ordinal)||String.Equals(mapId,"archive:magnum-3-chaosvr",StringComparison.Ordinal);}
    }
}

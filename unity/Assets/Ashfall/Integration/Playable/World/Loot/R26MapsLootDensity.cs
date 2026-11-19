#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using Ashfall.Core;
using UnityEngine;

namespace Ashfall.Integration.Playable.World.Loot
{
    internal static class R26MapsLootDensity
    {
        // Twenty-four six-draw caches in one neighborhood can create 144 nearby owners.
        // Preserve sparse areas and separate floors. Keep the original ordering.
        internal static void SpaceCrowdedCaches(List<Float3> sites)
        {
            const int crowded = 24;
            if (sites.Count < crowded) return;
            var counts = new Dictionary<Vector3Int,int>();
            foreach (var p in sites)
            {
                var cell = Neighborhood(p);
                int count;
                counts.TryGetValue(cell,out count);
                counts[cell] = count+1;
            }
            var kept = new HashSet<Vector3Int>();
            int write = 0;
            for (int read=0; read<sites.Count; read++)
            {
                var site=sites[read];
                if (counts[Neighborhood(site)]>=crowded && !kept.Add(Cell(site))) continue;
                sites[write++]=site;
            }
            sites.RemoveRange(write,sites.Count-write);
        }

        static Vector3Int Neighborhood(Float3 p)
        {
            return new Vector3Int(Mathf.FloorToInt(p.X/96),Mathf.FloorToInt(p.Y/36),Mathf.FloorToInt(p.Z/96));
        }

        static Vector3Int Cell(Float3 p)
        {
            return new Vector3Int(Mathf.FloorToInt(p.X/32),Mathf.FloorToInt(p.Y/12),Mathf.FloorToInt(p.Z/32));
        }
    }
}
#endif

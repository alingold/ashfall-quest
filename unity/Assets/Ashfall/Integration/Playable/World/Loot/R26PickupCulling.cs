#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;using UnityEngine;
namespace Ashfall.Integration.Playable.World.Loot
{
    // MP-PERF / MP-FLOATING-LOOT: one presentation-only distance policy for every world
    // pickup (weapons, grenades, foods, supply boxes, backpacks). The loot and network
    // owners keep deciding eligibility ("may be shown"); this decides only whether an
    // eligible item's art exists near the local viewer. It never changes custody, reach,
    // host admission or replicated positions.
    public static class R26PickupCulling
    {
        public const float DefaultShowRange=40,HideMargin=6,UrgentRange=8;
        static float show=DefaultShowRange;
        // Metres from the local player at which pickup art appears; it leaves HideMargin further out.
        public static float ShowRange{get{return show;}set{show=Mathf.Clamp(value,10,1000);}}
        public static float HideRange{get{return show+HideMargin;}}
        // The local player's position; R23WorldLoot refreshes it every frame. Without a viewer nothing is culled.
        public static Vector3 Viewer;public static bool HasViewer;
        public static bool Within(Vector3 position,float range){return !HasViewer||(position-Viewer).sqrMagnitude<range*range;}
        // Should this item's art exist? The margin keeps the current answer at the boundary.
        public static bool Wanted(Vector3 position,bool presented){return Within(position,presented?HideRange:show);}
    }
    // Bounded round-robin over a keyed owner table. A step visits at most its budget, allocates
    // nothing after warm-up, and re-reads the keys every lap, so owners need no add/remove hooks.
    public sealed class R26CullSweep<T> where T:class
    {
        readonly Dictionary<string,T> items;readonly List<string> keys=new List<string>(256);int cursor,remaining;
        public R26CullSweep(Dictionary<string,T> items){this.items=items;}
        public void Begin(int budget){remaining=budget<items.Count?budget:items.Count;}
        public bool Next(out string id,out T item)
        {
            while(remaining-->0)
            {
                if(cursor>=keys.Count){keys.Clear();foreach(var pair in items)keys.Add(pair.Key);cursor=0;if(keys.Count==0)break;}
                id=keys[cursor++];if(items.TryGetValue(id,out item))return true;
            }
            remaining=0;id=null;item=null;return false;
        }
    }
}
#endif

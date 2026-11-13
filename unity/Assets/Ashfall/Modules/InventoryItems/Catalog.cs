using System;
using System.Collections.Generic;

namespace Ashfall.Modules.InventoryItems
{
    public sealed class WeaponStats
    {
        public readonly double Body, Head, Recoil, Spread;
        public readonly int Pellets, MagazinePerHand, Hands;
        public int MagazineTotal { get { return MagazinePerHand * Hands; } }
        public WeaponStats(double body, double head, int pellets, int magazine, int hands, double recoil, double spread)
        {
            if (!Finite(body) || !Finite(head) || !Finite(recoil) || !Finite(spread) || body < 0 || head < 0 || recoil < 0 || spread < 0 || pellets < 1 || magazine < 0 || hands < 1 || hands > 2) throw new ArgumentException("Invalid stats");
            Body=body; Head=head; Pellets=pellets; MagazinePerHand=magazine; Hands=hands; Recoil=recoil; Spread=spread;
        }
        internal static bool Finite(double v) { return !Double.IsNaN(v) && !Double.IsInfinity(v); }
    }
    public sealed class ItemDefinition
    {
        public readonly string Id, ProfileId, Label, AmmoFamily, FireMode, StackKey;
        public readonly int CatalogIndex, Category;
        public readonly bool Active, IsWeapon, Tiered;
        public readonly double RoundsPerSecond;
        private readonly WeaponStats[] stats;
        public ItemDefinition(string id, string profile, string label, int index, bool active, int category,
            string family, string mode, double rate, bool tiered, string stackKey, WeaponStats[] levels)
        {
            if (String.IsNullOrEmpty(id) || label == null || stackKey == null || !WeaponStats.Finite(rate) || rate < 0 || levels == null || levels.Length != 5) throw new ArgumentException("Invalid definition");
            foreach (WeaponStats s in levels) if (s == null) throw new ArgumentException("Missing stats");
            Id=id; ProfileId=profile; Label=label; CatalogIndex=index; IsWeapon=index>=0; Active=active; Category=category;
            AmmoFamily=family; FireMode=mode; RoundsPerSecond=rate; Tiered=tiered; StackKey=stackKey; stats=(WeaponStats[])levels.Clone();
        }
        public WeaponStats Stats(int stars)
        { if (stars<1 || stars>5 || (!Tiered && stars!=1)) throw new ArgumentOutOfRangeException("stars"); return stats[stars-1]; }
    }
    public sealed class ItemCatalog
    {
        private readonly Dictionary<string,ItemDefinition> ids=new Dictionary<string,ItemDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<int,ItemDefinition> indices=new Dictionary<int,ItemDefinition>();
        private readonly ItemDefinition[] ordered;
        public ItemCatalog(IEnumerable<ItemDefinition> definitions)
        {
            var list=new List<ItemDefinition>();
            foreach (ItemDefinition d in definitions)
            {
                if (d==null || ids.ContainsKey(d.Id) || (d.IsWeapon && indices.ContainsKey(d.CatalogIndex))) throw new ArgumentException("Duplicate/null definition");
                ids.Add(d.Id,d); if (d.IsWeapon) indices.Add(d.CatalogIndex,d); list.Add(d);
            }
            list.Sort(delegate(ItemDefinition a, ItemDefinition b) { return StringComparer.Ordinal.Compare(a.Id,b.Id); }); ordered=list.ToArray();
        }
        public ItemDefinition[] Definitions { get { return (ItemDefinition[])ordered.Clone(); } }
        public bool TryGet(string id,out ItemDefinition d) { d=null; return id!=null && ids.TryGetValue(id,out d); }
        public ItemDefinition Weapon(int index) { return indices[index]; }
        public ItemDefinition Get(string id) { return ids[id]; }
    }
}

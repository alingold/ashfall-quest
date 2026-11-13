using System;using System.Collections.Generic;using Ashfall.Core;
namespace Ashfall.Integration.Playable.World.Loot {
 public struct R23BaselineSlot {
  public readonly int Catalog,GridIndex,SourceInstanceIndex,LayoutIndex;
  public R23BaselineSlot(int catalog,int grid,int instance,int layout){Catalog=catalog;GridIndex=grid;SourceInstanceIndex=instance;LayoutIndex=layout;}
 }
 public static class R23BaselineLootRecipe {
  // Original rack has30 catalog nodes (4/22 inactive). relocate_pickups creates
  // copies of owned originals AFTER index29; archive _place_loot sorts those IDs.
  public static R23BaselineSlot[] Slots(ICollection<int> ownedOriginals,bool archive,bool magnumOnly){
   if(ownedOriginals==null)throw new ArgumentNullException("Actual original-instance ownership required");var rows=new List<R23BaselineSlot>();int next=30;var active=R23LootRecipe.ReviewCatalogIds;
   for(int i=0;i<active.Length;i++){int catalog=active[i],instance=ownedOriginals.Contains(catalog)?next++:catalog;if(!magnumOnly||catalog==7)rows.Add(new R23BaselineSlot(catalog,i,instance,0));}
   if(archive)rows.Sort(delegate(R23BaselineSlot a,R23BaselineSlot b){return a.SourceInstanceIndex.CompareTo(b.SourceInstanceIndex);});
   for(int i=0;i<rows.Count;i++){var row=rows[i];rows[i]=new R23BaselineSlot(row.Catalog,row.GridIndex,row.SourceInstanceIndex,i);}return rows.ToArray();
  }
  public static Float3 BuiltinOffset(int gridIndex){if(gridIndex<0||gridIndex>=28)throw new ArgumentOutOfRangeException("gridIndex");return new Float3(-4.3f+(gridIndex%8)*1.4f,.3f,-.60f+(gridIndex/8)*1.4f);}
  public static int ScatterIndex(int layoutIndex,int lootCount){if(layoutIndex<0||layoutIndex>=28||lootCount<=0)throw new ArgumentOutOfRangeException("Actual sorted slot and native loot points required");return layoutIndex*17%lootCount;}
 }
}

#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;using Ashfall.Core;using Ashfall.Integration.Playable.Explosives;using UnityEngine;
namespace Ashfall.Integration.Playable.World.Loot {
 public struct R23BaselinePickup {
  public readonly string ActionId;public readonly int Catalog,GridIndex,SourceInstanceIndex,LayoutIndex;public readonly Vector3 RequestedPosition;
  public R23BaselinePickup(string action,R23BaselineSlot slot,Vector3 requested){ActionId=action;Catalog=slot.Catalog;GridIndex=slot.GridIndex;SourceInstanceIndex=slot.SourceInstanceIndex;LayoutIndex=slot.LayoutIndex;RequestedPosition=requested;}
 }
 public sealed partial class R23WorldLoot {
  readonly List<string> baselineWeapons=new List<string>(),baselineGrenades=new List<string>();readonly List<R23BaselinePickup> baseline=new List<R23BaselinePickup>();readonly Dictionary<int,string> originalBaselineActions=new Dictionary<int,string>();
  readonly HashSet<string> baselineEager=new HashSet<string>();
  public string[] BaselineWeaponActionIds{get{return baselineWeapons.ToArray();}}public string[] BaselineGrenadeInstanceIds{get{return baselineGrenades.ToArray();}}public R23BaselinePickup[] BaselineKit{get{return baseline.ToArray();}}
  public string[] CacheWeaponActionIds{get{return instances.ToArray();}}public string[] CacheGrenadeInstanceIds{get{return grenadeInstances.ToArray();}}
  public int BaselineSpawnedCount{get{return baseline.Count;}}
  IEnumerable<string> AllWeaponWorldIds{get{foreach(var id in instances)yield return id;foreach(var id in baselineWeapons)yield return id;}}
  IEnumerable<string> AllGrenadeWorldIds{get{foreach(var id in grenadeInstances)yield return id;foreach(var id in baselineGrenades)yield return id;}}
  R23BaselineSlot[] BaselineSlots(bool archive){
   var owned=new List<int>();foreach(int catalog in R23LootRecipe.ReviewCatalogIds){string profile=Profile(catalog),action;bool has=profile!=null&&weapons.OwnsWeapon(profile);if(originalBaselineActions.TryGetValue(catalog,out action)){if(R23LootRecipe.GrenadeKind(catalog)==null)has|=weapons.OwnsWeapon(action);else{R23GrenadeItemState item;has|=explosives!=null&&explosives.TryReadItem(action,out item)&&item.Owned&&!item.Spent;}}if(has)owned.Add(catalog);}
   return R23BaselineLootRecipe.Slots(owned,archive,archive&&MagnumOnlyRecipe);
  }
  void SpawnBaselineArchive(Float3[] loot){
   if(loot==null||loot.Length==0)throw new System.InvalidOperationException("Actual archive loot_points required for original kit scatter");
   foreach(var slot in BaselineSlots(true)){Vector3 at;if(!sourceWorld.TryArchiveSourceToWorld(loot[R23BaselineLootRecipe.ScatterIndex(slot.LayoutIndex,loot.Length)],out at))throw new System.InvalidOperationException("Actual baseline source frame unavailable");SpawnBaseline(slot,at);}
  }
  void SpawnBaseline(R23BaselineSlot slot,Vector3 requested){
   if(!Supported(slot.Catalog)){UnsupportedDraws++;return;}bool eager=row==null||slot.SourceInstanceIndex<30;string id;if(!SpawnGround(slot.Catalog,1,requested,out id,eager))return;if(eager)baselineEager.Add(id);
   if(R23LootRecipe.GrenadeKind(slot.Catalog)==null)baselineWeapons.Add(id);else baselineGrenades.Add(id);baseline.Add(new R23BaselinePickup(id,slot,requested));if(slot.SourceInstanceIndex==slot.Catalog)originalBaselineActions[slot.Catalog]=id;if(row==null)SpawnedCount++;
  }
 }
}
#endif

#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;using UnityEngine;using Ashfall.Integration.Playable.Consumables;
namespace Ashfall.Integration.Playable.World.Loot {
 public sealed partial class R23WorldLoot {
  R24VRSupplies supplies;readonly List<string> backpackInstances=new List<string>();long backpackSerial;
  public string[] CacheBackpackInstanceIds{get{return backpackInstances.ToArray();}}
  void SpawnCacheBackpack(int sourceSite){if(supplies==null||!supplies.Ready)return;string id="r24-cache/backpack/"+(++backpackSerial);Vector3 at=sites[sourceSite]+Vector3.up*.65f;if(!supplies.Spawn(id,"backpack",at,1))return;backpackInstances.Add(id);SpawnedCount++;supplies.SetWorldItemVisible(id,false);long version=preparationVersion,generation=cacheSourceGeneration;string capturedMap=currentMap;supplies.SettleWorldItem(id,delegate{return map!=null&&map.IsLoaded&&enabledWorld&&SourceWorldReady;},delegate{return this!=null&&preparationVersion==version&&cacheSourceGeneration==generation&&currentMap==capturedMap&&backpackInstances.Contains(id);});}
  void HideCacheBackpacks(){if(supplies==null)return;foreach(string id in backpackInstances)supplies.SetWorldItemVisible(id,false);}
  void ClearCacheBackpacks(){if(supplies!=null)foreach(string id in backpackInstances){supplies.SetWorldItemVisible(id,false);supplies.RetireUncollectedWorldItem(id);}backpackInstances.Clear();}
  void StreamCacheBackpacks(bool available){if(supplies==null)return;foreach(string id in backpackInstances){Vector3 at;bool collected;if(supplies.TryReadWorldItem(id,out at,out collected))supplies.SetWorldItemVisible(id,available&&!collected);}}
 }
}
#endif

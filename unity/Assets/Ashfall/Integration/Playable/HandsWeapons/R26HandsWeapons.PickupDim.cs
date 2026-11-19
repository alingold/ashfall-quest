#if UNITY_6000_0_OR_NEWER
using System;using System.Collections.Generic;using UnityEngine;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public sealed partial class R23HandsWeapons
    {
        // MP-PICKUP: greyed-out world copies the local player cannot take right now.
        // Presentation only (host custody decides). Callers may poll every frame: the
        // material swap happens only when the state or the world visual changes.
        readonly Dictionary<string,Transform> dimmedWorldWeapons=new Dictionary<string,Transform>(StringComparer.Ordinal);readonly List<string> dimRelease=new List<string>();
        public bool SetWorldWeaponDimmed(string actionId,bool dimmed)
        {
            if(actionId==null)return false;WorldWeapon record;Transform applied;bool known=dimmedWorldWeapons.TryGetValue(actionId,out applied);
            if(!droppedWeapons.TryGetValue(actionId,out record)||record.Visual==null){if(known)dimmedWorldWeapons.Remove(actionId);return false;}
            if(dimmed){if(known&&applied==record.Visual)return true;R26PickupDimming.Apply(record.Visual,true);dimmedWorldWeapons[actionId]=record.Visual;return true;}
            if(!known)return true;if(applied==record.Visual)R26PickupDimming.Apply(record.Visual,false);dimmedWorldWeapons.Remove(actionId);return true;
        }
        public void ClearWorldWeaponDims(){dimRelease.Clear();foreach(string id in dimmedWorldWeapons.Keys)dimRelease.Add(id);foreach(string id in dimRelease)SetWorldWeaponDimmed(id,false);dimRelease.Clear();dimmedWorldWeapons.Clear();}
        // Same members as WorldWeaponActionIds without the two per-call array copies.
        public void CopyWorldWeaponActionIds(List<string> into)
        {
            into.Clear();if(round==null)return;R23WeaponInstanceInfo info;foreach(string id in droppedWeapons.Keys)if(round.TryReadWeaponInstance(id,out info))into.Add(id);
            if(starterPickup&&!holstered&&activeActionId!=null&&!droppedWeapons.ContainsKey(activeActionId)&&round.TryReadWeaponInstance(activeActionId,out info))into.Add(activeActionId);
        }
    }
}
#endif

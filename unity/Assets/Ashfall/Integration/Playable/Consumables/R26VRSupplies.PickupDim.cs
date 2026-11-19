#if UNITY_6000_0_OR_NEWER
using System;using System.Collections.Generic;using UnityEngine;
namespace Ashfall.Integration.Playable.Consumables
{
    public sealed partial class R24VRSupplies
    {
        // MP-PICKUP: greyed-out supply boxes (for example a backpack at the six-slot cap).
        // Presentation only; polled callers cost one lookup unless the state or view changes.
        readonly Dictionary<string,GameObject> dimmedViews=new Dictionary<string,GameObject>(StringComparer.Ordinal);readonly List<string> dimRelease=new List<string>();
        public bool SetWorldItemDimmed(string id,bool dimmed)
        {
            if(id==null)return false;Box box;GameObject applied;bool known=dimmedViews.TryGetValue(id,out applied);
            if(!boxes.TryGetValue(id,out box)||box.View==null){if(known)dimmedViews.Remove(id);return false;}
            if(dimmed){if(known&&applied==box.View)return true;R26PickupDimming.Apply(box.View.transform,true);dimmedViews[id]=box.View;return true;}
            if(!known)return true;if(applied==box.View)R26PickupDimming.Apply(box.View.transform,false);dimmedViews.Remove(id);return true;
        }
        public void ClearWorldItemDims(){dimRelease.Clear();foreach(string id in dimmedViews.Keys)dimRelease.Add(id);foreach(string id in dimRelease)SetWorldItemDimmed(id,false);dimRelease.Clear();dimmedViews.Clear();}
        // Same members as WorldItemIds without the per-call list and array copies.
        public void CopyWorldItemIds(List<string> into){into.Clear();foreach(string id in boxes.Keys)into.Add(id);}
        // Host-driven repositioning without re-raising WorldItemMoved for an unchanged point every frame.
        public bool RepositionWorldItemIfMoved(string id,Vector3 position){Box box;if(id!=null&&boxes.TryGetValue(id,out box)&&(box.Position-position).sqrMagnitude<.0000001f&&(box.Home-position).sqrMagnitude<.0000001f)return true;return RepositionWorldItem(id,position);}
    }
}
#endif

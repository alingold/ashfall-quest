using System;
using Ashfall.Modules.WeaponHandling;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // Original base/top-feed click and stick-down eject BEFORE their fire sample.
    // Input pump supplies values; this never polls hardware or owns ammunition.
    public sealed class R23MagazineControls
    {
        bool click=true,stick=true,desktop=true;
        public void Reset(){click=stick=desktop=true;}
        public HandlingAction Project(bool enabled,bool primaryClick,bool clickAllowed,float x,float y,bool desktopReload=false){if(!enabled||float.IsNaN(x)||float.IsInfinity(x)||float.IsNaN(y)||float.IsInfinity(y)){Reset();return HandlingAction.Sample;}var action=desktopReload&&!desktop?HandlingAction.ReloadStep:HandlingAction.Sample;if(primaryClick&&!click&&clickAllowed)action=HandlingAction.Eject;if(x*x+y*y<.25f*.25f)stick=false;else if(y<-.7f&&Math.Abs(x)<.5f&&!stick){action=HandlingAction.Eject;stick=true;}click=primaryClick;desktop=desktopReload;return action;}
    }
}

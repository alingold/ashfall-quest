using System;
using Ashfall.Modules.WeaponHandling;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public struct R23M1014ControlProjection
    {public HandlingAction Action;public bool EjectAfterShot;}
    // Only source click/stick neutral edges; no hardware polling or ammunition state.
    public sealed class R23M1014Controls
    {
        bool clickPrevious=true,stickPrevious=true,desktopPrevious=true;
        public void Reset(){clickPrevious=stickPrevious=desktopPrevious=true;}
        public R23M1014ControlProjection Project(bool enabled,bool primaryClick,bool clickAllowed,float stickX,float stickY,bool desktopReload=false)
        {
            var result=new R23M1014ControlProjection{Action=HandlingAction.Sample};
            if(!enabled||Single.IsNaN(stickX)||Single.IsNaN(stickY)||Single.IsInfinity(stickX)||Single.IsInfinity(stickY)){Reset();return result;}
            if(desktopReload&&!desktopPrevious)result.Action=HandlingAction.ReloadStep;
            else if(primaryClick&&!clickPrevious&&clickAllowed)result.Action=HandlingAction.Eject;
            float length2=stickX*stickX+stickY*stickY;
            if(length2<.25f*.25f)stickPrevious=false;
            else if(stickY<-.7f&&Math.Abs(stickX)<.5f&&!stickPrevious){result.EjectAfterShot=true;stickPrevious=true;}
            clickPrevious=primaryClick;desktopPrevious=desktopReload;return result;
        }
    }
}

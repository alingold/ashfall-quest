#if UNITY_6000_0_OR_NEWER
using Ashfall.Integration.Playable.Input;
using Ashfall.Modules.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ashfall.Integration.Playable
{
    public sealed partial class R23PlayableLoop
    {
        public R24TacticalMap TacticalMap {get;private set;}
        public bool TacticalMapOpen {get{return TacticalMap!=null&&TacticalMap.Opened;}}
        void InitializeTacticalMap()
        {
            TacticalMap=new GameObject("Source support-trigger tactical map").AddComponent<R24TacticalMap>();TacticalMap.transform.SetParent(transform,false);TacticalMap.Initialize(this);
        }
        // Same live and native input route. Only the support hand owns this hold;
        // the selected dominant hand continues to own construction when closed.
        public void StepTacticalMap(HandSample left,HandSample right,bool hardware)
        {
            if(TacticalMap==null)return;int support=Gameplay.PrimaryHand==0?1:0;var sample=support==0?left:right;
            bool handReady=support==0?Rig.LeftInputReady:Rig.RightInputReady;
            int index=HandsWeapons.R23WeaponPickupRules.CatalogIndex(Gameplay.ResolveWeaponProfile(Gameplay.ActiveWeaponActionId));
            bool eligible=Ready&&roundStarted&&!switchingMap&&!PageBlocksGameplay&&!BackpackOpen&&hardware&&sample.Tracked&&handReady&&(Gameplay.IsHolstered||index!=11&&index!=12);
            bool pressed=sample.Trigger>.65f;
            if(!Rig.DisplayRunning&&Keyboard.current!=null)pressed|=Keyboard.current.mKey.isPressed;
            TacticalMap.Sample(pressed,eligible);
            if(TacticalMapOpen)Rig.ConstructionInputAllowed=false;
        }
    }
}
#endif

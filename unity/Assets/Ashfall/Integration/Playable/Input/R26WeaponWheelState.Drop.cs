using System;
namespace Ashfall.Integration.Playable.Input
{
    public sealed partial class R23WeaponWheelState
    {
        // Godot weapon_rack.gd: DROP_HOLD_SECONDS; one attempt owns the A gesture,
        // even when placement or host custody refuses it. Release must never equip.
        public const float DropHoldSeconds=3f;
        int dropSlot=-1;double dropElapsed;bool dropConsumed;
        public int DropSlot {get{return dropSlot;}}
        public float DropFraction {get{return dropSlot<0?0:(float)Math.Min(1,dropElapsed/DropHoldSeconds);}}
        void ResetDrop(){dropSlot=-1;dropElapsed=0;dropConsumed=false;}
        R23WeaponWheelRequest StepDrop(float dt,int primaryHand)
        {
            if(dropConsumed)return null;
            int slot=Active?R23WeaponWheelRules.SlotForSector(HighlightedSector):-1;
            if(slot<0||slot>=Capacity||slots[slot]==null||!slots[slot].CanDrop){dropSlot=-1;dropElapsed=0;return null;}
            if(slot!=dropSlot){dropSlot=slot;dropElapsed=0;}
            dropElapsed+=dt;if(dropElapsed+0.0000001f<DropHoldSeconds)return null;
            dropConsumed=true;dropSlot=-1;dropElapsed=0;
            return new R23WeaponWheelRequest(R23WeaponWheelAction.Drop,slots[slot].Id,slot,primaryHand);
        }
    }
}

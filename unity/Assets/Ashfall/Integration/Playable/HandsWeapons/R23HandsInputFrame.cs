using System;
using Ashfall.Core;
using Ashfall.Modules.WeaponHandling;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // Compatibility boundary for existing object-initializer pumps. Snapshot is immutable.
    public struct R23HandsInputFrame
    {
        public bool LeftTracked, RightTracked;
        public bool LeftHandReserved,RightHandReserved;
        public float LeftGrip,LeftTrigger,RightGrip,RightTrigger;
        public bool LeftGripPressed,LeftTriggerPressed,LeftPrimary,LeftSecondary;
        public bool RightGripPressed,RightTriggerPressed,RightPrimary,RightSecondary;
        public bool LeftStickClick,RightStickClick;
        public float LeftStickX,LeftStickY,RightStickX,RightStickY;
        public bool ReloadRequested;
        // Legacy aliases: Grip/Trigger/ReloadPressed are RIGHT; SupportGrip is physical LEFT.
        public bool SupportHandReserved,SupportGripPressed;
        public float SupportGrip;
        public bool GripPressed, TriggerPressed, ReloadPressed, RestartPressed, NextWeaponPressed;
        public float Grip, Trigger;
        public bool GripHeld { get { return Snapshot.Right.GripHeld; } }
        public bool TriggerHeld { get { return Snapshot.Right.TriggerHeld; } }
        public R23HandsInputSnapshot Snapshot { get {
            return new R23HandsInputSnapshot(
                new R23HandInput(LeftTracked,LeftHandReserved||SupportHandReserved,R23HandInput.Combine(LeftGrip,SupportGrip),LeftTrigger,LeftGripPressed||SupportGripPressed,LeftTriggerPressed,LeftPrimary,LeftSecondary,LeftStickClick,LeftStickX,LeftStickY),
                new R23HandInput(RightTracked,RightHandReserved,R23HandInput.Combine(RightGrip,Grip),R23HandInput.Combine(RightTrigger,Trigger),RightGripPressed||GripPressed,RightTriggerPressed||TriggerPressed,RightPrimary||ReloadPressed,RightSecondary,RightStickClick||ReloadPressed,RightStickX,RightStickY),
                ReloadRequested,NextWeaponPressed&&LeftTracked,RestartPressed&&LeftTracked);
        } }
    }
    public struct R23HandInput
    {
        public readonly bool Tracked,Reserved,GripPressed,TriggerPressed,Primary,Secondary,StickClick;
        public readonly float Grip,Trigger,StickX,StickY;
        public R23HandInput(bool tracked,bool reserved,float grip,float trigger,bool gripPressed=false,bool triggerPressed=false,bool primary=false,bool secondary=false,bool stickClick=false,float stickX=0,float stickY=0) {
            Tracked=tracked;Reserved=reserved;Grip=Axis(grip);Trigger=Axis(trigger);GripPressed=gripPressed;TriggerPressed=triggerPressed;Primary=primary;Secondary=secondary;StickClick=stickClick;StickX=Stick(stickX);StickY=Stick(stickY);
        }
        static float Stick(float value){return float.IsNaN(value)||float.IsInfinity(value)?0:Math.Max(-1,Math.Min(1,value));}
        static float Axis(float value) { return float.IsNaN(value)||float.IsInfinity(value)?0:Math.Max(0,Math.Min(1,value)); }
        internal static float Combine(float a,float b) { return Math.Max(Axis(a),Axis(b)); }
        public bool GripHeld { get { return GripPressed||Grip>=.55f; } }
        public bool TriggerHeld { get { return TriggerPressed||Trigger>=.65f; } }
        public bool TriggerReleased { get { return !TriggerPressed&&Trigger<.2f; } }
    }
    public struct R23HandsInputSnapshot
    {
        public readonly R23HandInput Left,Right;
        public readonly bool ReloadRequested,NextWeaponRequested,RestartRequested;
        public R23HandsInputSnapshot(R23HandInput left,R23HandInput right,bool reloadRequested=false,bool nextWeaponRequested=false,bool restartRequested=false) {
            Left=left;Right=right;ReloadRequested=reloadRequested;NextWeaponRequested=nextWeaponRequested;RestartRequested=restartRequested;
        }
        public R23HandInput Hand(int hand) { if(hand<0||hand>1)throw new ArgumentOutOfRangeException("hand");return hand==0?Left:Right; }
    }
    public static class R23HandRoleRules
    {
        static bool Candidate(R23HandInput hand,float distance,float reach) { return hand.Tracked&&!hand.Reserved&&hand.GripHeld&&!float.IsNaN(distance)&&!float.IsInfinity(distance)&&distance>=0&&distance<=reach; }
        public static int ChoosePickupHand(R23HandsInputSnapshot frame,float leftDistance,float rightDistance,float reach=.30f) {
            bool l=Candidate(frame.Left,leftDistance,reach),r=Candidate(frame.Right,rightDistance,reach);
            return l&&(!r||leftDistance<rightDistance)?0:r?1:-1;
        }
        public static HandlingInput Project(R23HandsInputSnapshot frame,int primaryHand,Float3 supportLocal,float foregripDistance) {
            var primary=frame.Hand(primaryHand);var support=frame.Hand(1-primaryHand);
            return new HandlingInput{Enabled=true,PrimaryTracked=primary.Tracked,PrimaryClimbing=primary.Reserved,SupportTracked=support.Tracked,SupportReserved=support.Reserved,LeftHanded=primaryHand==0,Trigger=primary.TriggerHeld,Grip=support.GripHeld,SupportLocal=supportLocal,ForegripDistance=foregripDistance};
        }
    }
    // spr.gd's authored semi trigger uses <.2 rearm independently of its >.65 fire threshold.
    public sealed class R23SemiTriggerLatch
    {
        bool armed=true;
        public bool Armed { get { return armed; } }
        public void Sample(bool fullyReleased) { if(fullyReleased)armed=true; }
        public bool Allows(bool trigger) { return armed&&trigger; }
        public void Shot() { armed=false; }
        public void Suspend() { armed=false; }
        public void Reset() { armed=true; }
    }
}

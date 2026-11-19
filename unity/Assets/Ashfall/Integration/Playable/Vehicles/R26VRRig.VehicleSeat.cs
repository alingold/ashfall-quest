#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using Ashfall.Integration.Playable.Vehicles;
namespace Ashfall.Integration.Playable
{
    // r19 VEHICLES19: overhead eject handle (every vehicle seat) and the helicopter collective (left grip + tilt).
    public sealed partial class R23VRRig
    {
        readonly R26EjectHandleState ejectHandle=new R26EjectHandleState();
        R26VehicleEjectHandleView ejectView;
        Vector3 seatHeadLocal;bool seatHeadSet;
        float collectiveRef;bool collectiveArmed,collectiveDown;
        float collectiveSmoothed;bool collectiveHeld;
        // True only inside the DismountVehicle callback of a handle eject, so the session can send the pull with the exit.
        public bool DismountedByHandle{get;private set;}
        public float EjectHandlePull{get{return seatHeadSet?ejectHandle.Pull:0;}}
        public bool EjectHandleGrabbed{get{return seatHeadSet&&ejectHandle.Grabbed;}}
        public bool EjectHandleVisible{get{return seatHeadSet&&Riding&&!VehicleBoarding;}}
        public float VehicleCollective{get{return collectiveHeld?collectiveSmoothed:0;}}
        public bool VehicleCollectiveHeld{get{return collectiveHeld;}}
        Vector3 EjectHandleRestLocal{get{return seatHeadLocal+new Vector3(0,R26VehicleSeat.HandleAboveHead,R26VehicleSeat.HandleForward);}}
        public Vector3 EjectHandleRestWorld{get{return transform.TransformPoint(EjectHandleRestLocal);}}
        void HideVehicleSeat()
        {
            seatHeadSet=false;collectiveHeld=false;collectiveSmoothed=0;collectiveArmed=false;collectiveDown=false;ejectHandle.Reset();
            if(ejectView!=null)ejectView.ShowLocal(Vector3.zero,0,false,false);
        }
        bool SeatHandFree(int hand){return hand==0?LeftInput.Tracked&&!LeftHandOccupied&&Left!=null:RightInput.Tracked&&!RightHandOccupied&&Right!=null;}
        Vector3 SeatHandLocal(int hand){return transform.InverseTransformPoint((hand==0?Left:Right).position);}
        // The hand is on, or gripping near, the handle: fallbacks and other grip actions stay out of its way.
        bool VehicleSeatHandBusy(int hand)
        {
            if(!seatHeadSet)return false;
            if(ejectHandle.Grabbed&&ejectHandle.Hand==hand)return true;
            if(!SeatHandFree(hand))return false;
            Vector3 rest=EjectHandleRestLocal-new Vector3(0,ejectHandle.Pull,0);
            return (SeatHandLocal(hand)-rest).sqrMagnitude<.25f*.25f;
        }
        // Returns true on the frame the pull completes. Called once per frame while seated.
        bool TickVehicleSeat(float dt)
        {
            Vector3 head=transform.InverseTransformPoint(Head.transform.position);
            if(!seatHeadSet){seatHeadLocal=head;seatHeadSet=true;ejectHandle.Reset();collectiveArmed=false;collectiveDown=false;collectiveHeld=false;collectiveSmoothed=0;}
            // The seat anchor re-centres slowly (height recalibration, settling) and freezes while the handle is held, so it never chases the head.
            else if(!ejectHandle.Grabbed)seatHeadLocal=Vector3.Lerp(seatHeadLocal,head,1-Mathf.Exp(-dt/4f));
            if(ejectView==null)ejectView=R26VehicleEjectHandleView.Create(transform,"R26 eject handle");
            Vector3 rest=EjectHandleRestLocal;
            bool lok=SeatHandFree(0),rok=SeatHandFree(1);Vector3 l=lok?SeatHandLocal(0):Vector3.zero,r=rok?SeatHandLocal(1):Vector3.zero;
            bool eject=ejectHandle.Step(dt,rest.x,rest.y,rest.z,new R26SeatHand(lok,l.x,l.y,l.z,LeftInput.Grip),new R26SeatHand(rok,r.x,r.y,r.z,RightInput.Grip));
            int hand=ejectHandle.Hand;
            if(ejectHandle.GrabbedNow){TraversalHaptic(hand,.35f);ejectView.Click(1.7f);}
            if(ejectHandle.HapticSteps>0)TraversalHaptic(hand,.15f);
            if(ejectHandle.CrossedThreshold||eject){TraversalHaptic(hand,1f);ejectView.Click(1);}
            if(VehicleSeatHandBusy(0)&&LeftInput.Grip>R26VehicleSeat.GripUp)LeftObjectiveGripReserved=true;
            if(VehicleSeatHandBusy(1)&&RightInput.Grip>R26VehicleSeat.GripUp)RightObjectiveGripReserved=true;
            ejectView.ShowLocal(rest,ejectHandle.Pull,ejectHandle.Grabbed,!eject);
            return eject;
        }
        // Left-hand collective. The left grip is consumed by it (no climbing, grabs or other left-grip actions while held).
        void TickCollective(float dt)
        {
            bool ok=SeatHandFree(0);
            if(ok){
                float g=LeftInput.Grip;collectiveDown=g>=R26VehicleSeat.GripDown||collectiveDown&&g>=R26VehicleSeat.GripUp;
            }else collectiveDown=false;
            if(!collectiveDown){collectiveArmed=true;collectiveHeld=false;collectiveSmoothed=0;return;}
            LeftObjectiveGripReserved=true;
            // A new press only: the mounting grip, or a grip that started on the eject handle, never becomes a collective.
            if(!collectiveHeld){
                if(!collectiveArmed||VehicleSeatHandBusy(0)){collectiveArmed=false;return;}
                collectiveHeld=true;collectiveRef=LeftControllerPitch();collectiveSmoothed=0;
            }
            collectiveSmoothed=R26VehicleSeat.Smooth(collectiveSmoothed,R26VehicleSeat.Command(LeftControllerPitch()-collectiveRef),dt);
        }
        float LeftControllerPitch()
        {
            Quaternion q=LeftAimTracked?LeftAimRotation:Left.rotation;Vector3 f=q*Vector3.forward;
            return R26VehicleSeat.PitchDegrees(f.x,f.y,f.z);
        }
    }
}
#endif

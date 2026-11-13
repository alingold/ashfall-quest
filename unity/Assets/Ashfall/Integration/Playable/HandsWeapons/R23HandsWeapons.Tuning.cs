#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public sealed partial class R23HandsWeapons
    {
        float motionDamping=1,offhandInfluence,supportAimWeight;bool aimFilterReady;Quaternion filteredAim,aimPlayerRotation;string aimFilterAction;int aimFilterHand;
        public float MotionDamping {get{return motionDamping;}}
        public float OffhandInfluence {get{return offhandInfluence;}}
        public void SetWeaponTuning(float damping,float offhand)
        {
            if(float.IsNaN(damping)||float.IsInfinity(damping)||damping<0||damping>2||float.IsNaN(offhand)||float.IsInfinity(offhand)||offhand<0||offhand>1)throw new ArgumentOutOfRangeException("source weapon tuning");
            if(motionDamping==damping&&offhandInfluence==offhand)return;motionDamping=damping;offhandInfluence=offhand;aimFilterReady=false;
        }
        void ApplySourceAimTuning(float seconds,bool suspended)
        {
            if(suspended||weapon==null||player==null||!haveInputSnapshot||!lastInput.Hand(primaryHand).Tracked){aimFilterReady=false;supportAimWeight=0;return;}
            if(aimFilterAction!=activeActionId||aimFilterHand!=primaryHand){aimFilterReady=false;supportAimWeight=0;aimFilterAction=activeActionId;aimFilterHand=primaryHand;}
            Quaternion target=weapon.rotation;var support=lastInput.Hand(1-primaryHand);bool supported=round!=null&&round.ActiveSnapshot.Supported&&support.Tracked&&!support.Reserved;
            supportAimWeight=Mathf.MoveTowards(supportAimWeight,supported?1:0,Mathf.Max(0,seconds)*9);
            if(offhandInfluence>0&&supportAimWeight>.001f&&support.Tracked&&!support.Reserved)
            {
                var holding=primaryHand==0?left:right;var other=primaryHand==0?right:left;Vector3 up=target*Vector3.up,towards=other.position+up*.025f-holding.position;
                if(towards.magnitude>.15f&&Vector3.Dot(towards.normalized,target*Vector3.forward)>.8f)target=Quaternion.Slerp(target,Quaternion.LookRotation(towards.normalized,up),supportAimWeight*offhandInfluence);
            }
            // weapon.gd: yaw locomotion is not controller jitter; position stays exact.
            Quaternion playerBasis=player.rotation;if(aimFilterReady)filteredAim=playerBasis*Quaternion.Inverse(aimPlayerRotation)*filteredAim;aimPlayerRotation=playerBasis;
            if(!aimFilterReady||motionDamping<=0)filteredAim=target;
            else filteredAim=Quaternion.Slerp(filteredAim,target,1-Mathf.Exp(-Mathf.Max(0,seconds)/(.006f+.09f*motionDamping*motionDamping)));
            aimFilterReady=true;
        }
        Quaternion SourceAimRotation {get{return aimFilterReady&&aimFilterAction==activeActionId&&aimFilterHand==primaryHand?filteredAim:weapon.rotation;}}
    }
}
#endif

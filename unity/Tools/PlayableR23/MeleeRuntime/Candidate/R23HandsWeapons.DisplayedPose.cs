#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public sealed partial class R23HandsWeapons
    {
        bool requestedGripToggle;
        public void SetGripToggle(bool enabled){requestedGripToggle=enabled;if(round!=null)round.SetGripToggle(enabled);}
        public bool TryReadAmmoReserveFamily(string family,out int available){available=0;return round!=null&&round.TryReadAmmoReserveFamily(family,out available);}
        public int ApplyReserveSupply(string eventId,string family,int requested){return round==null?0:round.ApplyReserveSupply(eventId,family,requested);}
        public int WithdrawReserveSupply(string family,int requested){return round==null?0:round.WithdrawReserveSupply(family,requested);}
        public int ReadAmmoReserveFamily(string family){return round==null?0:round.ReadAmmoReserveFamily(family);}
        public bool TryReadPracticeDamageTarget(int index,out string id,out Vector3 center){id=null;center=Vector3.zero;if(!Ready||round==null||index<0||index>=targets.Count)return false;var target=targets[index];var health=round.ReadTarget(target.TargetId);if(health==null||health.Life!=Ashfall.Modules.HealthStates.LifeState.Alive)return false;id=target.TargetId;center=target.transform.position;return true;}
        // Explosion owner supplies its accepted finite falloff/contact decision. Health
        // remains the same registered target service; no fabricated second damage pool.
        public bool ApplyTargetDamage(string id,float amount,Vector3 origin){if(!Ready||round==null||!To(origin).IsFinite)return false;foreach(var target in targets)if(target.TargetId==id){var before=round.ReadTarget(id);if(!round.ApplyTargetDamage(id,amount))return false;var after=round.ReadTarget(id);target.Refresh(round);if(TargetDamaged!=null)TargetDamaged(id,false,before.Shield-after.Shield,before.Health-after.Health);return true;}return false;}
        R23ShotPose displayedShot,lastAcceptedShot;bool displayedPoseValid,lastAcceptedPoseValid;
        Vector3 displayedAimPoint;bool displayedAimBlocked;
        public Vector3 DisplayedAimPoint {get{return displayedAimPoint;}}
        public bool DisplayedAimBlocked {get{return displayedAimBlocked;}}
        public bool TryReadDisplayedShotPose(out R23ShotPose pose){pose=displayedShot;return displayedPoseValid&&FeedbackAllowed;}
        public bool TryReadLastAcceptedShotPose(out R23ShotPose pose){pose=lastAcceptedShot;return lastAcceptedPoseValid;}
        public string LastAcceptedShotActionId {get;private set;}
        public long LastAcceptedNativeShot {get;private set;}
        public bool TryReadHandArtPose(int hand,out Vector3 position,out Quaternion rotation){var visual=hand==0?leftVisual:hand==1?rightVisual:null;position=visual==null?Vector3.zero:visual.position;rotation=visual==null?Quaternion.identity:visual.rotation;return visual!=null&&visual.gameObject.activeInHierarchy;}
        R23ShotPose NeutralGunFrame(){var rotation=SourceAimRotation;return new R23ShotPose(To(weapon.position),To(rotation*Vector3.right),To(rotation*Vector3.up),To(rotation*Vector3.forward));}
        void RenderDisplayedPose(){
            if(weapon==null||muzzle==null||selectedWeapon>=weaponModels.Count)return;
            if(IsKnife){RenderKnifePose();return;}if(IsSmaw){RenderSmawPose();return;}var feedback=ActiveFeedback;feedback.SetSupported(round!=null&&round.ActiveSnapshot.Supported);
            R23ShotPose model;R23WeaponPoseMath.Resolve(NeutralGunFrame(),To(sourceMuzzles[selectedWeapon]),modelScales[selectedWeapon],feedback.Distance*ActiveRecoilScale,feedback.PitchRadians*ActiveRecoilScale,feedback.YawRadians*ActiveRecoilScale,R23WeaponPoseMath.UprightPulse(ActiveWeaponId),out model,out displayedShot);
            weaponModels[selectedWeapon].SetPositionAndRotation(From(model.Origin),Quaternion.LookRotation(From(model.Forward),From(model.Up)));
            // One published unit muzzle drives accepted bullets, reticles, optics, laser and flash.
            muzzle.SetPositionAndRotation(From(displayedShot.Origin),Quaternion.LookRotation(From(displayedShot.Forward),From(displayedShot.Up)));displayedPoseValid=true;
        }
        void ResolveDisplayedAim(){
            if(!displayedPoseValid||head==null)return;if(IsKnife){displayedAimPoint=From(displayedShot.Origin);displayedAimBlocked=false;return;}if(IsSmaw&&smawView!=null){displayedAimPoint=smawView.AimPosition;displayedAimBlocked=smawView.HeadBlocked;return;}
            Vector3 origin=From(displayedShot.Origin),direction=From(displayedShot.Forward),segment=origin-head.transform.position;RaycastHit hit;
            displayedAimPoint=origin+direction*ActiveRange;displayedAimBlocked=Nearest(head.transform.position,segment.normalized,segment.magnitude,out hit);
            if(displayedAimBlocked)displayedAimPoint=hit.point;else if(Nearest(origin,direction,ActiveRange,out hit))displayedAimPoint=hit.point;
        }
        [BeforeRenderOrder(1100)]
        void RefreshLateTrackedPresentation(){
            // Rig updates its tracked parents before this callback. No input sampling,
            // recoil integration, native owner tick or player/controller transform writes.
            if(!FeedbackAllowed){ApplyFreeHandContactOffsets();return;}
            RenderDisplayedPose();ResolveDisplayedAim();PresentDisplayedAim();ApplyFreeHandContactOffsets();
        }
        void PresentDisplayedAim(){
            if(IsKnife||IsSmaw)return;if(gunFeedback==null||!displayedPoseValid)return;
            Vector3 origin=From(displayedShot.Origin),direction=From(displayedShot.Forward);var feedback=ActiveFeedback;
            if(mp9Laser!=null)mp9Laser.Present(origin,displayedAimPoint,head.transform.position,From(displayedShot.Right),ActiveWeaponId=="mp9"&&!displayedAimBlocked);
            bool sight=ViewingSight(displayedAimPoint);
            bool visible=!displayedAimBlocked&&ActiveWeaponId!="spr"&&ActiveWeaponId!="mp9"&&ActiveWeaponId!="s85"&&ReloadStage==Ashfall.Modules.WeaponHandling.ReloadPhase.Ready&&Ammo>0&&!sight;
            gunFeedback.PresentReticle(head,displayedAimPoint,direction,feedback.ReticleWidth(round.ActiveSnapshot.Supported),visible,IsMagnum,round.ActiveSnapshot.Supported,IsShotgun);
        }
    }
}
#endif

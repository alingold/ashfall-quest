#if UNITY_6000_0_OR_NEWER
using Ashfall.Integration.Playable.HandsWeapons;using Ashfall.Integration.Playable.Match;using Ashfall.Modules.HealthStates;using UnityEngine;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanRemoteAvatar
    {
        string remoteProfile;int remoteHand=1;bool remoteSuppressed=true,remoteBlocked=true;LifeState remoteLife=LifeState.Alive;
        readonly Transform[] remoteWeapons=new Transform[2];readonly float[] remoteScales=new float[2];readonly Vector3[] remoteBores=new Vector3[2];R23HandsWeapons remoteSource;R24GhostAppearance remoteGhost;TextMesh remoteName;string liveName;
        public string HeldProfile{get{return remoteProfile;}}public bool GhostVisible{get{return remoteGhost!=null&&remoteGhost.Active&&Visible;}}
        public int EquipmentBuilds{get;private set;}
        public bool WeaponVisible{get{return remoteWeapons[0]!=null&&remoteWeapons[0].gameObject.activeInHierarchy||remoteWeapons[1]!=null&&remoteWeapons[1].gameObject.activeInHierarchy;}}
        internal void ReceivePresentation(R23HandsWeapons source,R26RemoteEquipment equipped,LifeState life)
        {
            remoteLife=life;remoteSuppressed=equipped==null||equipped.Suppressed;remoteBlocked=equipped==null||equipped.Blocked;
            string profile=equipped==null?null:equipped.Profile;int hand=equipped==null?1:equipped.Hand;
            if(remoteSource!=source||remoteProfile!=profile||remoteHand!=hand){foreach(var model in remoteWeapons)if(model!=null){model.gameObject.SetActive(false);Destroy(model.gameObject);}remoteWeapons[0]=remoteWeapons[1]=null;remoteSource=source;remoteProfile=profile;remoteHand=hand;}
            if(profile!=null&&source!=null&&source.Ready&&remoteWeapons[hand]==null){remoteWeapons[hand]=source.CreateRemoteWeaponArt(profile,transform,out remoteScales[hand],out remoteBores[hand]);if(R24LanLoadout.Paired(profile))remoteWeapons[1-hand]=source.CreateRemoteWeaponArt(profile,transform,out remoteScales[1-hand],out remoteBores[1-hand]);EquipmentBuilds++;}
        }
        readonly Transform[] rescuePads=new Transform[2];AudioSource rescueVoice;double playedRescue=-1;bool showingRescue;
        readonly Mesh[] rescueCoronas=new Mesh[2];readonly MeshRenderer[] rescueCoronaViews=new MeshRenderer[2];AudioSource remoteChargeVoice;int coronaTick=-1,rescueChargePhase;
        internal bool RescueCoronaVisible{get{return RescuePaddlesVisible&&rescueCoronaViews[0]!=null&&rescueCoronaViews[0].enabled;}}
        internal int RescueChargeCues{get;private set;}
        internal int RescueReadyCues{get;private set;}
        public bool RescuePaddlesVisible{get{return showingRescue&&rescuePads[0]!=null&&rescuePads[0].gameObject.activeInHierarchy;}}
        public int RescueSounds{get;private set;}
        internal void ShowRescue(R24VRRevivePaddles source,bool equipped,double cue,float charge,float remaining)
        {
            showingRescue=visible&&handsReady&&remoteLife==LifeState.Alive&&source!=null&&source.Ready&&(equipped||cue>=0||charge>0||remaining>0);
            bool charged=showingRescue&&remaining>0;int tick=Mathf.FloorToInt(Time.unscaledTime*28);
            for(int hand=0;hand<2;hand++){
                var glove=hand==0?left:right;
                if(showingRescue&&rescuePads[hand]==null)rescuePads[hand]=source.CopyPaddle(glove,hand);
                var pad=rescuePads[hand];if(pad==null)continue;pad.gameObject.SetActive(showingRescue);
                if(charged&&rescueCoronas[hand]==null)rescueCoronas[hand]=source.MakeRemoteCorona(pad,out rescueCoronaViews[hand]);
                var corona=rescueCoronaViews[hand];
                if(corona!=null){corona.enabled=charged;if(charged&&tick!=coronaTick)source.DrawRemoteCorona(pad,rescueCoronas[hand],corona,tick);}
                if(!showingRescue)continue;
                var grip=Quaternion.Inverse(R23TrackedGripPose.GloveLocalRotation(hand))*R23TrackedGripPose.LocalRotation;
                pad.localPosition=grip*new Vector3(0,0,.1f);
                pad.localRotation=grip*Quaternion.AngleAxis(cue<0?0:(hand==0?1:-1)*Time.unscaledTime*360,Vector3.right)*Quaternion.AngleAxis(90,Vector3.forward);
            }
            coronaTick=charged?tick:-1;
            int soundPhase=charged?2:showingRescue&&charge>0?1:0;
            bool changed=soundPhase!=rescueChargePhase;rescueChargePhase=soundPhase;
            if(soundPhase!=0){
                if(remoteChargeVoice==null)remoteChargeVoice=source.MakeRemoteChargeVoice(rescuePads[1]);
                source.PresentRemoteChargeSound(remoteChargeVoice,charge,remaining,changed);
                if(changed&&remoteChargeVoice.clip!=null){
                    if(soundPhase==2)RescueReadyCues++;else RescueChargeCues++;
                }
            }else if(changed&&remoteChargeVoice!=null)remoteChargeVoice.Stop();
            if(showingRescue&&cue>=0&&cue!=playedRescue){
                playedRescue=cue;if(rescueVoice==null){rescueVoice=right.gameObject.AddComponent<AudioSource>();rescueVoice.playOnAwake=false;}
                source.PlayRescue(rescueVoice);if(rescueVoice.clip!=null)RescueSounds++;
            }
        }
        bool RemoteHolding(int hand){return !showingRescue&&visible&&target.Tracked&&handsReady&&!remoteSuppressed&&!remoteBlocked&&remoteLife==LifeState.Alive&&remoteWeapons[hand]!=null&&(target.Flags&(hand==0?2:4))!=0;}
        void PresentRemoteLifeAndWeapon()
        {
            bool ghost=R24GhostLifePolicy.Ghost(true,remoteLife);if(remoteGhost==null&&ghost)remoteGhost=gameObject.AddComponent<R24GhostAppearance>();
            if(remoteGhost!=null){remoteGhost.SetRoots(appearance.CurrentVisualRoot,handsReady?left:null,handsReady?right:null);remoteGhost.SetEnabled(ghost);if(ghost)remoteGhost.Refresh();}
            if(remoteName==null){remoteName=nameTag.GetComponent<TextMesh>();liveName=remoteName.text;}string label=liveName;/* HUD19 DOWNED-TAG: name only; the ghost tint and look say it. */if(remoteName.text!=label)remoteName.text=label;remoteName.color=ghost?new Color(.65f,.85f,1):Color.white;
            for(int hand=0;hand<2;hand++){var model=remoteWeapons[hand];if(model==null)continue;bool held=RemoteHolding(hand);model.gameObject.SetActive(held);if(!held)continue;
                var glove=hand==0?left:right;float scale=remoteScales[hand];Quaternion rotation;Vector3 contact;
                if(R24LanLoadout.Paired(remoteProfile)){bool automatic=remoteProfile=="uzis";rotation=glove.rotation*Quaternion.Inverse(R23TrackedGripPose.GloveLocalRotation(hand))*R23TrackedGripPose.LocalRotation*Quaternion.Euler(automatic?0:-35,0,0);contact=new Vector3(R24DualDefinition.SpinPivot.X,R24DualDefinition.SpinPivot.Y,R24DualDefinition.SpinPivot.Z)*scale+(automatic?new Vector3(0,0,-.0127f):new Vector3(0,.1524f-.1016f*Mathf.Cos(35*Mathf.Deg2Rad),-.1016f*Mathf.Sin(35*Mathf.Deg2Rad)));}
                else{var fit=R23WeaponGripFit.ForProfile(remoteProfile);rotation=glove.rotation*Quaternion.Inverse(R23TrackedGripPose.GloveLocalRotation(hand))*R23TrackedGripPose.LocalRotation*Quaternion.AngleAxis(-fit.PitchRadians*Mathf.Rad2Deg,Vector3.right);var point=fit.ModelContact(scale);contact=new Vector3(point.X,point.Y,point.Z)*scale;}
                model.SetPositionAndRotation(glove.position-rotation*contact,rotation);
            }
        }
        internal bool TryRemoteMuzzle(int hand,out Vector3 point,out Quaternion rotation){point=Vector3.zero;rotation=Quaternion.identity;if(hand<0||hand>1||remoteWeapons[hand]==null||!remoteWeapons[hand].gameObject.activeInHierarchy)return false;var model=remoteWeapons[hand];point=model.TransformPoint(remoteBores[hand]);rotation=model.rotation;return true;}
        void DisposeRemotePresentation(){if(remoteGhost!=null)remoteGhost.Restore();foreach(var mesh in rescueCoronas)if(mesh!=null)Destroy(mesh);}
    }
}
#endif

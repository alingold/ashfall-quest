#if UNITY_6000_0_OR_NEWER
using System;using UnityEngine;using Ashfall.Integration.Playable.HandsWeapons;
namespace Ashfall.Integration.Playable.Gallery {
 public sealed partial class R23LobbyWeaponGallery {
  // Pure presentation view of the same accepted private gallery owners. Never
  // equips carried inventory, samples input or writes tracked/controller poses.
  public bool TryReadHandArt(int hand,out R23HandArtFrame frame){
   frame=new R23HandArtFrame();if(!Active||holder<0||hand<0||hand>1||!IsWeaponReady(ViewedWeaponId))return false;
   if(DualSelected){Vector3 at;Quaternion rotation;if(GalleryDual==null||!GalleryDual.View.TryReadGloveContact(dualSession.Owner,hand==holder?0:1,out at,out rotation))return false;frame=new R23HandArtFrame{Profile=ViewedWeaponId,Position=at,Rotation=rotation};return true;}
   if(smawSelected){if(hand!=holder||smawPreview==null)return false;R23ShotPose contact;if(!smawPreview.TryReadPrimaryGloveContact(out contact))return false;frame=new R23HandArtFrame{Profile="smaw",Position=new Vector3(contact.Origin.X,contact.Origin.Y,contact.Origin.Z),Rotation=Quaternion.LookRotation(new Vector3(contact.Forward.X,contact.Forward.Y,contact.Forward.Z),new Vector3(contact.Up.X,contact.Up.Y,contact.Up.Z))*Quaternion.Inverse(R23TrackedGripPose.LocalRotation)*R23TrackedGripPose.GloveLocalRotation(hand)};return true;}
   Transform model=null;bool supported=false;
   if(magnumSelected){model=magnumModel.HandArtModel;supported=magnumSession.Owner!=null&&magnumSession.Owner.Snapshot.Supported;}
   else if(m1014Selected){model=m1014Model.HandArtModel;supported=m1014Session.Owner!=null&&m1014Session.Owner.Snapshot.Supported;}
   else if(dt11Selected){model=dt11Model;supported=dt11Session.Owner!=null&&dt11Session.Owner.Snapshot.Supported;}
   else if(awpSelected){model=awpModel;supported=awpSession.Owner!=null&&awpSession.Owner.Snapshot.Supported;}
   else if(m60Selected){model=m60Model;supported=m60Session.Owner!=null&&m60Session.Owner.Snapshot.Supported;}
   else if(knifeSelected){model=knifeRoot;supported=knifeOwner!=null&&knifeOwner.Supported;}
   else{R23GalleryModel selected;if(!models.TryGetValue(ViewedWeaponId,out selected))return false;model=selected.Model;supported=session.State.HasValue&&session.State.Value.Supported;}
   if(model==null||!model.gameObject.activeInHierarchy||hand!=holder&&!supported)return false;
   frame=hand==holder?R23HandArtFit.Primary(ViewedWeaponId,model,Mathf.Abs(model.lossyScale.x),hand):R23HandArtFit.Support(ViewedWeaponId,model,hand);return true;
  }
  // Shared carried pose math; never advances feedback, input or native owners.
  static R23ShotPose RefreshGalleryRecoil(Transform root,Transform model,Ashfall.Core.Float3 sourceMuzzle,float scale,R23WeaponFeedbackState feedback,bool upright){var q=root.rotation;var neutral=new R23ShotPose(F(root.position),F(q*Vector3.right),F(q*Vector3.up),F(q*Vector3.forward));R23ShotPose display,shot;R23WeaponPoseMath.Resolve(neutral,sourceMuzzle,scale,feedback.Distance,feedback.PitchRadians,feedback.YawRadians,upright,out display,out shot);model.SetPositionAndRotation(U(display.Origin),Quaternion.LookRotation(U(display.Forward),U(display.Up)));return shot;}
  [BeforeRenderOrder(1050)]void RefreshLateGalleryHandArt(){try{
   if(!isActiveAndEnabled||!Active||holder<0)return;
   if(DualSelected){if(GalleryDual!=null)GalleryDual.RefreshTrackedPresentation();return;}
   if(smawSelected&&smawPreview!=null){var main=holder==0?left:right;var support=holder==0?right:left;var q=main.rotation*R23TrackedGripPose.LocalRotation;var pose=new R23ShotPose(F(main.position),F(q*Vector3.right),F(q*Vector3.up),F(q*Vector3.forward));smawPreview.RefreshTrackedPresentation(pose,F(support.position),holder,head);return;}
   if(dt11Selected&&dt11Session.Owner!=null){RefreshGalleryRecoil(dt11Root,dt11Model,R24Dt11Definition.Muzzle,R24Dt11Definition.Scale,dt11Recoil,true);dt11Feedback.PresentFlash(presentationFocused&&Time.unscaledTime<dt11FlashUntil,dt11View.Muzzle,.10f*R24Dt11Definition.Scale,.08f*R24Dt11Definition.Scale);return;}
   if(awpSelected&&awpSession.Owner!=null){RefreshGalleryRecoil(awpRoot,awpModel,R24AwpDefinition.Muzzle,1,awpFeedback,false);awpGunFeedback.PresentFlash(presentationFocused&&Time.unscaledTime<awpFlashUntil,awpView.Muzzle);return;}
   if(m60Selected&&m60Session.Owner!=null){RefreshGalleryRecoil(m60Root,m60Model,R24M60Definition.Muzzle,1,m60Feedback,false);m60GunFeedback.PresentFlash(presentationFocused&&Time.unscaledTime<m60FlashUntil,m60View.Flash);return;}
   Transform muzzle=null;float scale=1;bool compact=false;
   if(magnumSelected&&magnumSession.Owner!=null){magnumModel.RefreshTrackedPresentation();muzzle=magnumModel.Muzzle;}
   else if(m1014Selected&&m1014Session.Owner!=null)muzzle=m1014Model.Muzzle;
   else{R23GalleryModel selected;if(!models.TryGetValue(ViewedWeaponId,out selected)||!session.Live)return;selected.RefreshTrackedPresentation();muzzle=selected.Muzzle;bool fitted=ViewedWeaponId=="ump"||ViewedWeaponId=="p90"||ViewedWeaponId=="mp5"||ViewedWeaponId=="mp9"||ViewedWeaponId=="1911"||ViewedWeaponId=="tec9"||ViewedWeaponId=="rfb"||ViewedWeaponId=="fal"||ViewedWeaponId=="s85";scale=fitted?selected.Definition.ModelScale:1;compact=ViewedWeaponId=="ump"||ViewedWeaponId=="p90";}
   // The Magnum view bore inherits .84 model scale; carried feedback uses a
   // unit bore. Preserve the same physical flash size on either parent frame.
   float boreScale=Mathf.Abs(muzzle.lossyScale.x);
   if(previewGunFeedback!=null&&boreScale>.000001f)previewGunFeedback.PresentFlash(presentationFocused&&Time.unscaledTime<previewFlashUntil,muzzle,(compact?.055f:.07f)*scale/boreScale,(compact?.015f:.018f)*scale/boreScale);
  }finally{RefreshGalleryTracers();}}
 }
}
#endif

#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;using UnityEngine;
namespace Ashfall.Integration.Playable.Gallery
{
 public sealed partial class R23LobbyWeaponGallery
 {
  struct GalleryTraceBinding {public string Profile;public int Holder,PhysicalHand;}
  readonly Dictionary<LineRenderer,GalleryTraceBinding> galleryTracerBindings=new Dictionary<LineRenderer,GalleryTraceBinding>();
  void PresentGalleryTracer(LineRenderer line,Vector3 acceptedOrigin,Vector3 acceptedEnd,int physicalHand=-1)
  {
   line.SetPosition(0,acceptedOrigin);line.SetPosition(1,acceptedEnd);line.enabled=true;
   if(awpSelected&&awpView!=null&&awpView.Scope!=null)awpView.Scope.BindTracer(line);
   galleryTracerBindings[line]=new GalleryTraceBinding{Profile=ViewedWeaponId,Holder=holder,PhysicalHand=physicalHand};
  }
  // Cosmetic near-end transport only, matching the carried tracer fix. Keep the
  // accepted far endpoint and original lifetime; no new ray, shot or input sample.
  void RefreshGalleryTracers()
  {
   foreach(var pair in galleryTracerBindings){var line=pair.Key;var binding=pair.Value;if(line==null)continue;
    Vector3 muzzle;if(!Active||holder<0||holder!=binding.Holder||ViewedWeaponId!=binding.Profile||Time.unscaledTime>=traceUntil||!TryReadDisplayedTracerMuzzle(binding.PhysicalHand,out muzzle)){line.enabled=false;continue;}
    if(line.enabled)line.SetPosition(0,muzzle);
   }
  }
  public bool TryReadDisplayedTracerMuzzle(int physicalHand,out Vector3 point)
  {
   point=Vector3.zero;if(!Active||holder<0||!IsWeaponReady(ViewedWeaponId))return false;Transform muzzle=null;
   if(DualSelected){if(GalleryDual==null||dualSession.Owner==null||physicalHand<0||physicalHand>1)return false;int side=dualSession.Owner.ReadHand(0).PhysicalHand==physicalHand?0:1;if(dualSession.Owner.ReadHand(side).PhysicalHand!=physicalHand)return false;muzzle=GalleryDual.View.Muzzle(side);}
   else if(m1014Selected)muzzle=m1014Model.Muzzle;
   else if(magnumSelected)muzzle=magnumModel.Muzzle;
   else if(dt11Selected)muzzle=dt11View.Muzzle;
   else if(awpSelected)muzzle=awpView.Muzzle;
   else if(m60Selected)muzzle=m60View.Muzzle;
   else{
    R23GalleryModel selected;if(!models.TryGetValue(ViewedWeaponId,out selected)||!selected.Model.gameObject.activeInHierarchy)return false;
    muzzle=selected.Muzzle;
   }
   if(muzzle==null||!muzzle.gameObject.activeInHierarchy)return false;point=muzzle.position;return true;
  }
  public void RefreshTracerPresentation(){RefreshLateGalleryHandArt();}
  void LateUpdate(){RefreshLateGalleryHandArt();}
 }
}
#endif

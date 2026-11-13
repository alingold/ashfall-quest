#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable.HandsWeapons {
 public struct R23HandArtFrame {public string Profile;public bool Melee;public Vector3 Position;public Quaternion Rotation;}
 public delegate bool R23HandArtReader(int hand,out R23HandArtFrame frame);
 public sealed partial class R23HandsWeapons {
  R23HandArtReader externalHandArt;
  public void SetExternalHandArtReader(R23HandArtReader reader){externalHandArt=reader;}
  bool ReadExternalHandArt(int hand,out R23HandArtFrame frame){frame=new R23HandArtFrame();return externalHandArt!=null&&externalHandArt(hand,out frame);}
 }
 public static class R23HandArtFit {
  public static R23HandArtFrame Primary(string profile,Transform actualModel,float sourceScale,int hand){var fit=R23WeaponGripFit.ForProfile(profile);var point=fit.ModelContact(sourceScale);return new R23HandArtFrame{Profile=profile,Melee=R24MeleeProfiles.ForId(profile)!=null,Position=actualModel.TransformPoint(new Vector3(point.X,point.Y,point.Z)),Rotation=actualModel.rotation*Quaternion.AngleAxis(fit.PitchRadians*Mathf.Rad2Deg,Vector3.right)*Quaternion.Inverse(R23TrackedGripPose.LocalRotation)*R23TrackedGripPose.GloveLocalRotation(hand)};}
  public static R23HandArtFrame Support(string profile,Transform actualModel,int hand){bool melee=R24MeleeProfiles.ForId(profile)!=null;var p=melee?R24MeleeRuntime.SupportPoint:profile=="magnum"||profile=="px4"?new Ashfall.Core.Float3(0,-.065f,.095f):new Ashfall.Core.Float3(0,-.025f,.36f);return new R23HandArtFrame{Profile=profile,Melee=melee,Position=actualModel.TransformPoint(new Vector3(p.X,p.Y,p.Z)),Rotation=R23TrackedGripPose.SupportArtRotation(actualModel.rotation,hand)};}
 }
}
#endif

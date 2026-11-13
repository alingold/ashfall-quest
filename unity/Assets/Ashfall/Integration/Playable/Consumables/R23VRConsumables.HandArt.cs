#if UNITY_6000_0_OR_NEWER
using UnityEngine;using Ashfall.Integration.Playable.HandsWeapons;
namespace Ashfall.Integration.Playable.Consumables {
 public sealed partial class R23VRConsumables {
  // User-requested three inches downward in the held drink's frame. Presentation
  // only: neither prop/cap geometry nor consumption/reach/ownership is displaced.
  public const float DrinkHandDrop=.0762f;
  public bool TryReadHeldHandArt(int hand,out R23HandArtFrame frame){frame=new R23HandArtFrame();WorldItem item;if(hand<0||hand>1||HeldHand!=hand+1||HeldId==null||!items.TryGetValue(HeldId,out item)||item.View==null||!item.View.activeInHierarchy||(item.Kind!="soda"&&item.Kind!="shaker"))return false;var raw=Hand(HeldHand);var corrected=raw.rotation*R23TrackedGripPose.LocalRotation;frame=new R23HandArtFrame{Profile=item.Kind,Position=raw.position-corrected*Vector3.up*DrinkHandDrop,Rotation=raw.rotation*R23TrackedGripPose.GloveLocalRotation(hand)};return true;}
  [BeforeRenderOrder(1050)]void RefreshTrackedHeldPresentation(){if(isActiveAndEnabled&&HeldId!=null)UpdateHeldPose();}
 }
}
#endif

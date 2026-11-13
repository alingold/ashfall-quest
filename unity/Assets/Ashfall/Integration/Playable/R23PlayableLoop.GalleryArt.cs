#if UNITY_6000_0_OR_NEWER
using Ashfall.Integration.Playable.HandsWeapons;
namespace Ashfall.Integration.Playable {
 public sealed partial class R23PlayableLoop {
  bool ReadExternalItemHandArt(int hand,out R23HandArtFrame frame){frame=new R23HandArtFrame();if(Lobby!=null&&Lobby.Visible&&Gallery!=null&&Gallery.TryReadHandArt(hand,out frame))return true;return Consumables!=null&&Consumables.TryReadHeldHandArt(hand,out frame);}
 }
}
#endif

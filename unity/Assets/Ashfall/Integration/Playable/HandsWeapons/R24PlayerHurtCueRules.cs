namespace Ashfall.Integration.Playable.HandsWeapons
{
 // main.gd owns eight actor-level SFX voices at -12dB; weapon input pause or
 // holstering does not cancel accepted player damage. No cooldown drops hits.
 public sealed class R24PlayerHurtCueRules
 {
  public const int VoiceCount=8;
  public const float SourceGain=.25118864f;
  int cursor;
  public static bool ShouldPlay(bool ready,bool acceptedDamage,bool focused,bool active)
  {return ready&&acceptedDamage&&focused&&active;}
  public int NextSlot(){int selected=cursor;cursor=(cursor+1)%VoiceCount;return selected;}
  public void Reset(){cursor=0;}
 }
}

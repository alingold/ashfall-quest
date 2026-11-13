#if UNITY_6000_0_OR_NEWER
namespace Ashfall.Integration.Playable
{
 public sealed partial class R23PlayableLoop
 {
  float flashWorldGain=1;
  void SetFlashWorldAudio(float gain)
  {
   // Reapply saved personal levels, never divide previously ducked gains.
   if(flashWorldGain==gain)return;
   flashWorldGain=gain;
   if(UI==null)return;
   float sfx=UI.Settings.SfxVolume*gain;
   if(Calibration!=null)Calibration.SetSfxVolume(sfx);
   if(Map!=null)Map.SetSfxVolume(sfx);
   if(TeamLaunch!=null)TeamLaunch.SetSfxVolume(sfx);
   if(Explosives!=null)Explosives.SetSfxVolume(sfx);
   if(JumpPads!=null)JumpPads.SetSfxVolume(sfx);
   if(Paddles!=null)Paddles.SetSfxVolume(sfx);
   if(Supplies!=null)Supplies.SetSfxVolume(sfx);
   if(Gallery!=null)Gallery.SetSfxVolume(sfx);
   if(TraversalAudio!=null)TraversalAudio.SetSfxVolume(sfx);
   if(TeamMarks!=null)TeamMarks.SetSfxVolume(sfx);
   if(HealthStations!=null)HealthStations.SetSfxVolume(sfx);
   if(Gameplay!=null)Gameplay.SetSfxVolume(sfx);
   if(Consumables!=null)Consumables.SetSfxVolume(sfx);
   if(Activities!=null)Activities.SetSfxVolume(sfx);
   if(ValleyBots!=null)ValleyBots.SetSfxVolume(sfx);
   if(Soundscape!=null)Soundscape.SetVolumes(UI.Settings.MusicVolume*gain,UI.Settings.AmbienceVolume*gain);
  }
 }
}
#endif

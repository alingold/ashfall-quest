using System;
namespace Ashfall.Integration.Playable.MatchZone
{
 // Keep the requested five-metre warning on the safe side. The source outside
 // bed remains audible throughout the damaging side, with authored -15/-11dB.
 public struct R24ZoneAudioLevels
 {
  public readonly float Edge,Danger;
  public R24ZoneAudioLevels(double edgeDistance,bool headOutside,bool rootOutside,float sfx)
  {
   Edge=Danger=0;
   if(double.IsNaN(edgeDistance)||double.IsInfinity(edgeDistance)||edgeDistance<0||float.IsNaN(sfx)||float.IsInfinity(sfx))return;
   double volume=Math.Max(0,Math.Min(1,sfx)),t=Math.Max(0,Math.Min(1,edgeDistance/5));
   Edge=(float)(Math.Pow(10,-9/20.0)*(1-t*t*(3-2*t))*volume);
   Danger=headOutside||rootOutside?(float)(Math.Pow(10,(edgeDistance<90?-15:-11)/20.0)*volume):0;
  }
 }
}

using System;
namespace Ashfall.Integration.Playable.Match {
 // Actual local lifecycle clock; there is one real player, no synthetic peers or
 // practice-target scores. Root owns state/motion/health composition around it.
 public sealed class R24LocalMatchClock {
  public double Elapsed{get;private set;}public long Epoch{get;private set;}public int Round{get;private set;}public bool Active{get;private set;}public bool Results{get;private set;}public int TotalRounds{get;private set;}public int TimeLimit{get;private set;}
  public void Begin(int timeLimit,int rounds){if(timeLimit<0||timeLimit>3600||rounds<1||rounds>10)throw new ArgumentOutOfRangeException();TimeLimit=timeLimit;TotalRounds=rounds;Round=1;Elapsed=0;Epoch++;Active=true;Results=false;}
  public bool Advance(double seconds){if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<0)throw new ArgumentOutOfRangeException();if(!Active)return false;Elapsed+=seconds;return TimeLimit>0&&Elapsed>=TimeLimit;}
  public void BindNetworkRound(int round,double elapsed){if(round<1||round>TotalRounds||double.IsNaN(elapsed)||double.IsInfinity(elapsed)||elapsed<0)throw new ArgumentOutOfRangeException();Round=round;Elapsed=elapsed;}
  public bool Synchronize(double elapsed){if(double.IsNaN(elapsed)||double.IsInfinity(elapsed)||elapsed<0)throw new ArgumentOutOfRangeException();if(!Active)return false;Elapsed=Math.Max(Elapsed,elapsed);return TimeLimit>0&&Elapsed>=TimeLimit;}
  public void Finish(){if(!Active)return;Active=false;Results=true;}public bool NextRound(){if(!Results||Round>=TotalRounds)return false;Round++;Epoch++;Elapsed=0;Active=true;Results=false;return true;}
  public void Restart(){if(TotalRounds<1)return;Round=1;Epoch++;Elapsed=0;Active=true;Results=false;}public void End(){Active=false;Results=false;Elapsed=0;Round=0;Epoch++;}
 }
}

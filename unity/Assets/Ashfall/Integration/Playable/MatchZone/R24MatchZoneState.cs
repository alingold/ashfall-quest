using System;using System.Collections.Generic;using Ashfall.Core;
namespace Ashfall.Integration.Playable.MatchZone {
 // All square coordinates here are ORIGINAL GODOT MAP-LOCAL X/Z. Unity reflects
 // Z exactly once at its presentation boundary. No player/root transform writes.
 public struct R24ZoneSquare {
  public readonly double X,Z,Size;public double MaxX{get{return X+Size;}}public double MaxZ{get{return Z+Size;}}
  public R24ZoneSquare(double x,double z,double size){if(!R24ZoneMath.Finite(x)||!R24ZoneMath.Finite(z)||!R24ZoneMath.Finite(size)||size<=0)throw new ArgumentOutOfRangeException("Finite positive source square required");X=x;Z=z;Size=size;}
  public bool Contains(double x,double z){return x>=X&&x<=MaxX&&z>=Z&&z<=MaxZ;}
 }
 public sealed class R24ZoneRules {
  public readonly bool Enabled;public readonly double DamagePerSecond,EdgeSpeed,WaitSeconds;
  public R24ZoneRules(bool enabled=true,double damagePerSecond=5,double edgeSpeed=2,double waitSeconds=30){if(!R24ZoneMath.Finite(damagePerSecond)||!R24ZoneMath.Finite(edgeSpeed)||!R24ZoneMath.Finite(waitSeconds)||damagePerSecond<0||edgeSpeed<0||waitSeconds<0)throw new ArgumentOutOfRangeException("Actual finite source zone rules required");Enabled=enabled;DamagePerSecond=damagePerSecond;EdgeSpeed=edgeSpeed;WaitSeconds=waitSeconds;}
 }
 public sealed class R24ZonePlan {
  public readonly R24ZoneSquare From,Next;public readonly double HoldUntil,Ends;public readonly int Index;public readonly bool Final;
  public R24ZonePlan(R24ZoneSquare from,R24ZoneSquare next,double holdUntil,double ends,int index,bool final){if(!R24ZoneMath.Finite(holdUntil)||!R24ZoneMath.Finite(ends)||ends<holdUntil||index<0||next.X<from.X-.001||next.Z<from.Z-.001||next.MaxX>from.MaxX+.001||next.MaxZ>from.MaxZ+.001)throw new ArgumentException("Actual timestamped nested square plan required");From=from;Next=next;HoldUntil=holdUntil;Ends=ends;Index=index;Final=final;}
  public R24ZoneSquare Current(double time){if(!R24ZoneMath.Finite(time))throw new ArgumentOutOfRangeException("time");double f=Math.Max(0,Math.Min(1,(time-HoldUntil)/Math.Max(.001,Ends-HoldUntil)));return new R24ZoneSquare(From.X+(Next.X-From.X)*f,From.Z+(Next.Z-From.Z)*f,From.Size+(Next.Size-From.Size)*f);}
 }
 public static class R24ZoneMath {
  public const double Ratio=.65,MinimumSize=12,NextRibbonWidth=.3048,CurrentHalfWidth=.60;
  public static bool Finite(double n){return !double.IsNaN(n)&&!double.IsInfinity(n);}
  public static R24ZoneSquare Initial(double minX,double minZ,double maxX,double maxZ){if(!Finite(minX)||!Finite(minZ)||!Finite(maxX)||!Finite(maxZ)||maxX<minX||maxZ<minZ)throw new ArgumentException("Actual catalog bounds required");double spanX=maxX-minX,spanZ=maxZ-minZ,size=Math.Max(52,Math.Max(spanX,spanZ))+2;return new R24ZoneSquare(minX+(spanX-size)*.5,minZ+(spanZ-size)*.5,size);}
  public static R24ZonePlan Plan(R24ZoneSquare from,R24ZoneRules rules,double time,int index,Func<double> randomUnit){if(rules==null||randomUnit==null||!Finite(time))throw new ArgumentNullException();double size=Math.Max(MinimumSize,from.Size*Ratio),a=randomUnit(),b=randomUnit();if(!Finite(a)||!Finite(b)||a<0||a>1||b<0||b>1)throw new ArgumentOutOfRangeException("Actual host RNG must supply uniform [0,1]");double range=Math.Max(0,from.Size-size);var next=new R24ZoneSquare(from.X+a*range,from.Z+b*range,size);double travel=Math.Max(Math.Max(Math.Abs(next.X-from.X),Math.Abs(next.Z-from.Z)),Math.Max(Math.Abs(next.MaxX-from.MaxX),Math.Abs(next.MaxZ-from.MaxZ))),hold=time+rules.WaitSeconds;return new R24ZonePlan(from,next,hold,hold+travel/Math.Max(.1,rules.EdgeSpeed),index,from.Size<=MinimumSize+.001);}
  public static R24ZonePlan Advance(R24ZonePlan state,R24ZoneRules rules,double time,Func<double> randomUnit){if(state==null||state.Final||time<state.Ends)return state;for(int i=0;i<32;i++){state=Plan(state.Next,rules,state.Ends,state.Index+1,randomUnit);if(state.Final||time<state.Ends)break;}return state;}
  public static double BoundaryDistance(R24ZoneSquare r,double x,double z){double qx=Math.Abs(x-(r.X+r.Size*.5))-r.Size*.5,qz=Math.Abs(z-(r.Z+r.Size*.5))-r.Size*.5;return Math.Abs(Math.Sqrt(Math.Max(qx,0)*Math.Max(qx,0)+Math.Max(qz,0)*Math.Max(qz,0))+Math.Min(Math.Max(qx,qz),0));}
  public static void NearestEdge(R24ZoneSquare r,double x,double z,out double edgeX,out double edgeZ){double cx=Math.Max(r.X,Math.Min(r.MaxX,x)),cz=Math.Max(r.Z,Math.Min(r.MaxZ,z));edgeX=r.X;edgeZ=cz;double best=DistanceSquared(x,z,edgeX,edgeZ);Check(r.MaxX,cz,x,z,ref best,ref edgeX,ref edgeZ);Check(cx,r.Z,x,z,ref best,ref edgeX,ref edgeZ);Check(cx,r.MaxZ,x,z,ref best,ref edgeX,ref edgeZ);}
  static void Check(double x,double z,double px,double pz,ref double best,ref double ex,ref double ez){double d=DistanceSquared(px,pz,x,z);if(d<best){best=d;ex=x;ez=z;}}static double DistanceSquared(double a,double b,double c,double d){return(a-c)*(a-c)+(b-d)*(b-d);}
 }
 public sealed class R24ZoneDamage {
  public readonly long Tick;public readonly string ActorId;public readonly float Amount;public readonly double ElapsedSeconds;public bool BypassShield{get{return true;}}public bool BypassGenericDamageMultiplier{get{return true;}}public string Cause{get{return "zone";}}
  internal R24ZoneDamage(long tick,string actor,double elapsed,double rate){Tick=tick;ActorId=actor;ElapsedSeconds=elapsed;Amount=(float)(rate*elapsed);}
 }
 // The SAME host owner is usable by offline and actual listen-host composition.
 // A network transport is neither created nor simulated here. Clients present an
 // authoritative Plan; only the real host ticks this owner and requests damage.
 public sealed class R24MatchZoneHost {
  readonly Func<double> random;readonly HashSet<string> actors=new HashSet<string>(StringComparer.Ordinal);double accumulator,damageDt,time;long tick;bool active;
  public R24ZoneRules Rules{get;private set;}public R24ZonePlan State{get;private set;}public double Time{get{return time;}}public long DamageTick{get{return tick;}}public double DamageInterval{get{return damageDt;}}
  public R24MatchZoneHost(R24ZoneRules rules,double minX,double minZ,double maxX,double maxZ,double startTime,Func<double> actualHostUniformRandom){if(rules==null||actualHostUniformRandom==null||!R24ZoneMath.Finite(startTime))throw new ArgumentException();Rules=rules;random=actualHostUniformRandom;time=startTime;State=rules.Enabled?R24ZoneMath.Plan(R24ZoneMath.Initial(minX,minZ,maxX,maxZ),rules,startTime,0,random):null;}
  public void SetLiveDamage(double damagePerSecond){Rules=new R24ZoneRules(Rules.Enabled,damagePerSecond,Rules.EdgeSpeed,Rules.WaitSeconds);}
  public bool Tick(double actualHostTime,double deltaSeconds,bool matchActive){if(!R24ZoneMath.Finite(actualHostTime)||!R24ZoneMath.Finite(deltaSeconds)||deltaSeconds<0)throw new ArgumentOutOfRangeException();active=matchActive;time=actualHostTime;damageDt=0;if(!active)return false;accumulator+=deltaSeconds;if(accumulator<.10)return false;damageDt=accumulator;accumulator=0;tick++;actors.Clear();State=R24ZoneMath.Advance(State,Rules,time,random);return true;}
  public bool RequestDamage(string actualActor,Float3 godotMapLocalRoot,bool loaded,bool alive,double protectionUntil,out R24ZoneDamage request){request=null;if(!active||State==null||damageDt<=0||!loaded||!alive||!godotMapLocalRoot.IsFinite||!R24ZoneMath.Finite(protectionUntil)||time<protectionUntil||string.IsNullOrEmpty(actualActor)||actors.Contains(actualActor)||State.Current(time).Contains(godotMapLocalRoot.X,godotMapLocalRoot.Z)||Rules.DamagePerSecond<=0)return false;actors.Add(actualActor);request=new R24ZoneDamage(tick,actualActor,damageDt,Rules.DamagePerSecond);return true;}
 }
}

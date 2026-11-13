using System;
namespace Ashfall.Integration.Playable.Bots
{
 // Source enemy.gd voice policy; pure so routing/gain/counters are testable
 // without launching Unity or constructing substitute audio/Health services.
 public sealed class R24KestrelAudioRules
 {
  public const float UnitSize=9,MaxDb=3;
  public const int Polyphony=2;
  public const double SourceMaxLinear=1.4125375446227544;
  long variation;
  public long Variation {get{return variation;}}
  public struct Request {public readonly string Kind,Semantic;public readonly int Index;public readonly bool Impact;public Request(string kind,string semantic,int index,bool impact){Kind=kind;Semantic=semantic;Index=index;Impact=impact;}}
  public static string Semantic(string kind){switch(kind){case "fire":return "enemy_fire";case "charge":return "enemy_charge";case "death":return "enemy_break";default:return "impact_metal";}}
  public static string Kind(string kind){switch(kind){case "fire":case "charge":case "death":return kind;default:return "hit";}}
  public static int Count(string kind){return kind=="charge"?2:3;}
  public Request Next(string kind,bool impact){string canonical=Kind(kind);int count=Count(canonical);int index=(int)(variation%count);if(index<0)index+=count;variation=unchecked(variation+1);return new Request(canonical,Semantic(kind),index,impact);}
  public static float Pitch(int variant){switch(variant){case 0:return 1;case 1:return 1.16f;case 2:return .83f;case 3:return .68f;default:throw new ArgumentOutOfRangeException("variant");}}
  public static float MaxDistance(float engageRange){if(float.IsNaN(engageRange)||float.IsInfinity(engageRange)||engageRange<0)throw new ArgumentOutOfRangeException("engageRange");double max=Math.Max(48,engageRange*1.15);if(max>float.MaxValue)throw new ArgumentOutOfRangeException("engageRange");return(float)max;}
  public static double VoiceGain(bool impact){return Math.Pow(10,(impact?-12.0:-9.0)/20);}
  // Godot adds voice dB BEFORE its +3dB absolute cap. Maxdistance also
  // multiplies a linearfade. UI SFX bus is a separate linear gain afterwards.
  public static double DbToLinear(double db){if(double.IsNaN(db)||double.IsInfinity(db))throw new ArgumentOutOfRangeException("db");return Math.Pow(10,db/20);}
  public static double EvaluateAttenuation(double distance,double unit,double maxDistance,double volumeDb,double maxDb){if(double.IsNaN(distance)||double.IsInfinity(distance)||distance<0||double.IsNaN(unit)||double.IsInfinity(unit)||unit<=0||double.IsNaN(maxDistance)||double.IsInfinity(maxDistance)||maxDistance<0)throw new ArgumentOutOfRangeException("distance/unit/maxDistance");double gain=DbToLinear(volumeDb)/(distance/unit+.00001);double cap=DbToLinear(maxDb);if(maxDistance>0&&distance>=maxDistance)return 0;return Math.Min(cap,gain)*(maxDistance>0?Math.Max(0,1-distance/maxDistance):1);}
  public static double SourceGain(double distance,double maximum,bool impact){return EvaluateAttenuation(distance,UnitSize,maximum,impact?-12:-9,MaxDb);}
  public struct AttenuationPoint {public readonly float Time,Gain,InTangent,OutTangent;public AttenuationPoint(float time,float gain,float incoming,float outgoing){Time=time;Gain=gain;InTangent=incoming;OutTangent=outgoing;}}
  static double Slope(double normalized,double unit,double maximum,double voice,double cap){double a=maximum/unit;double divisor=a*normalized+.00001;return -(voice/cap)*(a+.00001)/(divisor*divisor);}
  public static AttenuationPoint[] AttenuationCurvePoints(float unit,float maxDistance,float volumeDb,float maxDb){double cap=DbToLinear(maxDb),voice=DbToLinear(volumeDb);EvaluateAttenuation(0,unit,maxDistance,volumeDb,maxDb);if(maxDistance<=0)throw new ArgumentOutOfRangeException("maxDistance","Unity finite custom curve requires a positive source maximum");double boundary=unit*(voice/cap-.00001);double start=Math.Max(maxDistance*.00000001,Math.Min(boundary,maxDistance));const int segments=128;var points=new AttenuationPoint[start>=maxDistance?2:segments+2];double firstSlope=boundary>0?-1:Slope(0,unit,maxDistance,voice,cap);points[0]=new AttenuationPoint(0,(float)(EvaluateAttenuation(0,unit,maxDistance,volumeDb,maxDb)/cap),(float)firstSlope,(float)firstSlope);if(points.Length==2)points[1]=new AttenuationPoint(1,0,-1,0);else for(int i=0;i<=segments;i++){double distance=start*Math.Pow(maxDistance/start,i/(double)segments),normalized=distance/maxDistance;double slope=Slope(normalized,unit,maxDistance,voice,cap);points[i+1]=new AttenuationPoint((float)normalized,(float)(EvaluateAttenuation(distance,unit,maxDistance,volumeDb,maxDb)/cap),(float)(i==0&&boundary>0?-1:slope),(float)slope);}return points;}
  public static double CurveGain(double normalizedDistance,double maximum,bool impact){return SourceGain(normalizedDistance*maximum,maximum,impact)/SourceMaxLinear;}
  public static double CapDistance(bool impact){return UnitSize*(VoiceGain(impact)/SourceMaxLinear-.00001);}
 }
}

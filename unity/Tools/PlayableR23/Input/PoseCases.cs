using System;
using Ashfall.Integration.Playable.Input;
class PoseCases
{
 static void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
 static void Main(){
  R23AimPoseSample result;
  Check(R23AimPoseRules.TryNormalize(new R23AimPoseSample(true,3,.2f,1.4f,-.3f,0,0,0,1),out result)&&result.X==.2f&&result.Y==1.4f&&result.Z==-.3f&&result.Qx==0&&result.Qy==0&&result.Qz==0&&result.Qw==1,"stage-local aim preserved exactly; no fixed grip rotation or positional offset");
  Check(R23AimPoseRules.TryNormalize(new R23AimPoseSample(true,3,0,0,0,0,1,0,1),out result)&&Math.Abs(result.Qy-(float)Math.Sqrt(.5))<.00001f&&Math.Abs(result.Qw-(float)Math.Sqrt(.5))<.00001f,"aim quaternion normalized without changing supplied orientation");
  Check(!R23AimPoseRules.TryNormalize(new R23AimPoseSample(false,3,0,1,0,0,0,0,1),out result),"untracked pointer never falls back to tracked grip");
  Check(!R23AimPoseRules.TryNormalize(new R23AimPoseSample(true,1,0,1,0,0,0,0,1),out result)&&!R23AimPoseRules.TryNormalize(new R23AimPoseSample(true,2,0,1,0,0,0,0,1),out result),"partial pointer position/rotation tracking fails closed");
  Check(!R23AimPoseRules.TryNormalize(new R23AimPoseSample(true,3,0,1,0,0,0,0,0),out result)&&!R23AimPoseRules.TryNormalize(new R23AimPoseSample(true,3,float.NaN,1,0,0,0,0,1),out result)&&!R23AimPoseRules.TryNormalize(new R23AimPoseSample(true,3,0,1,0,0,float.PositiveInfinity,0,1),out result),"invalid pointer values and zero quaternion rejected");
 }
}

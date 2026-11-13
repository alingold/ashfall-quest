using System;
namespace Ashfall.Integration.Playable.Bots
{
 // Scalar source animation only; actor motion/aim/core gameplay authority stays Root.
 public sealed class R24WardenPose
 {
  public float LeftRotor {get;private set;}public float RightRotor {get;private set;}public float LeftShutter {get;private set;}public float RightShutter {get;private set;}
  public void Step(float delta,bool coreOpen){if(float.IsNaN(delta)||float.IsInfinity(delta)||delta<0||delta>1)throw new ArgumentOutOfRangeException("delta");LeftRotor=Repeat(LeftRotor+delta*22);RightRotor=Repeat(RightRotor-delta*22);LeftShutter=Toward(LeftShutter,coreOpen?-1.15f:0,delta*9);RightShutter=Toward(RightShutter,coreOpen?1.15f:0,delta*9);}
  static float Repeat(float angle){float turn=(float)(Math.PI*2);return angle-(float)Math.Floor(angle/turn)*turn;}
  static float Toward(float from,float to,float amount){return Math.Abs(to-from)<=amount?to:from+Math.Sign(to-from)*amount;}
  public static float WindupGreen(float age){if(float.IsNaN(age)||float.IsInfinity(age))throw new ArgumentOutOfRangeException("age");return .12f+(float)Math.Sin(age*35)*.1f;}
 }
}

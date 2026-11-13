using System;
namespace Ashfall.Integration.Playable.HandsWeapons {
 public sealed class R24AwpFeedback {
  float clock,idle=.8f,distance,pitch,yaw,flashUntil;int pattern;long shot;
  static readonly float[] Vertical={1,.90f,.70f,.85f,1.10f},Yaw={0,-.12f,.18f,.10f,-.20f,-.08f};
  public float Distance{get{return distance*1.25f;}}public float PitchRadians{get{return pitch*1.25f;}}public float YawRadians{get{return yaw*1.25f;}}public bool Flash{get{return clock<flashUntil;}}public long AcceptedShots{get{return shot;}}
  public void Advance(float dt){if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0||dt>.25f)throw new ArgumentOutOfRangeException("dt");clock+=dt;idle+=dt;float recover=(float)Math.Exp(-dt*9);distance*=recover;if(idle>.5f){pitch*=recover;yaw*=recover;}if(idle>=.8f){pattern=0;distance=pitch=yaw=0;}}
  public bool AcceptedShot(long serial,bool supported){if(serial<=shot)return false;shot=serial;float reduction=supported?.38f:1,angle=5.5f*(float)Math.PI/180*reduction;distance=Math.Min(.13f,distance+.052f*reduction);pitch=Math.Min((float)Math.PI/4,pitch+angle*Vertical[pattern%Vertical.Length]);yaw=Math.Max(-80*(float)Math.PI/180,Math.Min(80*(float)Math.PI/180,yaw+angle*Yaw[pattern%Yaw.Length]));pattern++;idle=0;flashUntil=clock+.055f;return true;}
  public void ClearPresentation(){distance=pitch=yaw=0;idle=.8f;pattern=0;flashUntil=clock;}public void Reset(){clock=0;shot=0;ClearPresentation();}
 }
}

using System;using System.Collections.Generic;
namespace Ashfall.Integration.Playable.HandsWeapons {
 // Exact current recoil_observation067 selection/hold/reflect-tail. Source curves
 // contain wrist motion and are not a physical/headset recoil validation.
 public sealed class R24M60Feedback {
  float clock,idle=.8f,distance,pitch,yaw,burstTime,flashUntil;bool active,shotSupported;long shot;readonly List<float> stamps=new List<float>();
  public float Distance{get{return distance*1.25f;}}public float PitchRadians{get{return pitch*1.25f;}}public float YawRadians{get{return yaw*1.25f;}}public bool Flash{get{return clock<flashUntil;}}public long AcceptedShots{get{return shot;}}
  public float Bloom{get{float sum=0;foreach(float stamp in stamps){float age=(clock-stamp)/.063f;sum+=67*age*(float)Math.Exp(1-age);}return Math.Min(106,sum);}}
  public float ReticleWidth(bool supported){return(supported?58:98)+Bloom;}
  public void Advance(float dt){if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0||dt>.25f)throw new ArgumentOutOfRangeException("dt");clock+=dt;idle+=dt;float decay=(float)Math.Exp(-dt*9);distance*=decay;if(active){if(idle<=.32f){burstTime+=dt;Sample();}else{pitch*=decay;yaw*=decay;}}while(stamps.Count>0&&clock-stamps[0]>1.2f)stamps.RemoveAt(0);if(idle>=.8f){distance=pitch=yaw=burstTime=0;active=false;}}
  public bool AcceptedShot(long native,bool supported){if(native<=shot)return false;shot=native;if(!active||idle>.32f)burstTime=0;active=true;shotSupported=supported;distance=Math.Min(.13f,distance+.052f*(supported?.38f:1));idle=0;Sample();stamps.Add(clock);while(stamps.Count>32)stamps.RemoveAt(0);flashUntil=clock+.045f;return true;}
  void Sample(){var values=shotSupported?R24M60RecoilData.Two:R24M60RecoilData.One;int last=values.Length/3-1;float t=Math.Max(0,burstTime),end=values[last*3];if(t>end){float span=Math.Min(end,2.5f),phase=(t-end)%(span*2);t=end-(phase<span?phase:span*2-phase);}int lo=0,hi=last;while(hi-lo>1){int mid=(lo+hi)/2;if(values[mid*3]<=t)lo=mid;else hi=mid;}float f=Math.Max(0,Math.Min(1,(t-values[lo*3])/Math.Max(.000001f,values[hi*3]-values[lo*3])));pitch=(values[lo*3+1]+(values[hi*3+1]-values[lo*3+1])*f)*(float)Math.PI/180;yaw=-(values[lo*3+2]+(values[hi*3+2]-values[lo*3+2])*f)*(float)Math.PI/180;}
  public void ClearPresentation(){idle=.8f;distance=pitch=yaw=burstTime=0;active=false;stamps.Clear();flashUntil=clock;}public void Reset(){clock=0;shot=0;ClearPresentation();}
 }
}

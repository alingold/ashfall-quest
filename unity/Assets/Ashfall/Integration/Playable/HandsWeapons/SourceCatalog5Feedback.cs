using System;
using System.Collections.Generic;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // Source render observation only. Owner shot serials are inputs, never ammunition/effect grants.
    public sealed partial class SourceCatalog5Feedback
    {
        readonly string id;readonly List<float> ages=new List<float>(),peaks=new List<float>();
        static readonly float[] Vertical={1,.28f,.42f,.31f,.55f,.37f},Yaw={0,-.12f,.18f,.10f,-.20f,-.08f};
        float idle=.8f,recoil,pitch,yaw,observedTime,flash;int index;bool active,shotSupported,supported;long lastShot;
        const float Rad=(float)Math.PI/180,Gain=1.25f;
        public SourceCatalog5Feedback(string weaponId)
        {if(weaponId!="1911"&&weaponId!="tec9"&&weaponId!="rfb"&&weaponId!="fal"&&weaponId!="s85")throw new ArgumentException("Catalog5 source profile");id=weaponId;}
        bool PulseProfile {get{return id=="1911"||id=="fal";}}
        float SupportGain {get{return id=="rfb"&&supported?.4f:1;}}
        public float Distance {get{return recoil*Gain*SupportGain;}}
        public float PitchRadians {get{return pitch*Gain*SupportGain;}}
        public float YawRadians {get{return yaw*Gain*SupportGain;}}
        public float SlideTravel {get{return id=="1911"&&idle<.095f?(float)Math.Sin(idle/.095f*Math.PI)*.035f:0;}}
        public bool Flash {get{return flash>0;}}public long AcceptedShots {get{return lastShot;}}
        public bool HasReticleProfile {get{return id!="s85";}}
        public float Bloom {get{if(!HasReticleProfile)return 0;float sum=0;foreach(float age in ages){float a=age/.055f;sum+=32*a*(float)Math.Exp(1-a);}return Math.Min(76,sum);}}
        public float ReticleWidth(bool held){return HasReticleProfile?(held?58:70)+Bloom:0;}
        public void SetSupported(bool held){supported=held;}
        public bool AcceptedShot(long nativeShots,bool held)
        {
            if(nativeShots<=lastShot)return false;lastShot=nativeShots;supported=held;float oldIdle=idle,reduction=held&&id!="rfb"?.38f:1;
            recoil=Math.Min(.13f,recoil+.052f*reduction);idle=0;flash=.045f;
            if(id=="tec9"){if(!active||oldIdle>.32f)observedTime=0;active=true;shotSupported=held;SampleTec9();}
            else if(id=="rfb"){index++;int n=Math.Min(index,RfbOne.Length/2-1);pitch=RfbOne[n*2]*Rad;yaw=-RfbOne[n*2+1]*Rad;}
            else if(!PulseProfile){pitch=Math.Min(45*Rad,pitch+5.5f*Rad*reduction*Vertical[index%Vertical.Length]);yaw=Math.Max(-80*Rad,Math.Min(80*Rad,yaw+5.5f*Rad*reduction*Yaw[index%Yaw.Length]));index++;}
            ages.Add(0);peaks.Add(PulseProfile?6.05f*Rad*(held?.38f:1):0);while(ages.Count>32){ages.RemoveAt(0);peaks.RemoveAt(0);}return true;
        }
        public void Advance(float seconds)
        {
            if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<0||seconds>.25f)throw new ArgumentOutOfRangeException("seconds");
            idle+=seconds;flash=Math.Max(0,flash-seconds);float decay=(float)Math.Exp(-seconds*9*(id=="s85"?2:id=="fal"?1.3f:1));recoil*=decay;
            for(int i=ages.Count-1;i>=0;i--){ages[i]+=seconds;if(ages[i]>1.2f){ages.RemoveAt(i);peaks.RemoveAt(i);}}
            if(PulseProfile){pitch=0;for(int i=0;i<ages.Count;i++)pitch+=peaks[i]*SignedPulse(id,ages[i]);pitch=Math.Max(-6.05f*Rad*1.5f,Math.Min(6.05f*Rad*1.5f,pitch));yaw=0;}
            else if(id=="tec9")
            {if(active){if(idle<=.32f){observedTime+=seconds;SampleTec9();}else{pitch*=decay;yaw*=decay;}}}
            else if(idle>(id=="s85"?.08f:Math.Max(.18f,Math.Min((1f/9.5f)*1.15f,.5f)))){pitch*=decay;yaw*=decay;}
            if(idle>=.8f){index=0;recoil=pitch=yaw=0;observedTime=0;active=false;}
        }
        void SampleTec9()
        {
            var p=shotSupported?Tec9Two:Tec9One;int last=p.Length/3-1;float t=Math.Max(0,Math.Min(observedTime,p[last*3]));
            if(t<=p[0]){pitch=p[1]*Rad;yaw=-p[2]*Rad;return;}
            int lo=0,hi=last;while(hi-lo>1){int m=(lo+hi)/2;if(p[m*3]<=t)lo=m;else hi=m;}
            float weight=Math.Max(0,Math.Min(1,(t-p[lo*3])/Math.Max(.000001f,p[hi*3]-p[lo*3])));pitch=(p[lo*3+1]+(p[hi*3+1]-p[lo*3+1])*weight)*Rad;yaw=-(p[lo*3+2]+(p[hi*3+2]-p[lo*3+2])*weight)*Rad;
        }
        public static float SignedPulse(string id,float age)
        {
            bool pistol=id=="1911";if(!pistol&&id!="fal")throw new ArgumentException("Source signed pulse");
            float end=pistol?.43f:.51f;if(age<=0||age>=end)return 0;
            float[] times={0,pistol?.097f:.125f,pistol?.205f:.270f,pistol?.264f:.333f,end},values={0,1,0,-(pistol?.28f:.16f),0};
            float a=-1/(times[2]-times[1]),b=values[3]/(times[3]-times[2]);float[] slopes={1.8f/times[1],0,2*a*b/(a+b),0,0};
            for(int i=0;i<4;i++)if(age<=times[i+1]){float span=times[i+1]-times[i],u=(age-times[i])/span;return (2*u*u*u-3*u*u+1)*values[i]+(u*u*u-2*u*u+u)*span*slopes[i]+(-2*u*u*u+3*u*u)*values[i+1]+(u*u*u-u*u)*span*slopes[i+1];}return 0;
        }
        public void ClearPresentation(){ages.Clear();peaks.Clear();idle=.8f;recoil=pitch=yaw=observedTime=flash=0;index=0;active=false;}
        public void Reset(){ClearPresentation();lastShot=0;supported=shotSupported=false;}
    }
}

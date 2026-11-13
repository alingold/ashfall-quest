using System;
using System.Collections.Generic;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // Render-only port of original weapon.gd, reticle_bounce_071 and reference_062.
    // Accepted native shot serials are observations; this never owns/debits ammunition.
    public sealed class R23WeaponFeedbackState
    {
        public const float FlashSeconds=.045f,TracerSeconds=.045f,ImpactSeconds=.11f,Gain=1.25f;
        readonly SourceCatalog5Feedback catalog;readonly string id;readonly List<float> stamps=new List<float>(),pulsePeaks=new List<float>(),bloomImpulses=new List<float>();
        float clock,idle,recoil,pitch,yaw,flashUntil,observedTime;bool observedActive,observedSupported;int index;long lastShot;
        static readonly float[] BurstVertical={1,.44f,.28f,.50f,.34f,.42f},PatternYaw={0,-.12f,.18f,.10f,-.20f,-.08f};
        public R23WeaponFeedbackState(string weaponId){bool extra=weaponId=="1911"||weaponId=="tec9"||weaponId=="rfb"||weaponId=="fal"||weaponId=="s85";if(extra)catalog=new SourceCatalog5Feedback(weaponId);else if(weaponId!="mk18"&&weaponId!="akm"&&weaponId!="spr"&&weaponId!="magnum"&&weaponId!="cx4"&&weaponId!="px4"&&weaponId!="m1014"&&weaponId!="ump"&&weaponId!="p90"&&weaponId!="mp5"&&weaponId!="mp9"&&weaponId!="smaw"&&weaponId!="knife")throw new ArgumentException("Source feedback profile");id=weaponId;}
        public void SetSupported(bool held){if(catalog!=null)catalog.SetSupported(held);}
        bool Observed {get{return id=="cx4"||id=="mp5"||id=="mp9";}}
        public float CarrierTravel {get{return id=="ump"&&idle<.095f?(float)Math.Sin(idle/.095f*Math.PI)*.038f:0;}}
        bool SignedPulse {get{return id=="spr"||id=="magnum"||id=="px4";}}
        float Peak {get{return id=="magnum"?8.47f:id=="px4"?6.655f:6.776f;}}
        public float SlideTravel {get{return catalog!=null?catalog.SlideTravel:id=="px4"&&idle<.095f?(float)Math.Sin(idle/.095f*Math.PI)*.035f:0;}}
        public float Distance {get{return catalog!=null?catalog.Distance:recoil*Gain;}}
        public float PitchRadians {get{return catalog!=null?catalog.PitchRadians:pitch*Gain;}}
        public float YawRadians {get{return catalog!=null?catalog.YawRadians:yaw*Gain;}}
        public bool Flash {get{return catalog!=null?catalog.Flash:clock<flashUntil;}}
        public long AcceptedShots {get{return catalog!=null?catalog.AcceptedShots:lastShot;}}
        public float ReticleWidth(bool supported){return catalog!=null?catalog.ReticleWidth(supported):(id=="magnum"?(supported?88:134):id=="m1014"?(supported?58:98):(supported?58:70))+Bloom;}
        public float Bloom {get{if(catalog!=null)return catalog.Bloom;float result=0;for(int i=0;i<stamps.Count;i++){float age=(clock-stamps[i])/(id=="magnum"?.062f:.055f);result+=bloomImpulses[i]*age*(float)Math.Exp(1-age);}return Math.Min(id=="magnum"?250:76,result);}}
        public void Advance(float seconds)
        {
            if(catalog!=null){catalog.Advance(seconds);return;}
            if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<0||seconds>.25f)throw new ArgumentOutOfRangeException("seconds");
            clock+=seconds;idle+=seconds;float decay=(float)Math.Exp(-seconds*9);recoil*=decay;
            while(stamps.Count>0&&clock-stamps[0]>1.2f){stamps.RemoveAt(0);bloomImpulses.RemoveAt(0);if(SignedPulse)pulsePeaks.RemoveAt(0);}
            if(SignedPulse){pitch=0;for(int i=0;i<stamps.Count;i++)pitch+=pulsePeaks[i]*(id=="magnum"?MagnumPulse(clock-stamps[i]):id=="px4"?Px4Pulse(clock-stamps[i]):SprPulse(clock-stamps[i]));float cap=Peak*(float)Math.PI/180*1.5f;pitch=Math.Max(-cap,Math.Min(cap,pitch));yaw=0;}
            else if(Observed){
                if(observedActive){if(idle<=.32f){observedTime+=seconds;SampleCx4();}else{float release=(float)Math.Exp(-seconds*9);pitch*=release;yaw*=release;}}
                if(idle>=.8f){observedTime=0;observedActive=false;}
            }
            else if(idle>(id=="m1014"?.35f:.18f)){pitch*=decay;yaw*=decay;}
            if(idle>=.8f){index=0;recoil=pitch=yaw=0;}
        }
        public bool AcceptedShot(long nativeShots,bool supported)
        {
            if(catalog!=null)return catalog.AcceptedShot(nativeShots,supported);
            if(nativeShots<=lastShot)return false;lastShot=nativeShots;if(id=="smaw"||id=="knife")return true;float priorIdle=idle;float reduction=supported?.38f:1;
            recoil=Math.Min(.13f,recoil+(id=="magnum"?.039f:id=="px4"?.0312f:.052f*(id=="spr"?1.1f:1))*reduction);idle=0;
            if(Observed){if(!observedActive||priorIdle>.32f)observedTime=0;observedActive=true;observedSupported=supported;SampleCx4();}
            else if(id=="akm"||id=="ump"||id=="p90"){var data=id=="ump"?(supported?R23FeedbackSourceData.UmpTwo:R23FeedbackSourceData.UmpOne):id=="p90"?(supported?R23FeedbackSourceData.P90Two:R23FeedbackSourceData.P90One):(supported?R23FeedbackSourceData.AkmTwo:R23FeedbackSourceData.AkmOne);int n=Math.Min(++index,data.Length/2-1);pitch=data[n*2]*(float)Math.PI/180;yaw=-data[n*2+1]*(float)Math.PI/180;}
            else if(id=="mk18"){float angle=5.5f*(float)Math.PI/180*reduction;pitch=Math.Min((float)Math.PI/4,pitch+angle*BurstVertical[index%BurstVertical.Length]);yaw=Math.Max(-80*(float)Math.PI/180,Math.Min(80*(float)Math.PI/180,yaw+angle*PatternYaw[index%PatternYaw.Length]));index++;}
            stamps.Add(clock);bloomImpulses.Add(id=="magnum"?(supported?200:225):id=="m1014"?56:32);if(SignedPulse)pulsePeaks.Add(Peak*(float)Math.PI/180*reduction);
            while(stamps.Count>32){stamps.RemoveAt(0);bloomImpulses.RemoveAt(0);if(SignedPulse)pulsePeaks.RemoveAt(0);}flashUntil=clock+FlashSeconds;return true;
        }
        public void SetAcceptedSourceRecoil(float distance,float sourcePitch,float sourceYaw){if(id!="m1014")throw new InvalidOperationException("M1014 source pose observation only");recoil=distance/Gain;pitch=sourcePitch/Gain;yaw=sourceYaw/Gain;}
        public void ClearPresentation(){if(catalog!=null){catalog.ClearPresentation();return;}stamps.Clear();bloomImpulses.Clear();pulsePeaks.Clear();recoil=pitch=yaw=0;idle=.8f;index=0;observedTime=0;observedActive=false;flashUntil=clock;}
        public void Reset(){if(catalog!=null){catalog.Reset();return;}lastShot=0;clock=0;ClearPresentation();}
        public static string ReserveText(int reserve){return reserve>=0&&reserve<100000?reserve.ToString():"\u221e";}
        void SampleCx4(){
            var values=id=="mp5"?(observedSupported?R23FeedbackSourceData.Mp5Two:R23FeedbackSourceData.Mp5One):id=="mp9"?(observedSupported?R23FeedbackSourceData.Mp9Two:R23FeedbackSourceData.Mp9One):(observedSupported?R23FeedbackSourceData.Cx4Two:R23FeedbackSourceData.Cx4One);
            int last=values.Length/3-1;float time=Math.Max(0,observedTime),end=values[last*3];if(time>end){if(id=="cx4")time=end;else{float span=Math.Min(end,2.5f),phase=(time-end)%(span*2);time=end-(phase<span?phase:span*2-phase);}}
            int lo=0,hi=last;while(hi-lo>1){int middle=(lo+hi)/2;if(values[middle*3]<=time)lo=middle;else hi=middle;}float fraction=Math.Max(0,Math.Min(1,(time-values[lo*3])/Math.Max(.000001f,values[hi*3]-values[lo*3]))),gain=id=="mp5"?1.1f:1;
            pitch=(values[lo*3+1]+(values[hi*3+1]-values[lo*3+1])*fraction)*(float)Math.PI/180*gain;yaw=-(values[lo*3+2]+(values[hi*3+2]-values[lo*3+2])*fraction)*(float)Math.PI/180*gain;
        }
        public static float Px4Pulse(float age){return Pulse(age,.226f,.278f,.45f,.18f,.097f);}
        public static float SprPulse(float age)
        {
            return Pulse(age,.22f,.33f,.48f,.30f);
        }
        public static float MagnumPulse(float age){return Pulse(age,.24f,.31f,.50f,.18f);}
        static float Pulse(float age,float zero,float underTime,float end,float under,float peakTime=.09f)
        {
            if(age<=0||age>=end)return 0;
            float[] times={0,peakTime,zero,underTime,end},values={0,1,0,-under,0};float downA=-1/(times[2]-times[1]),downB=-under/(times[3]-times[2]);float[] slopes={1.8f/peakTime,0,2*downA*downB/(downA+downB),0,0};
            for(int i=0;i<4;i++)if(age<=times[i+1]){float span=times[i+1]-times[i],u=(age-times[i])/span;return (2*u*u*u-3*u*u+1)*values[i]+(u*u*u-2*u*u+u)*span*slopes[i]+(-2*u*u*u+3*u*u)*values[i+1]+(u*u*u-u*u)*span*slopes[i+1];}return 0;
        }
    }
}

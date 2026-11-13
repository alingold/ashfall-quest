using System;
using Ashfall.Core;
namespace Ashfall.Integration.Playable.Consumables
{
    public enum R23ConsumableAudioEvent { Equip,Stow,Open,Peel,Consume,Ready }
    public struct R23ConsumableCue
    {
        public readonly string Kind,Action;public readonly int Variant;public readonly bool Spatial,StopShake;
        public R23ConsumableCue(string kind,string action,int variant,bool spatial,bool stopShake)
        {Kind=kind;Action=action;Variant=variant;Spatial=spatial;StopShake=stopShake;}
    }
    // Presentation policy only. No preparation, inventory, consumption or effects authority.
    public sealed class R23ConsumableAudioPolicy
    {
        public const float UiGain=.39810717f,PhysicalGain=.25118864f,ShakeWindow=.20f;
        Float3 previous;bool haveSample;int eventCount;
        public float ShakeSecondsLeft {get;private set;}
        public int EventCount {get{return eventCount;}}
        public bool ShakeActive {get{return ShakeSecondsLeft>0;}}
        static bool Finite(float n){return !Single.IsNaN(n)&&!Single.IsInfinity(n);}
        public static bool KindValid(string kind){return kind=="soda"||kind=="shaker"||kind=="banana";}
        public static bool QualifiesShake(float travel,float seconds)
        {if(!Finite(travel)||!Finite(seconds)||travel<0||seconds<=0)return false;float speed=travel/seconds;return speed>=.45f&&speed<=8&&travel<.3f;}
        public bool SampleShake(Float3 actorLocal,float seconds,bool preparing)
        {
            if(!preparing||!Finite(seconds)||seconds<=0||!Finite(actorLocal.X)||!Finite(actorLocal.Y)||!Finite(actorLocal.Z)){haveSample=false;return false;}
            bool motion=false;if(haveSample){float x=actorLocal.X-previous.X,y=actorLocal.Y-previous.Y,z=actorLocal.Z-previous.Z;motion=QualifiesShake((float)Math.Sqrt(x*x+y*y+z*z),seconds);}
            previous=actorLocal;haveSample=true;if(motion)ShakeMotion();return motion;
        }
        // Explicit input for accepted source desktop E-held shake or a host-qualified VR sample.
        public void ShakeMotion(){ShakeSecondsLeft=ShakeWindow;}
        public void Tick(float seconds)
        {if(!Finite(seconds)||seconds<0){StopShake();return;}ShakeSecondsLeft=Math.Max(0,ShakeSecondsLeft-seconds);}
        public bool TryCue(R23ConsumableAudioEvent value,string kind,out R23ConsumableCue cue)
        {
            cue=default(R23ConsumableCue);if(!KindValid(kind))return false;
            string action;bool spatial=true,stop=false;
            switch(value)
            {
                case R23ConsumableAudioEvent.Equip:action="item_equip";spatial=false;stop=true;break;
                case R23ConsumableAudioEvent.Stow:action="item_stow";spatial=false;stop=true;break;
                case R23ConsumableAudioEvent.Open:action="consume_open";break;
                case R23ConsumableAudioEvent.Peel:if(kind!="banana")return false;action="consume_open";break;
                case R23ConsumableAudioEvent.Consume:action="consume_drink";stop=true;break;
                case R23ConsumableAudioEvent.Ready:if(kind!="shaker")return false;action="consume_ready";stop=true;break;
                default:return false;
            }
            eventCount=Math.Min(64,eventCount+1);if(stop)StopShake();
            cue=new R23ConsumableCue(kind,action,spatial?eventCount:0,spatial,stop);return true;
        }
        public void StopShake(){ShakeSecondsLeft=0;haveSample=false;}
        public void Reset(){StopShake();eventCount=0;}
    }
}

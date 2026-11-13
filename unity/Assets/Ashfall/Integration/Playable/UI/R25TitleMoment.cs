using System;
namespace Ashfall.Integration.Playable.UI
{
    // Local presentation clock; one instance survives page rebuilds. No input capture.
    public sealed class R25TitleMoment
    {
        public bool Entered { get; private set; }
        public float Elapsed { get; private set; }
        float settleAt=-1;
        static float Clamp(float n){return Math.Max(0,Math.Min(1,n));}
        public bool Enter(bool enabled,bool returningFromMatch)
        {
            if(Entered)return false;
            Entered=true;
            if(!enabled||returningFromMatch){Elapsed=5;settleAt=4;return false;}
            return true;
        }
        public void Advance(float seconds,bool input)
        {
            if(!Entered||float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<0)return;
            if(input&&settleAt<0)settleAt=Elapsed;
            Elapsed+=seconds;
            if(settleAt<0&&Elapsed>=4)settleAt=4;
        }
        public float LogoAlpha {get{return Clamp(Elapsed/.6f);}}
        public float Rise {get{float t=1-Clamp(Elapsed/.6f);return .04f*(1-t*t*t);}}
        public float Settle
        {
            get{if(settleAt<0)return 0;float t=Clamp((Elapsed-settleAt)/.8f);return t<.5f?4*t*t*t:1-(float)Math.Pow(-2*t+2,3)/2;}
        }
        public float Scale {get{return 1-.58f*Settle;}}
        public int EmberCount {get{return Elapsed<.35f?0:Settle>=1?10:36;}}
        public int AshCount {get{return Settle>=1?0:24;}}
    }
}

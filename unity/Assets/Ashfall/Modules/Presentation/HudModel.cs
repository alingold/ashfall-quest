using System;

namespace Ashfall.Modules.Presentation
{
    public sealed class HudModel
    {
        public bool Visible { get; private set; }
        private object transientResetIdentity=new object();
        public object TransientResetIdentity {get{return transientResetIdentity;}}
        public float Health { get; private set; }
        public float Shield { get; private set; }
        public float Sprint { get; private set; }
        public float Incoming { get; private set; }
        public float IncomingSeconds { get; private set; }
        public double MatchSeconds { get; private set; }
        public bool ClockDanger { get; private set; }
        public string ClockCaption { get; private set; }
        private static bool Finite(float n) { return !float.IsNaN(n)&&!float.IsInfinity(n); }
        private static float Fraction(float n) { return Finite(n)?Math.Max(0,Math.Min(1,n)):0; }
        public void Damage(float amount)
        {
            if(!Visible || !Finite(amount)||amount<=0)return;
            Incoming=IncomingSeconds>0?Math.Min(999999,Incoming+amount):Math.Min(999999,amount);IncomingSeconds=1.15f;
        }
        public void Update(HudInput input,bool paused,float delta)
        {
            if(input==null)throw new ArgumentNullException("input");
            // Reset each update, including zone removal, round restart and hidden HUD.
            ClockDanger=false;
            MatchSeconds=double.IsNaN(input.MatchSeconds)||double.IsInfinity(input.MatchSeconds)?0:Math.Max(0,input.MatchSeconds);
            bool wasVisible=Visible;
            Visible=!paused && (!input.Dead || input.NetworkGhost) && !input.Complete;
            if(wasVisible&&!Visible)transientResetIdentity=new object();
            if(Visible && input.ZoneActive)ClockDanger=input.OutsideZone;
            double whole=Math.Min(Int32.MaxValue,Math.Ceiling(MatchSeconds));
            ClockCaption=input.NoTimeCap?"NO TIME CAP":String.Format(System.Globalization.CultureInfo.InvariantCulture,"TIME {0}:{1:00}",(int)whole/60,(int)whole%60);
            if(Visible&&input.ZoneActive) {
                ClockCaption+="  ·  "+(input.FinalZone?"FINAL ZONE":input.ZoneMoving?"ZONE MOVING":"ZONE "+Math.Ceiling(double.IsNaN(input.ZoneHoldSeconds)?0:Math.Max(0,Math.Min(Int32.MaxValue,input.ZoneHoldSeconds))).ToString("0",System.Globalization.CultureInfo.InvariantCulture)+"s");
                if(input.OutsideZone)ClockCaption+=" · OUTSIDE "+(Finite(input.ZoneDamagePerSecond)?Math.Max(0,input.ZoneDamagePerSecond):0).ToString("0",System.Globalization.CultureInfo.InvariantCulture)+" HP/s";
            }
            Health=Fraction(input.Health/100);Shield=Fraction(input.Shield/100);
            Sprint=input.Stamina<0?1:Fraction(input.Stamina/Math.Max(.01f,Finite(input.StaminaMaximum)?input.StaminaMaximum:.01f));
            if(!Visible){Incoming=0;IncomingSeconds=0;return;}
            IncomingSeconds=Math.Max(0,IncomingSeconds-(Finite(delta)?Math.Max(0,delta):0));
            if(IncomingSeconds==0)Incoming=0;
        }
    }
}

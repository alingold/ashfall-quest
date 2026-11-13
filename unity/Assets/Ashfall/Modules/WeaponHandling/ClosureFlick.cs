using System;
using System.Collections.Generic;
using Ashfall.Core;
namespace Ashfall.Modules.WeaponHandling
{
    // Port of scripts/closure_flick.gd: player RIGHT stays +X after Z reflection.
    internal sealed class ClosureFlick
    {
        struct Sample { public Float3 Point,Forward,Up;public float Time; }
        List<Sample> history=new List<Sample>();float clock,neutral;bool armed;
        internal ClosureFlick Copy(){var c=(ClosureFlick)MemberwiseClone();c.history=new List<Sample>(history);return c;}
        internal void Reset(){history.Clear();neutral=0;armed=false;}
        static Float3 Sub(Float3 a,Float3 b){return new Float3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);}
        static float Length(Float3 a){return (float)Math.Sqrt(a.X*a.X+a.Y*a.Y+a.Z*a.Z);}
        static float Angle(Float3 a,Float3 b){float n=Length(a)*Length(b);return n<=0?0:(float)Math.Acos(Math.Max(-1,Math.Min(1,(a.X*b.X+a.Y*b.Y+a.Z*b.Z)/n)));}
        internal bool Read(HandlingInput i)
        {
            float dt=i.DeltaSeconds;Float3 point=i.PrimaryPoint,forward=i.PrimaryForward,up=i.PrimaryUp;
            if(!i.HasPrimaryPose||!point.IsFinite||!forward.IsFinite||!up.IsFinite||dt<=0||dt>.15f){Reset();return false;}
            clock+=dt;float speed=0,angular=0;
            if(history.Count>0){Sample last=history[history.Count-1];float distance=Length(Sub(point,last.Point));if(distance>.7f){Reset();return false;}speed=distance/dt;angular=Angle(forward,last.Forward)/dt;if(Length(up)>.70710678f&&Length(last.Up)>.70710678f)angular=Math.Max(angular,Angle(up,last.Up)/dt);}
            if(!i.Trigger&&speed<.35f&&angular<1.2f){neutral+=dt;if(neutral>=.035f)armed=true;}else neutral=0;
            while(history.Count>0&&clock-history[0].Time>.22f)history.RemoveAt(0);
            if(armed&&!i.Trigger&&history.Count>0){Sample first=history[0];float elapsed=Math.Max(.001f,clock-first.Time);Float3 d=Sub(point,first.Point);float travel=d.X,pitch=forward.X-first.Forward.X,roll=Length(up)>.70710678f&&Length(first.Up)>.70710678f?up.X-first.Up.X:0;float lateral=(float)Math.Sqrt(d.Y*d.Y+d.Z*d.Z);
                if((travel>=.045f&&travel/elapsed>=.32f&&travel>lateral*.65f)||(pitch>=.28f&&pitch/elapsed>=1.4f)||(roll>=.28f&&roll/elapsed>=1.4f)){Reset();return true;}}
            history.Add(new Sample{Point=point,Forward=forward,Up=up,Time=clock});return false;
        }
    }
    public sealed partial class WeaponHandling
    {
        ClosureFlick flick=new ClosureFlick();
        void PrimaryFlick(HandlingInput i)
        {
            if(mechanism!=WeaponMechanism.MagnumRevolver||i.Desktop||(phase!=ReloadPhase.CylinderOpen&&phase!=ReloadPhase.CylinderLoaded)){flick.Reset();return;}
            if(flick.Read(i)&&phase==ReloadPhase.CylinderLoaded)CloseCylinder();
        }
    }
}

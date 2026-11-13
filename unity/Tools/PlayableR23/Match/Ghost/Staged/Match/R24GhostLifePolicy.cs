using System;
using Ashfall.Modules.HealthStates;
namespace Ashfall.Integration.Playable.Match
{
    // net_bridge1000._sync_player: downed/eliminated retain normal actor input.
    // HealthService.Dead is source eliminated (also while awaiting respawn).
    public static class R24GhostLifePolicy
    {
        public static bool Ghost(bool active,LifeState life)
        {return active&&(life==LifeState.Downed||life==LifeState.Dead);}
        public static bool Motion(bool selected,bool active,LifeState life)
        {return !selected||active&&(life==LifeState.Alive||Ghost(active,life));}
        public static bool Items(bool selected,bool active,LifeState life)
        {return !selected||active&&life==LifeState.Alive;}
        public const float Opacity=.42f;
        public static float Gray(float red,float green,float blue,float facing)
        {
            if(!Finite(red)||!Finite(green)||!Finite(blue)||!Finite(facing))throw new ArgumentException("Finite ghost sample required");
            float rim=1-Math.Min(1,Math.Abs(facing));
            return Math.Max(0,Math.Min(1,.17f+(red*.2126f+green*.7152f+blue*.0722f)*.69f+rim*rim*.14f));
        }
        static bool Finite(float x){return !Single.IsNaN(x)&&!Single.IsInfinity(x);}
    }
}

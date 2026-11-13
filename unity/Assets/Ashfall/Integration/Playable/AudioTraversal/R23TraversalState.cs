using System;

namespace Ashfall.Integration.Playable.AudioTraversal
{
    public enum R23Footfall { None, Walk, Sprint, Landing }
    public struct R23TraversalSample
    {
        // Supply accepted velocity, never desired stick speed or proposed motion.
        public float HorizontalSpeed, VerticalSpeed;
        public bool Enabled, Grounded, Sprinting, Gliding, Climbing, Crouched, HandAnchored;
    }
    public struct R23TraversalFrame
    {
        public R23Footfall Footfall;
        public bool ClimbContact;
        public float GlideGain, GlidePitch;
    }
    // ambience.gd's gait and flight feedback clocks, independent of render/audio APIs.
    public sealed class R23TraversalState
    {
        float stride,glideGain,lastVertical,climbElapsed;
        bool wasGrounded,havePrevious;
        public void Reset() { stride=glideGain=lastVertical=climbElapsed=0;wasGrounded=havePrevious=false; }
        public R23TraversalFrame Tick(R23TraversalSample sample,float seconds)
        {
            if(!Finite(seconds)||seconds<=0||!sample.Enabled||!Finite(sample.HorizontalSpeed)||sample.HorizontalSpeed<0||!Finite(sample.VerticalSpeed))
            { Reset();return new R23TraversalFrame{GlidePitch=.85f}; }
            float target=sample.Gliding?Math.Max(0,Math.Min(1,(sample.HorizontalSpeed-2.5f)/8)):0;
            glideGain=Approach(glideGain,target,seconds*2);
            var output=new R23TraversalFrame{GlideGain=glideGain,GlidePitch=.85f+glideGain*.3f};
            if(sample.Grounded&&sample.HorizontalSpeed>.35f&&!sample.Climbing&&!sample.Gliding&&!sample.Crouched)
            {
                stride+=seconds;float interval=sample.Sprinting?.33f:.48f;
                // At most one transient, including stalls/extreme source walk gains.
                if(stride+.000001f>=interval){stride=Math.Max(0,stride-interval)%interval;output.Footfall=sample.Sprinting?R23Footfall.Sprint:R23Footfall.Walk;}
            }
            else stride=0;
            // ambience.gd: only active hand-over-hand motion advances the .55s
            // clock. A stationary anchored hold retains progress; release resets.
            if(sample.HandAnchored&&((double)sample.HorizontalSpeed*sample.HorizontalSpeed+(double)sample.VerticalSpeed*sample.VerticalSpeed)>.12*.12)
            {climbElapsed+=seconds;if(climbElapsed+.000001f>=.55f){climbElapsed=Math.Max(0,climbElapsed-.55f)%.55f;output.ClimbContact=true;}}
            else if(!sample.HandAnchored)climbElapsed=0;
            if(havePrevious&&sample.Grounded&&!wasGrounded&&lastVertical< -2.8f&&!sample.Crouched&&!sample.Climbing)output.Footfall=R23Footfall.Landing;
            wasGrounded=sample.Grounded;lastVertical=sample.VerticalSpeed;havePrevious=true;return output;
        }
        static float Approach(float from,float to,float amount){return from<to?Math.Min(to,from+amount):Math.Max(to,from-amount);}
        static bool Finite(float value){return !Single.IsNaN(value)&&!Single.IsInfinity(value);}
        public static string SurfaceType(string value)
        {
            value=(value??"").ToLowerInvariant();
            if(value.Contains("metal")||value.Contains("steel")||value.Contains("grate"))return "metal";
            if(value.Contains("wood")||value.Contains("timber"))return "wood";
            if(value.Contains("gravel")||value.Contains("dirt")||value.Contains("stone"))return "gravel";
            return "concrete";
        }
    }
}

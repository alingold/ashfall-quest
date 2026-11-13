using System;
using Ashfall.Core;
using Ashfall.Modules.Climbing;
namespace Ashfall.Integration.Playable.Climbing
{
    // Reuse source anchor/mean-pull mechanics while release momentum comes from
    // the actual sole-root collision result, not the speculative query displacement.
    public sealed class R23ClimbState
    {
        readonly ClimbController controller;
        Float3 releaseVelocity;
        ClimbStep pending;
        bool hasPending;
        public bool LeftAnchored {get{return controller.LeftAnchored;}}
        public bool RightAnchored {get{return controller.RightAnchored;}}
        public bool Mantling {get{return controller.Mantling;}}
        public Float3 LeftAnchor {get{return controller.LeftAnchor;}}
        public Float3 RightAnchor {get{return controller.RightAnchor;}}
        public R23ClimbState(IClimbCollision world){controller=new ClimbController(world);}
        public void Reset(){controller.Reset();releaseVelocity=new Float3();pending=new ClimbStep();hasPending=false;}
        public ClimbStep Evaluate(InputFrame worldInput,Float3 root,float seconds,ClimbOptions options)
        {
            // Missing feedback must never turn a proposed displacement into a fling.
            if(hasPending){Reset();}
            bool left=controller.LeftAnchored,right=controller.RightAnchored;
            var step=controller.Step(worldInput,root,seconds,options);
            bool newAnchor=(!left&&controller.LeftAnchored)||(!right&&controller.RightAnchored);
            if(newAnchor)releaseVelocity=new Float3();
            bool lost=(left&&!options.LeftTracked)||(right&&!options.RightTracked)||!worldInput.Tracked||worldInput.Paused||!options.Enabled;
            if(step.Released&&!step.Mantling)
            {
                step.Velocity=lost?new Float3():Multiply(Limit(releaseVelocity,7),Math.Max(0,Math.Min(5,options.FlingPower))*1.25f);
                step.Launch=Length(step.Velocity)*Length(step.Velocity)>.001f;
                releaseVelocity=new Float3();
            }
            if(!step.Accepted||(!step.Climbing&&!step.Released&&!step.Mantling))releaseVelocity=new Float3();
            pending=step;hasPending=true;return step;
        }
        // Source release is momentum for the NEXT frame; never a second root translation.
        public Float3 AcceptMotion(Float3 actualDisplacement,float seconds,bool grounded,bool ceiling,out bool acceptedGrounded)
        {
            acceptedGrounded=grounded;
            if(!hasPending||!actualDisplacement.IsFinite||Single.IsNaN(seconds)||Single.IsInfinity(seconds)||seconds<=0||seconds>.1f)
            {Reset();return new Float3();}
            hasPending=false;
            Float3 velocity=Multiply(actualDisplacement,1/seconds);
            if(!velocity.IsFinite){Reset();return new Float3();}
            if(pending.Climbing)releaseVelocity=Add(Multiply(releaseVelocity,.65f),Multiply(velocity,.35f));
            if(pending.Released&&pending.Launch)
            {
                velocity=pending.Velocity;
                // A positive release leaves the contact just completed; a ceiling still clips it.
                if(velocity.Y>0&&!ceiling)acceptedGrounded=false;
            }
            else if(pending.OwnsMotion&&!pending.Climbing&&!pending.Mantling)velocity=new Float3();
            if((acceptedGrounded&&velocity.Y<0)||(ceiling&&velocity.Y>0))velocity=new Float3(velocity.X,0,velocity.Z);
            return velocity;
        }
        static Float3 Add(Float3 a,Float3 b){return new Float3(a.X+b.X,a.Y+b.Y,a.Z+b.Z);}
        static Float3 Multiply(Float3 a,float k){return new Float3(a.X*k,a.Y*k,a.Z*k);}
        static float Length(Float3 a){return (float)Math.Sqrt((double)a.X*a.X+(double)a.Y*a.Y+(double)a.Z*a.Z);}
        static Float3 Limit(Float3 a,float max){float length=Length(a);return length>max?Multiply(a,max/length):a;}
    }
}

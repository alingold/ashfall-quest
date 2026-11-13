using System;
using Ashfall.Core;
using Ashfall.Integration.Playable.Exclusion;
namespace Ashfall.Integration.Playable.Traversal
{
    // One displacement proposal and one accepted-physical feedback per frame.
    // No rotation, tracking, combat or durable health state belongs to this motor.
    public sealed class R25GrapplePull
    {
        public const float Speed=16, Ramp=.15f, Duration=1.5f, Stop=1.5f, Residual=6;
        public bool Active {get;private set;}
        public Float3 Target {get;private set;}
        public Float3 Velocity {get;private set;}
        float age,blocked;
        public void Begin(Float3 target,float elapsed)
        {
            Target=target;age=Math.Max(0,elapsed);blocked=0;Velocity=new Float3();
            Active=target.IsFinite&&!Single.IsNaN(elapsed)&&!Single.IsInfinity(elapsed)&&age<Duration;
        }
        public void End(){Active=false;Velocity=R25TraversalVector.Limit(Velocity,Residual);}
        public Float3 Propose(Float3 chest,float dt)
        {
            if(!Active)return new Float3();
            if(!chest.IsFinite||Single.IsNaN(dt)||dt<=0||dt>.1f){End();return new Float3();}
            var delta=R25TraversalVector.Sub(Target,chest);float distance=R25TraversalVector.Length(delta);
            if(distance<=Stop||age>=Duration){End();return new Float3();}
            float next=Math.Min(Duration,age+dt);
            // Integral of the linear speed ramp, independent of sample frequency.
            float length=Travel(next)-Travel(age);age=next;
            return R25TraversalVector.Mul(R25TraversalVector.Unit(delta),Math.Min(distance-Stop,length));
        }
        static float Travel(float time){return time<Ramp?Speed*time*time/(2*Ramp):Speed*(time-Ramp*.5f);}
        public void Accept(Float3 requested,Float3 physical,float dt)
        {
            if(!Active)return;
            Velocity=dt>0&&physical.IsFinite?R25TraversalVector.Limit(R25TraversalVector.Mul(physical,1/dt),Speed):new Float3();
            bool obstruction=R25TraversalVector.Length(requested)>.001f&&R25TraversalVector.Length(physical)<R25TraversalVector.Length(requested)*.2f;
            blocked=obstruction?blocked+dt:0;
            if(blocked>=.25f||age>=Duration)End();
        }
        public Float3 Step(Float3 chest,float dt,R25HeadClearance clearance,Func<Float3,Float3,Float3> sweep,Func<Float3,bool> clear)
        {
            var wanted=Propose(chest,dt);
            var physical=Active?clearance.Move(chest,wanted,sweep,clear):new Float3();
            Accept(wanted,physical,dt);return physical;
        }
        // Add the pull budget to the ordinary validator allowance, only in this window.
        public static float Allowance(float ordinary,double now,double until)
        {return now<=until?ordinary+Speed/55f:ordinary;}
    }
}

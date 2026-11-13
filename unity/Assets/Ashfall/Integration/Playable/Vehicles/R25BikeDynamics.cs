using System;
using Ashfall.Core;
namespace Ashfall.Integration.Playable.Vehicles
{
    // Pure kinematic proposal. The exclusive rig owner must sweep Displacement,
    // then feed accepted physical velocity back through Accept (no support lift).
    public sealed class R25BikeDynamics
    {
        public Float3 Velocity { get; private set; }
        public Float3 Displacement { get; private set; }
        public float BoostRemaining { get; private set; }
        public float RechargeRemaining { get; private set; }
        public float HopRemaining { get; private set; }
        bool boostHeld, jumpHeld;
        public void Accept(Float3 velocity) { Velocity = velocity.IsFinite ? velocity : new Float3(); }
        static float Clamp(float v,float a,float b) { return Math.Max(a,Math.Min(b,v)); }
        static float Approach(float a,float b,float step) { return a+Clamp(b-a,-step,step); }
        public void Step(float dt,float throttle,float strafe,float yaw,bool boost,bool jump,
            bool ground,float height,Float3 normal)
        {
            Displacement=new Float3();
            if(Single.IsNaN(dt)||Single.IsInfinity(dt)||dt<=0||dt>.1f||
                Single.IsNaN(throttle)||Single.IsInfinity(throttle)||Single.IsNaN(strafe)||Single.IsInfinity(strafe)||
                Single.IsNaN(yaw)||Single.IsInfinity(yaw)||!normal.IsFinite||Single.IsNaN(height)||Single.IsInfinity(height))return;
            bool startBoost=boost&&!boostHeld&&RechargeRemaining<=0;
            bool hop=jump&&!jumpHeld&&HopRemaining<=0&&ground;
            boostHeld=boost;jumpHeld=jump;
            if(startBoost){BoostRemaining=2;RechargeRemaining=8;} // 2 active + 6 recharge
            float cap=BoostRemaining>0?50:40;
            float s=(float)Math.Sin(yaw),c=(float)Math.Cos(yaw);
            float forward=Velocity.X*s+Velocity.Z*c,side=Velocity.X*c-Velocity.Z*s;
            throttle=Clamp(throttle,-1,1);strafe=Clamp(strafe,-1,1);
            forward=Approach(forward,throttle*(throttle<0?6:cap),
                (throttle<0&&forward>0?22:Math.Abs(throttle)<.01f?3:11)*dt);
            side=Approach(side,strafe*6,(Math.Abs(strafe)<.01f?3:11)*dt);
            forward=Clamp(forward,-6,cap);side=Clamp(side,-6,6);
            float x=forward*s+side*c,z=forward*c-side*s;
            float ax=x-Velocity.X,az=z-Velocity.Z;
            float change=(float)Math.Sqrt(ax*ax+az*az);
            float acceleration=(throttle<0&&Velocity.X*s+Velocity.Z*c>0?22:11)*dt;
            if(change>acceleration){x=Velocity.X+ax*acceleration/change;z=Velocity.Z+az*acceleration/change;}
            float length=(float)Math.Sqrt(x*x+z*z);
            if(length>cap){x*=cap/length;z*=cap/length;}
            float vy=Velocity.Y-16.2f*dt;
            float n=(float)Math.Sqrt(normal.X*normal.X+normal.Y*normal.Y+normal.Z*normal.Z);
            if(ground&&!(HopRemaining>0&&Velocity.Y>0)&&n>.0001f&&normal.Y/n>=(float)Math.Cos(40*Math.PI/180)&&height<=1.2f){
                float slope=-(normal.X*x+normal.Z*z)/normal.Y;
                vy=Approach(Velocity.Y,slope+Clamp((.55f-height)*8,-4,4),30*dt);
            }
            if(hop){vy=5.5f;HopRemaining=1.2f;}
            Velocity=new Float3(x,vy,z);Displacement=new Float3(x*dt,vy*dt,z*dt);
            BoostRemaining=Math.Max(0,BoostRemaining-dt);RechargeRemaining=Math.Max(0,RechargeRemaining-dt);HopRemaining=Math.Max(0,HopRemaining-dt);
        }
    }
}

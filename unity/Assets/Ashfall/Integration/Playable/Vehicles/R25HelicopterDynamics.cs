using System;
using Ashfall.Core;
namespace Ashfall.Integration.Playable.Vehicles
{
    public static class R25HelicopterClearance
    {
        public static Float3 Half(int volume) {return volume==1?new Float3(3.4f,.4f,3.4f):new Float3(.85f,.63f,3.25f);}
        public static Float3 Center(Float3 root,float yaw,int volume)
        {
            float z=volume==1?-.95f:-1.15f;double a=yaw*Math.PI/180;
            return new Float3(root.X+(float)Math.Sin(a)*z,root.Y+(volume==1?2:.15f),root.Z+(float)Math.Cos(a)*z);
        }
        // Queries are injected so CPU analytic worlds and native casts share endpoint policy.
        public static Float3 Sweep(Float3 root,float yaw,Float3 motion,Func<Float3,Float3,bool> clear,Func<Float3,Float3,Float3,float> fraction)
        {
            if(!root.IsFinite||!motion.IsFinite||Single.IsNaN(yaw)||Single.IsInfinity(yaw)||clear==null||fraction==null)return new Float3();
            float f=1;
            for(int v=0;v<2;v++){
                var c=Center(root,yaw,v);var h=Half(v);if(!clear(c,h))return new Float3();
                float candidate=fraction(c,h,motion);if(Single.IsNaN(candidate)||candidate<0)return new Float3();f=Math.Min(f,Math.Min(1,candidate));
            }
            var safe=new Float3(motion.X*f,motion.Y*f,motion.Z*f);
            for(int v=0;v<2;v++){var c=Center(new Float3(root.X+safe.X,root.Y+safe.Y,root.Z+safe.Z),yaw,v);if(!clear(c,Half(v)))return new Float3();}
            return safe;
        }
    }
    // Owner proposes translation only. Accepted swept motion feeds back through Accept.
    public sealed class R25HelicopterDynamics
    {
        public Float3 Velocity {get;private set;}
        public Float3 Displacement {get;private set;}
        public float Rotor {get;private set;}
        public float LaunchGround {get;private set;}
        public void Reset(float ground) { LaunchGround=Finite(ground)?ground:0;Rotor=0;Velocity=new Float3();Displacement=new Float3(); }
        public void Accept(Float3 v) { Velocity=v.IsFinite?v:new Float3(); }
        // R5 (playtest 4): the owner asked for no maximum height. A very high
        // finite bound remains so the host's track check stays well defined.
        public static float Ceiling(float ground,float mapTop) {return ground+2000;}
        public static bool CanExit(float height,bool grips,bool empty,bool tracked,bool jump,bool click,bool menu)
        { return Finite(height)&&height>=0&&grips&&empty&&tracked&&!menu; }
        public void Step(float dt,float x,float z,float yaw,bool jump,bool fast,float y,float ground,float mapTop)
        {
            Displacement=new Float3();
            if(!Finite(dt)||dt<=0||dt>.1f||!Finite(x)||!Finite(z)||!Finite(yaw)||!Finite(y)||!Finite(ground)||!Finite(mapTop))return;
            Rotor=Math.Min(1,Rotor+dt/1.5f);
            if(Rotor<.99999f){Velocity=new Float3();return;}
            float length=(float)Math.Sqrt(x*x+z*z);if(length>1){x/=length;z/=length;}
            double a=yaw*Math.PI/180;float tx=(float)(x*Math.Cos(a)+z*Math.Sin(a))*18,tz=(float)(z*Math.Cos(a)-x*Math.Sin(a))*18;
            float dx=tx-Velocity.X,dz=tz-Velocity.Z,d=(float)Math.Sqrt(dx*dx+dz*dz),limit=dt*(length<.01f?12:10);
            if(d>limit){dx*=limit/d;dz*=limit/d;}
            float height=Math.Max(0,y-ground),target=fast?-6:jump?6:-2;
            // Arrest descent over the final two metres; rest root is .55m above ground.
            if(target<0)target=Math.Max(target,-Math.Max(0,(height-.55f)*2));
            float vy=Velocity.Y+Math.Max(-12*dt,Math.Min(12*dt,target-Velocity.Y));
            float ceiling=Ceiling(LaunchGround,mapTop);
            vy=Math.Min(vy,Math.Max(0,(ceiling-y)/dt));
            vy=Math.Max(vy,Math.Min(0,(ground+.55f-y)/dt));
            Velocity=new Float3(Velocity.X+dx,vy,Velocity.Z+dz);Displacement=new Float3(Velocity.X*dt,vy*dt,Velocity.Z*dt);
        }
        static bool Finite(float n) {return !Single.IsNaN(n)&&!Single.IsInfinity(n);}
    }
}

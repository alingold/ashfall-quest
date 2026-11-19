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
        // r19 VEHICLES19 vertical tuning (m/s, m/s^2). Before r19: passive descent 2, click descent 6, climb 6, accel 12.
        public const float PassiveDescent=5,FastDescent=10,Climb=6,VerticalAccel=16;
        public const float LegacyPassiveDescent=2,LegacyFastDescent=6,LegacyVerticalAccel=12;
        // Pre-r19 rates, kept for the maintained CPU suites. The game calls the collective overload below.
        public void Step(float dt,float x,float z,float yaw,bool jump,bool fast,float y,float ground,float mapTop)
        {StepCore(dt,x,z,yaw,false,0,jump,fast,y,ground,mapTop,LegacyPassiveDescent,LegacyFastDescent,LegacyVerticalAccel);}
        // r19: collective is the smoothed left-controller command in -1..1 while the left grip is held; otherwise altitude behaviour is climb button / click / passive descent.
        public void Step(float dt,float x,float z,float yaw,bool collectiveHeld,float collective,bool climbButton,bool fast,float y,float ground,float mapTop)
        {StepCore(dt,x,z,yaw,collectiveHeld,collective,climbButton,fast,y,ground,mapTop,PassiveDescent,FastDescent,VerticalAccel);}
        void StepCore(float dt,float x,float z,float yaw,bool collectiveHeld,float collective,bool jump,bool fast,float y,float ground,float mapTop,float descent,float fastDescent,float accel)
        {
            Displacement=new Float3();
            if(!Finite(dt)||dt<=0||dt>.1f||!Finite(x)||!Finite(z)||!Finite(yaw)||!Finite(y)||!Finite(ground)||!Finite(mapTop)||!Finite(collective))return;
            Rotor=Math.Min(1,Rotor+dt/1.5f);
            if(Rotor<.99999f){Velocity=new Float3();return;}
            float length=(float)Math.Sqrt(x*x+z*z);if(length>1){x/=length;z/=length;}
            double a=yaw*Math.PI/180;float tx=(float)(x*Math.Cos(a)+z*Math.Sin(a))*18,tz=(float)(z*Math.Cos(a)-x*Math.Sin(a))*18;
            float dx=tx-Velocity.X,dz=tz-Velocity.Z,d=(float)Math.Sqrt(dx*dx+dz*dz),limit=dt*(length<.01f?12:10);
            if(d>limit){dx*=limit/d;dz*=limit/d;}
            float height=Math.Max(0,y-ground),target=collectiveHeld?R26VehicleSeat.VerticalTarget(collective):fast?-fastDescent:jump?Climb:-descent;
            // Arrest descent over the final two metres; rest root is .55m above ground.
            if(target<0)target=Math.Max(target,-Math.Max(0,(height-.55f)*2));
            float vy=Velocity.Y+Math.Max(-accel*dt,Math.Min(accel*dt,target-Velocity.Y));
            float ceiling=Ceiling(LaunchGround,mapTop);
            vy=Math.Min(vy,Math.Max(0,(ceiling-y)/dt));
            vy=Math.Max(vy,Math.Min(0,(ground+.55f-y)/dt));
            Velocity=new Float3(Velocity.X+dx,vy,Velocity.Z+dz);Displacement=new Float3(Velocity.X*dt,vy*dt,Velocity.Z*dt);
        }
        static bool Finite(float n) {return !Single.IsNaN(n)&&!Single.IsInfinity(n);}
    }
    // r19 VEHICLES19: seat controls shared by the rig, the host and the CPU checks. No engine types.
    public struct R26SeatHand
    {
        public bool Ok;public float X,Y,Z,Grip;
        public R26SeatHand(bool ok,float x,float y,float z,float grip){Ok=ok;X=x;Y=y;Z=z;Grip=grip;}
    }
    public static class R26VehicleSeat
    {
        // Handle geometry, rig-local metres, relative to the seated head captured for the ride.
        public const float HandleAboveHead=.30f,HandleForward=.12f,CableLength=.34f;
        // Grab: grip rising edge within GrabRadius of the handle bar. Eject: pulled EjectPull below the grab point.
        public const float GrabRadius=.12f,GrabLoseRadius=.32f,EjectPull=.12f,MaxPull=.20f,GripDown=.6f,GripUp=.35f,MinHoldSeconds=.10f,SpringSeconds=.08f,HapticStep=.03f;
        // Fallback exit (old right-grip / bike-jump hold) is now deliberate: 1.5 s instead of .5 to .6 s.
        public const float FallbackExitSeconds=1.5f;
        // Helicopter collective (left grip held): controller pitch change since the press.
        public const float CollectiveDeadDegrees=6,CollectiveFullDegrees=30,CollectiveSmoothSeconds=.12f,CollectiveClimbRate=6,CollectiveDescentRate=10;
        public static bool Finite(float n){return !Single.IsNaN(n)&&!Single.IsInfinity(n);}
        public static float PitchDegrees(float fx,float fy,float fz)
        {
            float len=(float)Math.Sqrt(fx*fx+fy*fy+fz*fz);if(!Finite(len)||len<.0001f)return 0;
            return (float)(Math.Asin(Math.Max(-1,Math.Min(1,fy/len)))*180/Math.PI);
        }
        // Tilt up (positive) climbs, down descends. Dead zone around the press pose, linear to full at CollectiveFullDegrees.
        public static float Command(float tiltDegrees)
        {
            if(!Finite(tiltDegrees))return 0;
            float a=Math.Abs(tiltDegrees);if(a<=CollectiveDeadDegrees)return 0;
            float t=Math.Min(1,(a-CollectiveDeadDegrees)/(CollectiveFullDegrees-CollectiveDeadDegrees));
            return tiltDegrees<0?-t:t;
        }
        // Vertical speed target, m/s: climb is capped lower than descent.
        public static float VerticalTarget(float command)
        {
            command=Math.Max(-1,Math.Min(1,Finite(command)?command:0));
            return command>=0?command*CollectiveClimbRate:command*CollectiveDescentRate;
        }
        public static float Smooth(float current,float target,float dt)
        {
            if(!Finite(dt)||dt<=0)return current;
            return current+(target-current)*(1-(float)Math.Exp(-dt/CollectiveSmoothSeconds));
        }
        public static bool PullEjects(float pull){return Finite(pull)&&pull>=EjectPull-.0005f;}
    }
    // Grabbable eject handle. Feed it rig-local positions each frame; it never ejects on a brush or a stationary grip.
    public sealed class R26EjectHandleState
    {
        public float Pull{get;private set;}
        public bool Grabbed{get;private set;}
        public int Hand{get;private set;}
        public bool Ejected{get;private set;}
        public int HapticSteps{get;private set;}
        public bool CrossedThreshold{get;private set;}
        public bool GrabbedNow{get;private set;}
        float startY,held;bool downL,downR;int stepIndex;bool wasPast;
        public void Reset(){Pull=0;Grabbed=false;Ejected=false;HapticSteps=0;CrossedThreshold=false;GrabbedNow=false;held=0;downL=downR=false;stepIndex=0;wasPast=false;}
        bool Down(bool was,R26SeatHand h)
        {if(!h.Ok)return false;if(h.Grip>=R26VehicleSeat.GripDown)return true;if(h.Grip<R26VehicleSeat.GripUp)return false;return was;}
        // True on the frame the pull completes. Positions are rig-local; rest is the handle at zero pull.
        public bool Step(float dt,float restX,float restY,float restZ,R26SeatHand left,R26SeatHand right)
        {
            HapticSteps=0;CrossedThreshold=false;GrabbedNow=false;
            if(Ejected||!R26VehicleSeat.Finite(dt)||dt<=0||dt>.5f||!R26VehicleSeat.Finite(restX+restY+restZ))return false;
            bool nl=Down(downL,left),nr=Down(downR,right);bool edgeL=nl&&!downL,edgeR=nr&&!downR;downL=nl;downR=nr;
            if(!Grabbed){
                float best=R26VehicleSeat.GrabRadius*R26VehicleSeat.GrabRadius;int pick=-1;
                for(int h=0;h<2;h++){
                    var hand=h==0?left:right;if(!(h==0?edgeL:edgeR)||!hand.Ok)continue;
                    float dx=hand.X-restX,dy=hand.Y-(restY-Pull),dz=hand.Z-restZ,d=dx*dx+dy*dy+dz*dz;
                    if(d<=best){best=d;pick=h;}
                }
                if(pick>=0){Grabbed=true;GrabbedNow=true;Hand=pick;startY=(pick==0?left:right).Y+Pull;held=0;stepIndex=(int)(Pull/R26VehicleSeat.HapticStep);}
            }
            if(Grabbed){
                var hand=Hand==0?left:right;bool down=Hand==0?downL:downR;
                float dx=hand.X-restX,dz=hand.Z-restZ;
                if(!hand.Ok||!down||dx*dx+dz*dz>R26VehicleSeat.GrabLoseRadius*R26VehicleSeat.GrabLoseRadius){Grabbed=false;}
                else{
                    Pull=Math.Max(0,Math.Min(R26VehicleSeat.MaxPull,startY-hand.Y));held+=dt;
                    int index=(int)(Pull/R26VehicleSeat.HapticStep);if(index>stepIndex)HapticSteps=index-stepIndex;stepIndex=index;
                    bool past=R26VehicleSeat.PullEjects(Pull);if(past&&!wasPast)CrossedThreshold=true;wasPast=past;
                    if(past&&held>=R26VehicleSeat.MinHoldSeconds){Ejected=true;Grabbed=false;return true;}
                }
            }
            if(!Grabbed&&Pull>0){Pull*=(float)Math.Exp(-dt/R26VehicleSeat.SpringSeconds);if(Pull<.001f)Pull=0;wasPast=false;stepIndex=0;}
            return false;
        }
    }
}

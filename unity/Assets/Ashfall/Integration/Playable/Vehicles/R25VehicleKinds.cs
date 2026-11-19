using System;
using Ashfall.Core;
namespace Ashfall.Integration.Playable.Vehicles
{
    public static class R25VehicleKinds
    {
        public static bool Valid(string kind) { return kind=="hoverbike"||kind=="tank"||kind=="helicopter"; }
        public static float Health(string kind) { return kind=="tank"?1800:kind=="helicopter"?600:300; }
        public static float BulletScale(string kind) { return kind=="tank"?.25f:1; }
        public const float GunTraverseDegrees=40f,TankMuzzleHeight=.5f,TankBarrelLength=3.46f;
        const double Radians=Math.PI/180;
        static readonly float GunConeCos=(float)Math.Cos(GunTraverseDegrees*Radians),GunConeSin=(float)Math.Sin(GunTraverseDegrees*Radians);
        public static float RestGunPitch(string kind){return kind=="helicopter"?-5f:kind=="tank"?4.6f:0f;}
        public static Float3 GunPivot(string kind){return kind=="helicopter"?new Float3(0,-.232f,1.102f):new Float3(0,.1f,.95f);}
        public static float GunBarrelLength(string kind){return kind=="helicopter"?.55f:.45f;}
        public static Float3 AimDirection(float yaw,float pitch)
        {
            double a=yaw*Radians,p=pitch*Radians;
            return new Float3((float)(Math.Sin(a)*Math.Cos(p)),(float)Math.Sin(p),(float)(Math.Cos(a)*Math.Cos(p)));
        }
        public static float AimYaw(Float3 direction){return (float)(Math.Atan2(direction.X,direction.Z)/Radians);}
        public static float AimPitch(Float3 direction){return (float)(Math.Atan2(direction.Y,Math.Sqrt(direction.X*direction.X+direction.Z*direction.Z))/Radians);}
        // Inputs are unit vectors. The mount's forward axis is local +Z.
        public static bool GunDirectionAllowed(Float3 direction,float hullYaw)
        {return direction.IsFinite&&direction.X*Math.Sin(hullYaw*Radians)+direction.Z*Math.Cos(hullYaw*Radians)>=GunConeCos-.00001f;}
        public static Float3 ClampGunDirection(Float3 local)
        {
            if(GunDirectionAllowed(local,0))return local;
            float radial=(float)Math.Sqrt(local.X*local.X+local.Y*local.Y);
            if(radial<.00001f)return new Float3(GunConeSin,0,GunConeCos);
            return new Float3(local.X/radial*GunConeSin,local.Y/radial*GunConeSin,GunConeCos);
        }
        public static Float3 TankMuzzle(Float3 root,Float3 direction)
        {return new Float3(root.X+direction.X*TankBarrelLength,root.Y+TankMuzzleHeight+direction.Y*TankBarrelLength,root.Z+direction.Z*TankBarrelLength);}
        public const float TankReloadSeconds=1; // owner 2026-11-12: was 4 s
        public static float Turn(float current,float target,float seconds,float rate)
        { float d=(target-current)%360;if(d>180)d-=360;if(d< -180)d+=360;return (current+Math.Max(-rate*seconds,Math.Min(rate*seconds,d))+360)%360; }
        public static bool LargeMap(string map,float width,float depth)
        { return R24MapAllowed(map)&&width>=100&&depth>=100; }
        static bool R24MapAllowed(string map)
        {return !String.IsNullOrEmpty(map)&&map.IndexOf("indoor",StringComparison.OrdinalIgnoreCase)<0&&map.IndexOf("paintball",StringComparison.OrdinalIgnoreCase)<0;}
    }
    // Planted motor: body yaw is the steering target; camera rotation is never written.
    public sealed class R25TankDynamics
    {
        public Float3 Velocity {get;private set;}
        public Float3 Displacement {get;private set;}
        public float Yaw {get;private set;}
        public void Reset(float yaw,Float3 velocity){Yaw=yaw;Accept(velocity);}
        public void Accept(Float3 velocity){Velocity=velocity.IsFinite?velocity:new Float3();}
        public void Step(float dt,float throttle,float yaw,bool ground,float height,Float3 normal)
        {
            Displacement=new Float3();
            if(!Finite(dt)||dt<=0||dt>.1f||!Finite(throttle)||!Finite(yaw)||!Finite(height)||!normal.IsFinite)return;
            Yaw=R25VehicleKinds.Turn(Yaw,yaw,dt,35);
            float s=(float)Math.Sin(Yaw*Math.PI/180),c=(float)Math.Cos(Yaw*Math.PI/180);
            float target=Math.Max(-1,Math.Min(1,throttle))*(throttle<0?3:7);
            float forward=Velocity.X*s+Velocity.Z*c;
            float speed=forward+Math.Max(-8*dt,Math.Min(8*dt,target-forward));
            float x=s*speed,z=c*speed,dx=x-Velocity.X,dz=z-Velocity.Z;
            float change=(float)Math.Sqrt(dx*dx+dz*dz);if(change>8*dt){x=Velocity.X+dx*8*dt/change;z=Velocity.Z+dz*8*dt/change;}
            float vy=Velocity.Y-16.2f*dt;
            if(ground&&normal.Y>0&&normal.Y/Math.Sqrt(normal.X*normal.X+normal.Y*normal.Y+normal.Z*normal.Z)>=Math.Cos(30*Math.PI/180)&&height<1.2f)
                {float targetY=-(normal.X*x+normal.Z*z)/normal.Y+Math.Max(-2,Math.Min(2,(.55f-height)*8));vy=Velocity.Y+Math.Max(-20*dt,Math.Min(20*dt,targetY-Velocity.Y));}
            Velocity=new Float3(x,vy,z);Displacement=new Float3(x*dt,vy*dt,z*dt);
        }
        static bool Finite(float n){return !Single.IsNaN(n)&&!Single.IsInfinity(n);}
    }
}

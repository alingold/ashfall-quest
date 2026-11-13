using System;
using Ashfall.Core;
namespace Ashfall.Integration.Playable.Vehicles
{
    // Timers latch until release: denial never floods the reliable command queue.
    public sealed class R25BikeHold
    {
        float elapsed; bool sent;
        public bool Step(bool held, float seconds, float threshold)
        {
            if (!held) { elapsed=0; sent=false; return false; }
            if (Single.IsNaN(seconds)||Single.IsInfinity(seconds)||seconds<=0||seconds>.1f) return false;
            elapsed+=seconds;
            if(sent||elapsed+.000001f<threshold)return false;
            sent=true;return true;
        }
    }
    public static class R25BikeRider
    {
        public const float MountReach=.6f;
        public static Float3 MountOffset(string kind,int side)
        {return kind=="tank"?new Float3(side*1.62f,.55f,.15f):kind=="helicopter"?new Float3(side*1.02f,.55f,.15f):new Float3(0,.55f,.55f);}
        public static Float3 MountPoint(Float3 rest,float yaw,string kind,int side)
        {
            var p=MountOffset(kind,side);double a=yaw*Math.PI/180;
            return new Float3(rest.X+p.X*(float)Math.Cos(a)+p.Z*(float)Math.Sin(a),rest.Y+p.Y,rest.Z-p.X*(float)Math.Sin(a)+p.Z*(float)Math.Cos(a));
        }
        public static bool Allowed(bool rule,bool tracked,bool blocked,bool ghost,bool carrier)
        {return rule&&tracked&&!blocked&&!ghost&&!carrier;}
        public static Float3 Remote(Float3 root,Float3 velocity,double age)
        {
            if(!root.IsFinite||!velocity.IsFinite||Double.IsNaN(age)||Double.IsInfinity(age))return root;
            float t=(float)Math.Max(0,Math.Min(.15,age));
            return new Float3(root.X+velocity.X*t,root.Y+velocity.Y*t,root.Z+velocity.Z*t);
        }
        public static float Yaw(float x,float y,float z,float w)
        {return (float)(Math.Atan2(2*(w*y+x*z),1-2*(y*y+z*z))*180/Math.PI);}
        public static void Drive(R25BikeDynamics motor,float dt,float x,float y,float yaw,bool boost,bool hop,bool ground,float height,Float3 normal)
        {motor.Step(dt,y,x,yaw*(float)Math.PI/180,boost,hop,ground,height,normal);}
    }
}

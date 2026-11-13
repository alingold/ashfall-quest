using System;
namespace Ashfall.Integration.Playable.Input
{
    public struct R23AimPoseSample
    {
        public readonly bool Tracked;public readonly int TrackingBits;
        public readonly float X,Y,Z,Qx,Qy,Qz,Qw;
        public R23AimPoseSample(bool tracked,int bits,float x,float y,float z,float qx,float qy,float qz,float qw)
        {Tracked=tracked;TrackingBits=bits;X=x;Y=y;Z=z;Qx=qx;Qy=qy;Qz=qz;Qw=qw;}
    }
    public static class R23AimPoseRules
    {
        // UnityEngine.XR.InputTrackingState.Position=1, Rotation=2.
        public const int RequiredPoseBits=3;
        static bool Finite(float value){return !float.IsNaN(value)&&!float.IsInfinity(value);}
        public static bool TryNormalize(R23AimPoseSample source,out R23AimPoseSample pose)
        {
            pose=new R23AimPoseSample();
            if(!source.Tracked||(source.TrackingBits&RequiredPoseBits)!=RequiredPoseBits||!Finite(source.X)||!Finite(source.Y)||!Finite(source.Z)||!Finite(source.Qx)||!Finite(source.Qy)||!Finite(source.Qz)||!Finite(source.Qw))return false;
            double length=Math.Sqrt((double)source.Qx*source.Qx+(double)source.Qy*source.Qy+(double)source.Qz*source.Qz+(double)source.Qw*source.Qw);if(length<.000001)return false;
            pose=new R23AimPoseSample(true,source.TrackingBits,source.X,source.Y,source.Z,(float)(source.Qx/length),(float)(source.Qy/length),(float)(source.Qz/length),(float)(source.Qw/length));return true;
        }
    }
}

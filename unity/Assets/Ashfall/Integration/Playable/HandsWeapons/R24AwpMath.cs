using System;using Ashfall.Core;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public static class R24AwpMath
    {
        public const float BaseMagnification=6.2f;
        // Source model.basis * Rx(-2.5deg) reflected ONCE into +Z Unity.
        public static R23ShotPose Calibrated(R23ShotPose model)
        {if(!model.Valid)throw new ArgumentException("finite model frame");float a=2.5f*(float)Math.PI/180,c=(float)Math.Cos(a),s=(float)Math.Sin(a);return new R23ShotPose(model.Point(R24AwpDefinition.Muzzle),model.Right,R23WeaponPoseMath.Add(R23WeaponPoseMath.Scale(model.Up,c),R23WeaponPoseMath.Scale(model.Forward,s)),R23WeaponPoseMath.Add(R23WeaponPoseMath.Scale(model.Forward,c),R23WeaponPoseMath.Scale(model.Up,-s)));}
        public static float OpticalFov(float magnification){if(magnification!=4&&magnification!=BaseMagnification&&magnification!=8)throw new ArgumentOutOfRangeException("magnification");return(float)(Math.Atan((.15/.45)/magnification)*360/Math.PI);}
    }
    public sealed class R24AwpZoom
    {
        bool forwardDown=true,session;public float Magnification{get;private set;}
        public R24AwpZoom(){Magnification=R24AwpMath.BaseMagnification;}
        public void Sample(float x,float y,bool scoped,bool valid)
        {if(!valid||!scoped||float.IsNaN(x)||float.IsNaN(y)||float.IsInfinity(x)||float.IsInfinity(y)){Reset();return;}if(!session){session=true;Magnification=R24AwpMath.BaseMagnification;forwardDown=y>.25f;}if(y<.25f)forwardDown=false;else if(y>.7f&&Math.Abs(x)<.5f&&!forwardDown){Magnification=Magnification==8?R24AwpMath.BaseMagnification:8;forwardDown=true;}}
        public void Reset(){Magnification=R24AwpMath.BaseMagnification;forwardDown=true;session=false;}
    }
}

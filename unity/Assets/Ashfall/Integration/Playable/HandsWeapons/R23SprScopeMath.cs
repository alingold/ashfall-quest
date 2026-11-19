using System;
using Ashfall.Core;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // spr_scope.gd angular stereo projection; +Z is reflected Godot forward.
    public static class R23SprScopeMath
    {
        public const float Width=.055f,Height=.0325f,Magnification=3,ReticleDepth=1;
        public const int PixelWidth=520,PixelHeight=307;
        static bool Finite(float value) { return !float.IsNaN(value)&&!float.IsInfinity(value); }
        public static bool Alignment(float relief,float radial,float gazeDot,bool clear) {
            return clear&&Finite(relief)&&Finite(radial)&&Finite(gazeDot)&&relief>=.05f&&relief<=.60f&&radial>=0&&radial<=.085f&&gazeDot>=.96f;
        }
        public static float HalfVerticalTangent(Float3[] cameraCorners) {
            float aspect=(float)PixelWidth/PixelHeight;double half=.001;
            foreach(var corner in cameraCorners) {
                if(!corner.IsFinite)throw new ArgumentException("Finite optical corners required");
                half=Math.Max(half,Math.Max(Math.Abs(corner.Y),Math.Abs(corner.X)/aspect)/(Math.Max(.001,corner.Z)*Magnification));
            }
            double degrees=Math.Max(1,Math.Min(179,Math.Atan(half)*360/Math.PI));return (float)Math.Tan(degrees*Math.PI/360);
        }
        public static Float3 ReticleCenter(R23ShotPose pose,Float3 lens){if(!pose.Valid||!lens.IsFinite)throw new ArgumentException("finite displayed SPR frame required");var plane=R23WeaponPoseMath.Add(lens,R23WeaponPoseMath.Scale(pose.Forward,ReticleDepth));var offset=R23WeaponPoseMath.Add(plane,R23WeaponPoseMath.Scale(pose.Origin,-1));return R23WeaponPoseMath.Add(pose.Origin,R23WeaponPoseMath.Scale(pose.Forward,R23WeaponPoseMath.Dot(offset,pose.Forward)));}
        public static Float3 TargetTangent(R23ShotPose pose,Float3 eye,Float3 target){if(!pose.Valid||!eye.IsFinite||!target.IsFinite)throw new ArgumentException("finite optical eye and target required");var delta=R23WeaponPoseMath.Add(target,R23WeaponPoseMath.Scale(eye,-1));float depth=Math.Max(.001f,R23WeaponPoseMath.Dot(delta,pose.Forward));return new Float3(R23WeaponPoseMath.Dot(delta,pose.Right)/depth,R23WeaponPoseMath.Dot(delta,pose.Up)/depth,0);}
        public static float SampleAngle(float apertureAngle,float reticleAngle,float targetAngle) { return (apertureAngle-reticleAngle)/Magnification+targetAngle; }
    }
}

using System;
using Ashfall.Core;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // Unit +Z-forward frame: reflected original model.global_transform. No Unity/hardware state.
    public struct R23ShotPose
    {
        public readonly Float3 Origin,Right,Up,Forward;
        public R23ShotPose(Float3 origin,Float3 right,Float3 up,Float3 forward){Origin=origin;Right=right;Up=up;Forward=forward;}
        public Float3 Point(Float3 local,float scale=1){return R23WeaponPoseMath.Add(Origin,R23WeaponPoseMath.Scale(Direction(local),scale));}
        public Float3 Direction(Float3 local){return R23WeaponPoseMath.Add(R23WeaponPoseMath.Scale(Right,local.X),R23WeaponPoseMath.Add(R23WeaponPoseMath.Scale(Up,local.Y),R23WeaponPoseMath.Scale(Forward,local.Z)));}
        public bool Valid {get{return Origin.IsFinite&&Right.IsFinite&&Up.IsFinite&&Forward.IsFinite&&Math.Abs(R23WeaponPoseMath.Dot(Right,Right)-1)<.002f&&Math.Abs(R23WeaponPoseMath.Dot(Up,Up)-1)<.002f&&Math.Abs(R23WeaponPoseMath.Dot(Forward,Forward)-1)<.002f&&Math.Abs(R23WeaponPoseMath.Dot(Right,Up))<.002f&&Math.Abs(R23WeaponPoseMath.Dot(Up,Forward))<.002f&&Math.Abs(R23WeaponPoseMath.Dot(Forward,Right))<.002f;}}
    }
    public static class R23WeaponPoseMath
    {
        public static Float3 Add(Float3 a,Float3 b){return new Float3(a.X+b.X,a.Y+b.Y,a.Z+b.Z);}
        public static Float3 Scale(Float3 a,float scale){return new Float3(a.X*scale,a.Y*scale,a.Z*scale);}
        public static float Dot(Float3 a,Float3 b){return a.X*b.X+a.Y*b.Y+a.Z*b.Z;}
        public static Float3 Cross(Float3 a,Float3 b){return new Float3(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);}
        static Float3 Unit(Float3 a){float size=(float)Math.Sqrt(Dot(a,a));return Scale(a,1/size);}
        static Float3 Rotate(Float3 value,Float3 axis,float angle){float c=(float)Math.Cos(angle),s=(float)Math.Sin(angle);return Add(Scale(value,c),Add(Scale(Cross(axis,value),s),Scale(axis,Dot(axis,value)*(1-c))));}
        public static bool UprightPulse(string id){return id=="spr"||id=="magnum"||id=="px4"||id=="1911"||id=="fal";}
        public static void Resolve(R23ShotPose neutral,Float3 sourceMuzzle,float modelScale,float distance,float pitch,float yaw,bool upright,out R23ShotPose model,out R23ShotPose shot){
            if(!neutral.Valid||!sourceMuzzle.IsFinite||!Finite(modelScale)||modelScale<=0||!Finite(distance)||!Finite(pitch)||!Finite(yaw))throw new ArgumentException("finite source model and unit tracked frame required");
            Float3 right,up,forward;
            if(upright){
                var worldUp=new Float3(0,1,0);var axis=Cross(neutral.Forward,worldUp);
                if(Dot(axis,axis)<.00000001f)axis=Add(new Float3(1,0,0),Scale(neutral.Forward,-neutral.Forward.X));
                axis=Unit(axis);float lower=-(float)Math.Acos(Math.Max(-1,Math.Min(1,-neutral.Forward.Y))),upper=(float)Math.Acos(Math.Max(-1,Math.Min(1,neutral.Forward.Y)));pitch=Math.Max(lower,Math.Min(upper,pitch));
                right=Rotate(neutral.Right,axis,pitch);up=Rotate(neutral.Up,axis,pitch);forward=Rotate(neutral.Forward,axis,pitch);
            }else{
                float cp=(float)Math.Cos(pitch),sp=(float)Math.Sin(pitch),cy=(float)Math.Cos(yaw),sy=(float)Math.Sin(yaw);
                // Godot default Euler YXZ, reflected once: Ry(-yaw)*Rx(-pitch).
                right=neutral.Direction(new Float3(cy,0,sy));up=neutral.Direction(new Float3(sy*sp,cp,-cy*sp));forward=neutral.Direction(new Float3(-sy*cp,sp,cy*cp));
            }
            model=new R23ShotPose(Add(neutral.Origin,Scale(neutral.Forward,-distance)),right,up,forward);
            shot=new R23ShotPose(model.Point(sourceMuzzle,modelScale),right,up,forward);
        }
        static bool Finite(float value){return !float.IsNaN(value)&&!float.IsInfinity(value);}
    }
    public struct R23WeaponGripFit
    {
        public readonly Float3 PrimaryModelPoint,PhysicalOffset;
        public readonly float PitchRadians;
        R23WeaponGripFit(Float3 point,Float3 offset,float pitch){PrimaryModelPoint=point;PhysicalOffset=offset;PitchRadians=pitch;}
        // player.gd get_hand_art_contact + downloaded_firearm_model overrides + playtest_fit_088.
        // These are glove contact points. The gun root itself is exactly corrected tracked grip.
        public static R23WeaponGripFit ForProfile(string id){
            var point=id=="smaw"?R24SmawRuntime.Grip:id=="px4"?new Float3(0,-.065f,.13f):id=="magnum"?new Float3(0,-.045f,.065f):id=="tec9"?new Float3(0,-.057f,.025f):new Float3(0,-.057f,-.025f);
            var offset=id=="knife"?new Float3(0,0,-.0508f):id=="smaw"?new Float3(0,.0254f,-.0508f):id=="katana"||id=="excalibur"||id=="cyberblade"||id=="px4"||id=="p90"?new Float3(0,0,-.0381f):id=="magnum"?new Float3(0,0,-.05715f):id=="m1014"?new Float3(0,.0381f,-.0381f):id=="mp5"?new Float3(0,-.0635f,0):id=="1911"?new Float3(0,-.0254f,0):id=="s85"?new Float3(0,.0762f,-.0508f):id=="mp9"?new Float3(0,0,.01905f):id=="mk18"?new Float3(0,-.0127f,-.0127f):id=="akm"?new Float3(0,0,-.0127f):new Float3();
            return new R23WeaponGripFit(point,offset,id=="s85"?35*(float)Math.PI/180:0);
        }
        public Float3 ModelContact(float modelScale){if(float.IsNaN(modelScale)||float.IsInfinity(modelScale)||modelScale<=0)throw new ArgumentOutOfRangeException("modelScale");return R23WeaponPoseMath.Add(PrimaryModelPoint,R23WeaponPoseMath.Scale(PhysicalOffset,1/modelScale));}
    }
}

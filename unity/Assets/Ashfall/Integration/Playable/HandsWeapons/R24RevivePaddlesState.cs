using System;using Ashfall.Core;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public sealed class R24ReviveTarget
    {
        public readonly string Id;public readonly R23ShotPose Basis;public readonly Float3 LeftPad,RightPad;
        public R24ReviveTarget(string id,R23ShotPose basis,Float3 leftPad,Float3 rightPad){Id=id;Basis=basis;LeftPad=leftPad;RightPad=rightPad;}
    }
    // Tool charge only. Root's callback must perform the actual completed revive;
    // returning an accepted network request is not a completed rescue.
    public sealed class R24RevivePaddlesState
    {
        public const float Fastest=5.5f,Slowest=10,ReadyWindow=3.5f;
        bool haveSample;Float3 previousLeft,previousRight;
        public bool Active{get;private set;}public bool Facing{get;private set;}public bool Visible{get;private set;}
        public float Charge{get;private set;}public float Remaining{get;private set;}public float ChargeDuration{get;private set;}
        public int Completions{get;private set;}public int SuccessfulRevives{get;private set;}public bool Ready{get{return Remaining>0;}}
        // Display the existing rescue window without consuming or extending charge authority.
        public float MeterFraction{get{return Math.Max(0,Math.Min(1,Ready?Remaining/ReadyWindow:Charge));}}
        // Resume a stopped ready cue at its original window position; never loop an exhausted clip.
        public bool TryReadyCueOffset(bool playing,float clipSeconds,out float offset){offset=0;if(!Active||!Visible||!Ready||playing||float.IsNaN(clipSeconds)||float.IsInfinity(clipSeconds)||clipSeconds<=0)return false;offset=Math.Max(0,ReadyWindow-Remaining);return offset<clipSeconds;}
        public void Equip(){Active=true;Clear();}public void Stow(){Active=false;Clear();}
        public void Restart(){Stow();Completions=SuccessfulRevives=0;}
        void Clear(){Charge=Remaining=0;haveSample=Facing=Visible=false;ChargeDuration=Slowest;}
        public static float Duration(float speed){return Slowest+(Fastest-Slowest)*Math.Max(0,Math.Min(1,speed/.5f));}
        public bool Step(float dt,bool allowed,bool paused,Float3 leftLocal,Float3 rightLocal,Float3 leftNormal,Float3 rightNormal){
            if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0||dt>.25f)throw new ArgumentOutOfRangeException("dt");if(!Active)return false;
            if(paused){Visible=false;haveSample=false;Facing=false;return false;}
            if(Ready){Remaining=Math.Max(0,Remaining-dt);if(Remaining==0)Clear();}
            Visible=allowed&&leftLocal.IsFinite&&rightLocal.IsFinite;
            if(!Visible){haveSample=false;Facing=false;return false;}
            float a=Length(leftNormal),b=Length(rightNormal);Facing=leftNormal.IsFinite&&rightNormal.IsFinite&&a>.001f&&b>.001f&&R23WeaponPoseMath.Dot(leftNormal,rightNormal)/(a*b)<=-.20f;
            bool completed=false;
            if(!Ready){
                if(!Facing)Charge=Math.Max(0,Charge-dt/2.5f);
                else if(haveSample&&dt>0&&dt<=.10f){float speed=Math.Max(Length(Sub(leftLocal,previousLeft)),Length(Sub(rightLocal,previousRight)))/dt;if(speed<=8){ChargeDuration=Duration(speed);Charge=Math.Min(1,Charge+dt/ChargeDuration);if(Charge>=1){Remaining=ReadyWindow;Completions++;completed=true;}}}
            }
            previousLeft=leftLocal;previousRight=rightLocal;haveSample=true;return completed;
        }
        public bool TryRevive(Float3 left,Float3 right,R24ReviveTarget target,Func<Float3,Float3,bool> lineClear,Func<string,bool> completeRevive){
            if(!Active||!Visible||!Ready||target==null||String.IsNullOrEmpty(target.Id)||!target.Basis.Valid||!left.IsFinite||!right.IsFinite||lineClear==null||completeRevive==null)return false;
            bool matches=(Contact(target.Basis,left,target.LeftPad)&&Contact(target.Basis,right,target.RightPad)&&lineClear(left,target.LeftPad)&&lineClear(right,target.RightPad))||(Contact(target.Basis,left,target.RightPad)&&Contact(target.Basis,right,target.LeftPad)&&lineClear(left,target.RightPad)&&lineClear(right,target.LeftPad));
            if(!matches||!completeRevive(target.Id))return false;SuccessfulRevives++;return true;
        }
        static bool Contact(R23ShotPose basis,Float3 point,Float3 pad){if(!pad.IsFinite)return false;var d=Sub(point,pad);float x=R23WeaponPoseMath.Dot(d,basis.Right),y=R23WeaponPoseMath.Dot(d,basis.Up),z=R23WeaponPoseMath.Dot(d,basis.Forward);return Math.Abs(x)<=.4f&&y>=-.2f&&y<=.9f&&z>=-.65f&&z<=.15f;}
        static Float3 Sub(Float3 a,Float3 b){return new Float3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);}static float Length(Float3 a){return (float)Math.Sqrt(R23WeaponPoseMath.Dot(a,a));}
    }
}

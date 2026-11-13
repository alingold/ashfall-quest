using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration.Playable.FlashSmoke;
namespace Ashfall.Integration.Playable.Explosives
{
    public sealed class R23ExplosiveTarget
    {public readonly string Id;public readonly Float3 Center;public R23ExplosiveTarget(string id,Float3 center){Id=id;Center=center;}}
    public interface IR23ExplosiveEffects
    {
        // Return real enemies/practice targets/constructed panels, excluding the player.
        // Enemy centers include source +.7m; source practice/panel centers are their roots.
        R23ExplosiveTarget[] ReadDamageTargets();
        bool ApplyTargetDamage(string targetId,float amount,Float3 origin);
        bool ApplyPlayerDamage(float amount,Float3 origin);
    }
    public sealed class R23GrenadeBlast
    {
        public readonly string ItemId,Kind;public readonly int Tick;public readonly Float3 Center;public readonly float Radius,Power;public readonly bool Falloff;
        public R23GrenadeBlast(string item,string kind,int tick,Float3 center,float radius,float power,bool falloff)
        {ItemId=item;Kind=kind;Tick=tick;Center=center;Radius=radius;Power=power;Falloff=falloff;}
        public float DamageAt(Float3 point)
        {if(!point.IsFinite||!Center.IsFinite||Radius<=0||Power<=0)return 0;float d=R23GrenadeMath.Length(R23GrenadeMath.Sub(point,Center));return d>Radius?0:Power*(Falloff?Math.Max(0,1-d/Radius):1);}
        public int Apply(IR23ExplosiveEffects effects,Float3 acceptedPlayerHead,float targetMultiplier=1)
        {
            if(effects==null)throw new ArgumentNullException("effects");if(Radius<=0)return 0;if(Single.IsNaN(targetMultiplier)||Single.IsInfinity(targetMultiplier)||targetMultiplier<0)targetMultiplier=0;
            var targets=effects.ReadDamageTargets()??new R23ExplosiveTarget[0];var decisions=new List<KeyValuePair<string,float>>();var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var target in targets)if(target!=null&&!String.IsNullOrEmpty(target.Id)&&seen.Add(target.Id)){float amount=DamageAt(target.Center);if(amount>0)decisions.Add(new KeyValuePair<string,float>(target.Id,amount*targetMultiplier));}
            float self=DamageAt(acceptedPlayerHead);int applied=0;
            // Original main.gd snapshots all target amounts BEFORE mutating any panel/health.
            // Original blast policy has no line-of-sight exclusion; do not invent one here.
            foreach(var target in decisions)if(effects.ApplyTargetDamage(target.Key,target.Value,Center))applied++;
            if(self>0&&effects.ApplyPlayerDamage(self,Center))applied++;return applied;
        }
    }
    public static class R23GrenadeMath
    {
        public const float Gravity=9.8f,CollisionRadius=.035f,FlatRange=29.2608f*48/41.5f;
        public static Float3 Add(Float3 a,Float3 b){return new Float3(a.X+b.X,a.Y+b.Y,a.Z+b.Z);}
        public static Float3 Sub(Float3 a,Float3 b){return new Float3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);}
        public static Float3 Scale(Float3 a,float b){return new Float3(a.X*b,a.Y*b,a.Z*b);}
        public static float Dot(Float3 a,Float3 b){return a.X*b.X+a.Y*b.Y+a.Z*b.Z;}
        public static float Length(Float3 a){return (float)Math.Sqrt(Dot(a,a));}
        public static Float3 LaunchVelocity(Float3 aim,Float3 right,float height)
        {
            if(!aim.IsFinite||!right.IsFinite||Single.IsNaN(height)||Single.IsInfinity(height)||Length(aim)<.001f)return new Float3();
            aim=Scale(aim,1/Length(aim));right=Sub(right,Scale(aim,Dot(right,aim)));if(Length(right)<.001f)right=new Float3(1,0,0);else right=Scale(right,1/Length(right));
            // Reuse the existing real source launch implementation, not a second ballistic fit.
            return WeaponHandling.SourceVelocity(new SourceLaunchGeometry("original-r23-grenade",new Float3(),aim,right,Math.Max(0,height),Gravity,200,1),false);
        }
        public static Float3 GroundRollVelocity(Float3 velocity,Float3 normal,float dt,float travel)
        {
            float dot=Dot(velocity,normal);var along=Sub(velocity,Scale(normal,dot));float length=Length(along);
            along=travel>=3.048f||length<=4*Math.Max(0,dt)?new Float3():Scale(along,(length-4*Math.Max(0,dt))/length);
            return Add(along,Scale(normal,Math.Min(0,dot)));
        }
        public static double[] WarningTimes(double duration)
        {
            if(Double.IsNaN(duration)||Double.IsInfinity(duration)||duration<=.18)return new double[0];int count=Math.Max(3,(int)Math.Ceiling(duration*3.2));var result=new double[count];
            for(int i=0;i<count;i++)result[i]=.09+(duration-.18)*(1-Math.Pow(1-(double)i/(count-1),1.65));return result;
        }
    }
    public struct R23GrenadeFrame {public bool Warning,Activated,Detonated,Ended;public float Radius;public int DamageTicks;}
    public sealed class R23GrenadeClock
    {
        readonly string id,kind;double age,activation,zoneAge,stuckAge=-1,emitAt=-1;int ticks,nextWarning;double[] warnings;bool stuck,active,ended;
        public bool Stuck {get{return stuck;}}public bool Active {get{return active;}}public bool Ended {get{return ended;}}
        public float Age {get{return(float)age;}}public float ZoneAge {get{return(float)zoneAge;}}public int ZoneTicks {get{return ticks;}}public int WarningEvents {get;private set;}
        public float Radius {get{return !active?0:kind=="smoke"?R25SmokeCloud.Radius((float)zoneAge):Math.Max(.001f,15*(1-(float)zoneAge/10));}}
        // flashsmoke: smoke emission start (launch-relative age, -1 before) and cloud age.
        public string Kind {get{return kind;}}public float EmitAt {get{return kind=="smoke"&&active?(float)emitAt:-1;}}public float CloudAge {get{return kind=="smoke"&&active?(float)zoneAge:0;}}
        public R23GrenadeClock(string item,string type){if(!R23GrenadeState.ValidKind(type))throw new ArgumentException("grenade kind");id=item;kind=type;warnings=type=="frag"?R23GrenadeMath.WarningTimes(3.5):new double[0];}
        public bool Contact(){if(ended||stuck||kind!="zone"&&kind!="smoke")return false;stuck=true;if(kind=="smoke"){stuckAge=age;return true;}warnings=R23GrenadeMath.WarningTimes(2.7);nextWarning=0;return true;}
        bool Warn(double clock,double duration)
        {bool due=false;while(nextWarning<warnings.Length&&warnings[nextWarning]<=clock+.000001){nextWarning++;WarningEvents++;due=true;}return due&&clock<duration;}
        public R23GrenadeFrame Advance(float seconds,bool enabled,Float3 actualPosition,Action<R23GrenadeBlast> apply)
        {
            var frame=new R23GrenadeFrame{Radius=Radius,Ended=ended};if(ended||!enabled)return frame;
            if(Single.IsNaN(seconds)||Single.IsInfinity(seconds)||seconds<0||seconds>.25f||!actualPosition.IsFinite)throw new ArgumentOutOfRangeException("source projectile step");age+=seconds;
            if(kind=="frag")
            {frame.Warning=Warn(age,3.5);if(age+.000001>=3.5){ended=true;frame.Detonated=true;if(apply!=null)apply(new R23GrenadeBlast(id,kind,0,actualPosition,10,150,true));}}
            // flashsmoke: flash pops at its fuse with no blast/damage; each viewer derives exposure.
            else if(kind=="flash")
            {if(age+.000001>=R25FlashExposure.Fuse){ended=true;frame.Detonated=true;}}
            // flashsmoke: smoke emits from min(impact+1.2 s, launch+2.0 s) for 16 s; no damage ticks.
            else if(kind=="smoke")
            {
                double start=R25SmokeCloud.EmissionStart((float)stuckAge);
                if(!active&&age+.000001>=start){active=true;emitAt=start;frame.Activated=true;}
                if(active){zoneAge=Math.Max(0,age-emitAt);if(zoneAge+.000001>=R25SmokeCloud.Life)ended=true;}
            }
            else if(stuck)
            {
                activation+=seconds;if(!active)frame.Warning=Warn(activation,2.7);
                if(!active&&activation+.000001>=2.7){active=true;frame.Activated=true;frame.Warning=false;}
                if(active)
                {
                    double target=Math.Min(10,zoneAge+Math.Min(seconds,Math.Max(0,activation-2.7)));
                    while((ticks+1)*.5<=target+.000001){ticks++;float radius=15*(1-ticks/20f);frame.DamageTicks++;if(radius>0&&apply!=null)apply(new R23GrenadeBlast(id,kind,ticks,actualPosition,radius,5,false));}
                    zoneAge=target;if(zoneAge+.000001>=10)ended=true;
                }
            }
            else if(age>=8)ended=true;
            frame.Ended=ended;frame.Radius=Radius;return frame;
        }
    }
}

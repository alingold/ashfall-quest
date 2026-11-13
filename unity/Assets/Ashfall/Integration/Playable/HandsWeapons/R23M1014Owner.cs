using System;
using Ashfall.Core;
using Ashfall.Modules.WeaponHandling;

namespace Ashfall.Integration.Playable.HandsWeapons
{
    public static class R23M1014Definition
    {
        public const string Id="m1014",AmmoFamily="shells";
        public const int Capacity=6,Pellets=8;
        public const float Interval=.29071666666667395f,PelletDamage=9.625f,Range=40,ModelScale=1;
        public static readonly Float3 Muzzle=new Float3(0,.045f,.77f),Offer=new Float3(0,-.25f,.14f),Socket=new Float3(0,-.10f,.14f),Handle=new Float3(.06f,.045f,.10f),Foregrip=new Float3(0,-.025f,.36f);
        public static float DamageAtDistance(float distance)
        {if(Single.IsNaN(distance)||Single.IsInfinity(distance)||distance<0||distance>Range)return 0;return PelletDamage*(1-.75f*Math.Max(0,Math.Min(1,(distance-8)/17)));}
    }
    public struct R23M1014Pose
    {
        // Actual accepted, recoiled muzzle frame, unit +Z forward. Never raw controller pose.
        public Float3 Origin,Forward,Right,Up;
        public bool Valid
        {get{return Origin.IsFinite&&Forward.IsFinite&&Right.IsFinite&&Up.IsFinite&&Math.Abs(Dot(Forward,Forward)-1)<.002f&&Math.Abs(Dot(Right,Right)-1)<.002f&&Math.Abs(Dot(Up,Up)-1)<.002f&&Math.Abs(Dot(Forward,Right))<.002f&&Math.Abs(Dot(Forward,Up))<.002f&&Math.Abs(Dot(Right,Up))<.002f;}}
        internal static float Dot(Float3 a,Float3 b){return a.X*b.X+a.Y*b.Y+a.Z*b.Z;}
    }
    // One immutable accepted discharge, after the real ammo owner has debited one shell.
    public sealed class R23M1014Volley
    {
        public readonly string InstanceId;public readonly CommandContext Command;public readonly long Shot;
        public readonly Float3 Origin;public readonly HandlingSnapshot Before,After;
        readonly Float3[] directions;
        public Float3[] Directions {get{return(Float3[])directions.Clone();}}
        public float PelletDamage {get{return R23M1014Definition.PelletDamage;}}
        public float Range {get{return R23M1014Definition.Range;}}
        internal R23M1014Volley(string instance,CommandContext command,R23M1014Pose pose,HandlingSnapshot before,HandlingSnapshot after,float spread=1)
        {
            InstanceId=instance;Command=command;Shot=after.Shots;Origin=pose.Origin;Before=before;After=after;directions=DirectionsFor(pose,spread);
        }
        public static Float3[] DirectionsFor(R23M1014Pose pose,float spread=1)
        {
            if(!pose.Valid||float.IsNaN(spread)||float.IsInfinity(spread)||spread<.6f||spread>1)throw new ArgumentException("finite unit M1014 muzzle frame required");var result=new Float3[8];
            for(int i=0;i<8;i++)
            {
                double angle=Math.PI*2*i/8,radius=Math.Tan(3.5*spread*Math.PI/180)*(i%2==0?.4:1),x=Math.Cos(angle)*radius,y=Math.Sin(angle)*radius;
                double a=pose.Forward.X+pose.Right.X*x+pose.Up.X*y,b=pose.Forward.Y+pose.Right.Y*x+pose.Up.Y*y,c=pose.Forward.Z+pose.Right.Z*x+pose.Up.Z*y,length=Math.Sqrt(a*a+b*b+c*c);
                result[i]=new Float3((float)(a/length),(float)(b/length),(float)(c/length));
            }
            return result;
        }
    }
    public sealed class R23M1014Owner
    {
        sealed class Initial:IWeaponLifetimeAuthority
        {
            readonly CommandContext creation;readonly string id;
            internal Initial(CommandContext c,string instance){creation=c;id=instance;}
            public RuleResult AuthorizeNew(CommandContext c,string instance,WeaponMechanism mechanism,int rounds)
            {return c.SessionId==creation.SessionId&&c.ActorId==creation.ActorId&&c.CommandId==creation.CommandId&&c.Tick==creation.Tick&&c.AuthorityEpoch==creation.AuthorityEpoch&&c.IsAuthority&&instance==id&&rounds==6?RuleResult.Accept():RuleResult.Reject("source M1014 initial entitlement");}
            public RuleResult AuthorizeRestore(CommandContext c,string instance,string actor,long epoch){return RuleResult.Reject("local retained owner; no transfer API");}
        }
        readonly WeaponHandling weapon;readonly IRevisionedAmmoReserve reserve;readonly float spread;bool retired;
        internal bool ReconcileNetworkAmmo(int rounds){return weapon.ReconcileNetworkMagazine(rounds);}
public string InstanceId {get;private set;}
        public bool ReloadGripInProgress {get{return weapon.R23M1014ReloadGripInProgress;}}
        public void Retire(){SuspendControls();retired=true;}
        public int Reserve {get{return reserve.Available;}}
        public HandlingSnapshot Snapshot {get{var s=weapon.Snapshot;s.ClaimsSupport=s.ClaimsSupport||s.Phase!=ReloadPhase.Ready;return s;}}
        public string ReloadCue {get;private set;}
        public float HapticAmplitude {get;private set;}public float HapticSeconds {get;private set;}public bool HapticSupport {get;private set;}
        public bool GripToggle {get{return weapon.GripToggle;}set{if(weapon.GripToggle!=value){weapon.GripToggle=value;SuspendControls();}}}
        public R23M1014Owner(CommandContext creation,string instanceId,IRevisionedAmmoReserve shells,IAuthorityGate authority,float spreadScale=1)
        {
            if(float.IsNaN(spreadScale)||float.IsInfinity(spreadScale)||spreadScale<.6f||spreadScale>1)throw new ArgumentException("source M1014 spread");spread=spreadScale;
            if(shells==null||authority==null||String.IsNullOrEmpty(instanceId))throw new ArgumentException("M1014 source identity/reserve/authority required");
            var lifetime=new WeaponLifetimes(creation.SessionId,new Initial(creation,instanceId));WeaponHandling active;
            var accepted=lifetime.CreateM1014(creation,instanceId,shells,authority,out active);if(!accepted.Accepted)throw new InvalidOperationException(accepted.Reason);
            weapon=active;reserve=shells;InstanceId=instanceId;
        }
        public void SuspendControls(){weapon.SuspendM1014Controls();ReloadCue=null;HapticAmplitude=HapticSeconds=0;}
        public RuleResult Apply(CommandContext command,HandlingAction action,HandlingInput input,R23M1014Pose firingPose,out R23M1014Volley volley,bool ejectAfterShot=false)
        {
            volley=null;if(retired)return RuleResult.Reject("retired M1014 instance");ReloadCue=null;HapticAmplitude=HapticSeconds=0;HapticSupport=false;var before=Snapshot;
            if(input.Enabled&&!input.Paused&&input.PrimaryTracked&&!input.PrimaryClimbing&&!input.HeadObstructed&&input.Trigger&&(action==HandlingAction.Fire||action==HandlingAction.Sample)&&!firingPose.Valid)return RuleResult.Reject("M1014 firing pose");
            var result=weapon.ApplyM1014(command,action,input,ejectAfterShot);var after=Snapshot;
            if(after.Shots==before.Shots+1){volley=new R23M1014Volley(InstanceId,command,firingPose,before,after,spread);HapticAmplitude=.7f;HapticSeconds=.07f;}
            if(after.Phase!=before.Phase)
            {
                ReloadCue=after.Phase==ReloadPhase.NeedsMagazine?"mag_eject":after.Phase==ReloadPhase.NeedsRack?"mag_insert":after.Phase==ReloadPhase.Ready?"bolt":null;
                HapticSupport=after.Phase!=ReloadPhase.NeedsMagazine;HapticAmplitude=after.Phase==ReloadPhase.NeedsMagazine?.24f:after.Phase==ReloadPhase.NeedsRack?.65f:.5f;HapticSeconds=after.Phase==ReloadPhase.NeedsRack?.085f:after.Phase==ReloadPhase.Ready?.08f:.07f;
            }
            return result;
        }
    }
}

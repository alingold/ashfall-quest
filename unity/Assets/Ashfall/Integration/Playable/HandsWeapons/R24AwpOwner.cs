using System;using Ashfall.Core;using Ashfall.Modules.WeaponHandling;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public static class R24AwpDefinition
    {
        public const string Id="awp",Family="sniper";public const int Capacity=3;public const float Interval=.95f,Scale=1,Range=SourceRifleDefinition.FirearmRange;
        public static readonly Float3 Muzzle=new Float3(0,.047f,1.01f),Bolt=new Float3(.072f,.045f,.07f),Offer=new Float3(0,-.1764f,.1061082762f),Insert=new Float3(0,-.18f,.1061082762f),Foregrip=new Float3(0,-.025f,.36f),MagazineHome=new Float3(.0002487176f,-.0128027467f,.1061082762f);
    }
    public sealed class R24AwpShot
    {
        public readonly string InstanceId;public readonly CommandContext Command;public readonly HandlingSnapshot Before,After;public readonly R23ShotPose Pose;public readonly bool Supported;
        internal R24AwpShot(string id,CommandContext command,HandlingSnapshot before,HandlingSnapshot after,R23ShotPose pose,bool supported){InstanceId=id;Command=command;Before=before;After=after;Pose=pose;Supported=supported;}
    }
    public sealed class R24AwpOwner
    {
        sealed class Initial:IWeaponLifetimeAuthority
        {
            readonly CommandContext creation;readonly string id;internal Initial(CommandContext c,string value){creation=c;id=value;}
            public RuleResult AuthorizeNew(CommandContext c,string instance,WeaponMechanism m,int rounds){return c.SessionId==creation.SessionId&&c.ActorId==creation.ActorId&&c.CommandId==creation.CommandId&&c.Tick==creation.Tick&&c.AuthorityEpoch==creation.AuthorityEpoch&&c.IsAuthority&&instance==id&&m==WeaponMechanism.PrecisionRifle&&rounds==3?RuleResult.Accept():RuleResult.Reject("source AWP3 entitlement");}
            public RuleResult AuthorizeRestore(CommandContext c,string id,string actor,long epoch){return RuleResult.Reject("retained local AWP owner");}
        }
        readonly WeaponHandling weapon;readonly IRevisionedAmmoReserve reserve;bool retired;
        internal bool ReconcileNetworkAmmo(int rounds){return weapon.ReconcileNetworkMagazine(rounds);}
public string InstanceId{get;private set;}public int Reserve{get{return reserve.Available;}}public HandlingSnapshot Snapshot{get{var s=weapon.Snapshot;s.ClaimsSupport|=s.Phase!=ReloadPhase.Ready;return s;}}
        public bool ReloadGripInProgress{get{return weapon.AwpReloadGripInProgress;}}public long CasingEvents{get{return weapon.AwpCasingEvents;}}public long MagazineEvents{get{return weapon.AwpMagazineEvents;}}
        public bool GripToggle{get{return weapon.GripToggle;}set{if(weapon.GripToggle!=value){weapon.GripToggle=value;SuspendControls();}}}
        public string ReloadCue{get;private set;}
        public R24AwpOwner(CommandContext creation,string id,IRevisionedAmmoReserve reserve,IAuthorityGate authority)
        {
            if(reserve==null||authority==null||String.IsNullOrEmpty(id)||id.Length>128)throw new ArgumentException("source AWP lifetime/finite reserve required");this.reserve=reserve;InstanceId=id;var ledger=new WeaponLifetimes(creation.SessionId,new Initial(creation,id));WeaponHandling active;
            var r=ledger.CreateNew(creation,id,WeaponMechanism.PrecisionRifle,3,reserve,authority,out active);if(!r.Accepted)throw new InvalidOperationException(r.Reason);weapon=active;weapon.SuspendAwpControls();
        }
        public void SuspendControls(){weapon.SuspendAwpControls();ReloadCue=null;}
        public void Retire(){SuspendControls();retired=true;}
        public RuleResult Apply(CommandContext command,HandlingAction action,HandlingInput input,R23ShotPose pose,out R24AwpShot shot)
        {
            shot=null;ReloadCue=null;if(retired)return RuleResult.Reject("retired AWP instance");
            if(input.Enabled&&!input.Paused&&input.PrimaryTracked&&!input.PrimaryClimbing&&!input.HeadObstructed&&input.Trigger&&!pose.Valid)return RuleResult.Reject("finite accepted AWP muzzle frame required");
            var before=Snapshot;var result=weapon.ApplyAwp(command,action,input);var after=Snapshot;
            if(after.Shots==before.Shots+1)shot=new R24AwpShot(InstanceId,command,before,after,pose,weapon.AwpLastShotSupported);
            if(before.Phase!=after.Phase)ReloadCue=after.Phase==ReloadPhase.NeedsMagazine?"mag_eject":after.Phase==ReloadPhase.HeldMagazine?"pouch":after.Phase==ReloadPhase.NeedsBolt&&before.Phase!=ReloadPhase.Ready?"mag_insert":after.Phase==ReloadPhase.Ready?"bolt":null;
            return result;
        }
    }
}

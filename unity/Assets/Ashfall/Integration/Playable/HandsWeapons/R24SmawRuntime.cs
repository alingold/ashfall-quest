using System;
using Ashfall.Core;
using Ashfall.Modules.WeaponHandling;
namespace Ashfall.Modules.WeaponHandling
{
    public sealed partial class WeaponHandling
    {public void SuspendR24SmawControls(){if(sourceMechanics!=null&&sourceMechanics.Profile=="smaw"&&!retired){Suspend();revision++;}}}
}
namespace Ashfall.Integration.Playable.HandsWeapons
{
    // One source lifetime: native SourceExplosive mechanics own chamber1 +local reload2.
    // No shared rocket pool or tactical loaded-round refund exists in R5.
    public sealed class R24SmawRuntime:IAuthorityGate,ISourceWeaponLifetimeAuthority,ISourceLaunchBinding
    {
        public const int Capacity=1,InitialReloads=2;public const float ModelScale=1.6f,RocketSpeed=200,Radius=4,ProjectileSeconds=SourceRifleDefinition.FirearmRange/RocketSpeed,CollisionRadius=.035f;
        public static readonly Float3 SourceMuzzle=new Float3(0,.045f,.85f),Grip=new Float3(0,-.105f,.55f),ModelOffset=new Float3(0,.111f,-.905f),Insert=new Float3(0,.045f,.96f);
        public static Float3 Offer {get{return new Float3(0,.045f,1.15f-.3048f/ModelScale);}}
        public static Float3 RearOffer {get{return new Float3(0,.045f,Offer.Z-.06f/ModelScale);}}
        const string Session="r24-local-smaw",Actor="player";readonly string id;readonly int stars;readonly WeaponLifetimes lifetimes;readonly WeaponHandling weapon;
        long tick,sequence,currentFrame;double seconds;bool retired,launchClear;SourceLaunchGeometry currentGeometry;
        public R24SmawRuntime(string instanceId,int rarity=1){if(String.IsNullOrEmpty(instanceId)||instanceId.Length>128||rarity<1||rarity>5)throw new ArgumentException("source SMAW instance/rarity");id=instanceId;stars=rarity;lifetimes=new WeaponLifetimes(Session,this);WeaponHandling created;var result=lifetimes.CreateSourceExplosive(Command(),id,"smaw",this,out created);if(!result.Accepted)throw new InvalidOperationException(result.Reason);weapon=created;SuspendControls();}
        CommandContext Command(){return new CommandContext(Session,Actor,"smaw/"+(++sequence),tick,0,true);}
        internal bool ReconcileNetworkAmmo(int loaded,int reloads){return reloads>=0&&reloads<=2&&weapon.ReconcileNetworkMagazine(loaded,reloads);}
        public string InstanceId {get{return id;}}public int Stars {get{return stars;}}
        public SourceExplosiveSnapshot Snapshot {get{SourceExplosiveSnapshot s;weapon.TryReadSourceExplosive(out s);return s;}}
        public int Loaded {get{return Snapshot.Loaded;}}public int Reloads {get{return Snapshot.Reloads;}}public bool Depleted {get{return Loaded==0&&Reloads==0;}}
        public bool ReloadHandOccupied {get{return Snapshot.HeldRocket;}}
        public float Damage {get{return new float[]{60,84,109,134,160}[stars-1];}}
        public HandlingSnapshot HandlingSnapshot {get{var s=Snapshot;return new HandlingSnapshot{Loaded=s.Loaded,Seated=0,Chambered=s.Loaded>0,Phase=s.Loaded>0?ReloadPhase.Ready:s.HeldRocket?ReloadPhase.HeldMagazine:ReloadPhase.NeedsMagazine,ClaimsSupport=s.HeldRocket,TriggerReleaseRequired=s.TriggerReleaseRequired,Shots=s.Shots,CooldownSeconds=(float)Math.Max(0,s.ReadyAtSeconds-s.SimulationSeconds)};}}
        public RuleResult Validate(CommandContext c){return !retired&&c.IsAuthority&&c.SessionId==Session&&c.ActorId==Actor&&c.AuthorityEpoch==0?RuleResult.Accept():RuleResult.Reject("local source SMAW");}
        public RuleResult AuthorizeNew(CommandContext c,string instance,WeaponMechanism mechanism,int loaded){return RuleResult.Reject("source creation only");}
        public RuleResult AuthorizeSourceNew(CommandContext c,string instance,string profile,int loaded,int reloads){return Validate(c).Accepted&&instance==id&&profile=="smaw"&&loaded==1&&reloads==2?RuleResult.Accept():RuleResult.Reject("one source lifetime entitlement");}
        public RuleResult AuthorizeRestore(CommandContext c,string instance,string actor,long epoch){return RuleResult.Reject("retained local owner");}
        public RuleResult Validate(CommandContext c,string instance,long equip,long life,long connection,long frame){return Validate(c).Accepted&&instance==id&&frame==currentFrame?RuleResult.Accept():RuleResult.Reject("current accepted source frame");}
        public RuleResult ValidateSource(CommandContext c,string instance,HandlingInput input,double clock,SourceLaunchGeometry geometry){return launchClear&&Object.ReferenceEquals(geometry,currentGeometry)&&clock==seconds&&input.Sequence==currentFrame?RuleResult.Accept():RuleResult.Reject("head-to-muzzle path or source pose unavailable");}
        public void SuspendControls(){if(weapon!=null)weapon.SuspendR24SmawControls();}
        public void Retire(){SuspendControls();retired=true;}
        public SourceLaunchDescriptor Step(float dt,HandlingInput frame,R23ShotPose pose,bool headToMuzzleClear,bool desktopReload=false){
            if(Single.IsNaN(dt)||Single.IsInfinity(dt)||dt<0||dt>.25f)throw new ArgumentOutOfRangeException("dt");if(retired)return null;if(!pose.Valid){SuspendControls();return null;}
            if(!frame.Paused)seconds+=dt;tick++;currentFrame=++sequence;frame.Sequence=currentFrame;frame.DeltaSeconds=dt;launchClear=headToMuzzleClear;
            currentGeometry=new SourceLaunchGeometry("R24R5-SMAW/"+id,pose.Origin,pose.Forward,pose.Right,0,9.8f,RocketSpeed,ModelScale);var c=Command();
            if(desktopReload){frame.Desktop=true;weapon.ReloadSourceDesktop(c,frame,seconds);return null;}
            if(frame.Trigger){IPreparedSourceLaunch prepared;var result=lifetimes.PrepareSourceRocketFire(c,id,new SourceRocketInput{Frame=frame,SimulationSeconds=seconds,Geometry=currentGeometry},0,0,0,c.Tick+1,this,out prepared);if(result.Accepted&&prepared!=null){if(prepared.Validate(c.Tick).Accepted){prepared.CommitValidated();if(prepared.Committed)return prepared.Descriptor;}prepared.Abort();}}
            weapon.SampleSourceExplosive(c,frame,seconds,currentGeometry);return null;
        }
    }
}

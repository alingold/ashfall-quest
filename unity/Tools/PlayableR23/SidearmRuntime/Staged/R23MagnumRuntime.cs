using System;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.WeaponHandling;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public struct R23MagnumInput
    {
        public readonly HandlingInput Mechanics;
        public readonly bool PrimaryButton,ManualClickAllowed,TriggerBlocksFlick,DesktopReload;
        public readonly float StickX,StickY;
        public R23MagnumInput(HandlingInput mechanics,bool primaryButton=false,bool manualClickAllowed=true,bool triggerBlocksFlick=false,float stickX=0,float stickY=0,bool desktopReload=false)
        {Mechanics=mechanics;PrimaryButton=primaryButton;ManualClickAllowed=manualClickAllowed;TriggerBlocksFlick=triggerBlocksFlick;StickX=stickX;StickY=stickY;DesktopReload=desktopReload;}
    }
    // One stable local weapon instance, not a catalog-selection respawn. Shared pistol
    // reserve is supplied by the round; no equip/drop/holster operation grants ammo.
    public sealed class R23MagnumRuntime:IAuthorityGate,IWeaponLifetimeAuthority
    {
        public const string Id="magnum",DisplayName="MAGNUM",AmmoFamily="pistol";
        public const int Capacity=7,InitialPistolReserve=1200;
        public const float Interval=1f/3f,BodyDamage=63,HeadDamage=81,ModelScale=.84f,MaximumRange=180;
        public static readonly Float3 SourceMuzzle=new Float3(0,.065f,.392f);
        readonly string instanceId;readonly IRevisionedAmmoReserve reserve;readonly WeaponHandling owner;readonly int starter;
        long tick,sequence;bool retired,previousClick=true,stickDown=true,previousDesktop=true,pendingShot;
        // Presentation-only occupancy, derived solely from accepted native loaded deltas.
        // The native owner's Loaded is the only mechanical ammunition count.
        int chamberMask,index;
        const string Session="r23-local-magnum",Actor="player";
        public static FiniteAmmoReserve CreatePistolReserve(int initial=InitialPistolReserve){return new FiniteAmmoReserve(int.MaxValue,initial);}
        public R23MagnumRuntime(IRevisionedAmmoReserve sharedPistolReserve,string stableInstanceId,int initialRounds=Capacity)
        {
            if(sharedPistolReserve==null||sharedPistolReserve.Available<0||string.IsNullOrEmpty(stableInstanceId)||stableInstanceId.Length>128||initialRounds<0||initialRounds>Capacity)throw new ArgumentException("finite pistol reserve and stable Magnum identity required");
            reserve=sharedPistolReserve;instanceId=stableInstanceId;starter=initialRounds;chamberMask=(1<<initialRounds)-1;
            var lifetimes=new WeaponLifetimes(Session,this);WeaponHandling created;var result=lifetimes.CreateNew(Command(),instanceId,WeaponMechanism.MagnumRevolver,initialRounds,reserve,this,out created);if(!result.Accepted)throw new InvalidOperationException(result.Reason);owner=created;SuspendControls();
        }
        CommandContext Command(){return new CommandContext(Session,Actor,"magnum/"+(++sequence),tick,0,true);}
        public RuleResult Validate(CommandContext command){return !retired&&command.IsAuthority&&command.SessionId==Session&&command.ActorId==Actor&&command.AuthorityEpoch==0?RuleResult.Accept():RuleResult.Reject("local Magnum only");}
        public RuleResult AuthorizeNew(CommandContext command,string id,WeaponMechanism mechanism,int rounds){return Validate(command).Accepted&&id==instanceId&&mechanism==WeaponMechanism.MagnumRevolver&&rounds==starter?RuleResult.Accept():RuleResult.Reject("explicit local starter only");}
        public RuleResult AuthorizeRestore(CommandContext command,string id,string actor,long epoch){return RuleResult.Reject("instance is retained, not reconstructed");}
        public string InstanceId {get{return instanceId;}}
        public HandlingSnapshot Snapshot {get{return owner.Snapshot;}}
        public int Loaded {get{return Snapshot.Loaded;}}
        public int Reserve {get{return reserve.Available;}}
        public int ChamberMask {get{return chamberMask;}}
        public bool ChamberLive(int slot){if(slot<0||slot>=Capacity)throw new ArgumentOutOfRangeException("slot");return (chamberMask&(1<<slot))!=0;}
        public bool ReloadHandOccupied {get{return owner.R23MagnumReloadGripInProgress;}}
        public bool SupportHandOccupied {get{return Snapshot.ClaimsSupport;}}
        public int OfferedRoundCount {get{return Snapshot.Phase==ReloadPhase.CylinderOpen?Math.Min(Capacity-Loaded,Reserve):0;}}
        public string LastReloadCue {get;private set;}
        public string ReloadInstruction {get{return Snapshot.Phase==ReloadPhase.CylinderOpen?"GRIP OFFERED ROUNDS / PUSH FORWARD":Snapshot.Phase==ReloadPhase.CylinderLoaded?"RELEASE THEN GRIP CYLINDER / PUSH UP OR RIGHT; OR FLICK PRIMARY HAND RIGHT":"TRIGGER FIRES / PRIMARY BUTTON OR STICK DOWN OPENS CYLINDER";}}
        public static Float3 Offer(bool left){return new Float3(left?.18f:-.18f,.095f,-.13f);}
        public static Float3 OpenPoint(bool left){return new Float3(left?.10f:-.10f,.035f,.10f);}
        public static Float3 CylinderOffset(bool left,bool open){return new Float3(open?(left?.10f:-.10f):0,0,0);}
        public void SuspendControls(){pendingShot=false;previousClick=previousDesktop=stickDown=true;owner.SuspendR23MagnumControls();}
        public void Retire(){SuspendControls();retired=true;}
        public bool Step(float seconds,R23MagnumInput frame)
        {
            if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<0||seconds>.25f||float.IsNaN(frame.StickX)||float.IsInfinity(frame.StickX)||float.IsNaN(frame.StickY)||float.IsInfinity(frame.StickY))throw new ArgumentOutOfRangeException("Magnum frame");
            pendingShot=false;LastReloadCue="";if(retired)return false;var input=frame.Mechanics;
            if(!input.Enabled||input.Paused||!input.PrimaryTracked||input.PrimaryClimbing||input.HeadObstructed){SuspendControls();return false;}
            bool click=frame.PrimaryButton&&!previousClick&&frame.ManualClickAllowed;
            float magnitude=frame.StickX*frame.StickX+frame.StickY*frame.StickY;if(magnitude<.25f*.25f)stickDown=false;
            bool down=!stickDown&&frame.StickY<-.7f&&Math.Abs(frame.StickX)<.5f;if(down)stickDown=true;
            bool desktop=frame.DesktopReload&&!previousDesktop;previousDesktop=frame.DesktopReload;previousClick=frame.PrimaryButton;
            var before=Snapshot;tick++;input.Sequence=++sequence;input.DeltaSeconds=seconds;
            owner.ApplyR23Magnum(Command(),input,click||down,desktop,frame.TriggerBlocksFlick||input.Trigger);
            var after=Snapshot;pendingShot=after.Shots>before.Shots;
            if(pendingShot){int slot=index;while(!ChamberLive(slot))slot=(slot+1)%Capacity;chamberMask&=~(1<<slot);index=(slot+1)%Capacity;}
            int added=after.Loaded-before.Loaded+(pendingShot?1:0);for(int i=0;i<Capacity&&added>0;i++)if(!ChamberLive(i)){chamberMask|=1<<i;added--;}
            if(before.Phase!=ReloadPhase.Ready&&after.Phase==ReloadPhase.Ready)index=0;
            if(before.Phase!=after.Phase)LastReloadCue=after.Phase==ReloadPhase.CylinderOpen?"mag_eject":after.Phase==ReloadPhase.CylinderLoaded?"mag_insert":after.Phase==ReloadPhase.Ready?"bolt":"";
            return pendingShot;
        }
        // Same owner-side health operation as the existing local practice lane. Caller
        // supplies its real HealthService and world-occluded hit; never a second HP pool.
        // The caller drains health transitions after the operation, including rejection.
        public bool TryApplyHit(HealthService health,CommandContext command,IInteractionQuery query,string target,Float3 origin,Float3 point,bool headshot,out DamageReceipt receipt)
        {
            receipt=null;if(!pendingShot||retired)return false;pendingShot=false;
            if(health==null||query==null||string.IsNullOrEmpty(target)||!origin.IsFinite||!point.IsFinite)return false;
            float x=point.X-origin.X,y=point.Y-origin.Y,z=point.Z-origin.Z;if(x*x+y*y+z*z>MaximumRange*MaximumRange||!query.HasLineOfSight(command.ActorId,target,origin,point,MaximumRange))return false;
            var before=health.Read(target);if(before==null||before.Life!=LifeState.Alive)return false;float amount=headshot?HeadDamage:BodyDamage,shield=Math.Min(before.Shield,amount),loss=Math.Min(before.Health,amount-shield);
            receipt=health.ApplyDamage(command,command.CommandId,target,before.Revision,loss,shield,false);return receipt.Result.Accepted;
        }
    }
}

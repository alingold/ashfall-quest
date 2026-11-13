using System;
using Ashfall.Core;
using Ashfall.Modules.Combat;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.WeaponHandling;
namespace Ashfall.Integration.DesktopPlaytest.Combat
{
    // Explicitly synthetic single-local-lane entitlement. Never a network authority adapter.
    public sealed class LocalRifleSandbox : IAuthorityGate, IWeaponLifetimeAuthority, IHealthWorld, ICombatEvidence, ICombatApplication
    {
        const string Session = "desktop-synthetic-rifle-v1", Actor = "local-player";
        readonly WeaponHandling weapon;
        readonly IAmmoReserve reserve;
        readonly SourceRifleDefinition sourceRifle;
        readonly string instanceId;
        int burstRemaining;
        bool burstArmed=true;
        readonly HealthService health;
        readonly CombatRules combat;
        readonly WeaponLease lease;
        // Bounded, non-destructive local journal. Each reader owns its own disposition.
        readonly FeedbackReceipt[] feedback=new FeedbackReceipt[32];
        long feedbackSequence;
        DamageDecision applyingDecision;
        HealthSnapshot applyingBefore;
        DamageReceipt appliedDamage;
        FeedbackReceipt preparedFeedback;
        bool feedbackRetired;
        public bool GripToggle {get{return weapon.GripToggle;}set{if(weapon.GripToggle!=value){weapon.GripToggle=value;SuspendControls();}}}
        public bool FeedbackRetired { get { return feedbackRetired; } }
        public void Retire() { feedbackRetired=true;SuspendControls(); }
        public sealed class FeedbackReceipt
        {
            public readonly LocalRifleSandbox Sandbox;
            public readonly long Sequence, Shot, LogicalEvent, EquipGeneration;
            public readonly string SessionId, ActorId, WeaponInstanceId, WeaponArchetype, TargetId, SubhitId;
            public readonly DamageSource Source;
            public readonly DamageKind Kind;
            public readonly HealthSnapshot Before;
            DamageReceipt actual;
            public HealthSnapshot After { get { return actual.Snapshot; } }
            public float HealthLoss { get { return (float)Math.Max(0,(double)Before.Health-After.Health); } }
            public float ShieldLoss { get { return (float)Math.Max(0,(double)Before.Shield-After.Shield); } }
            public bool Killed { get { return Before.Life==LifeState.Alive&&After.Life==LifeState.Dead; } }
            private FeedbackReceipt(LocalRifleSandbox owner,long sequence,DamageDecision d,HealthSnapshot before)
            {
                Sandbox=owner;Sequence=sequence;Shot=owner.weapon.Snapshot.Shots;LogicalEvent=d.LogicalEvent;
                SessionId=d.SessionId;ActorId=d.OwnerId;WeaponInstanceId=d.WeaponInstanceId;WeaponArchetype=d.WeaponArchetype;
                TargetId=d.TargetId;SubhitId=d.SubhitId;EquipGeneration=d.EquipGeneration;Source=d.Source;Kind=d.Kind;
                Before=before;
            }
            internal static FeedbackReceipt Prepare(LocalRifleSandbox owner)
            { return new FeedbackReceipt(owner,owner.feedbackSequence+1,owner.applyingDecision,owner.applyingBefore); }
            internal static void Record(LocalRifleSandbox owner)
            {
                // Only the owner can install this actual Health result. No external mint inputs.
                var actual=owner.appliedDamage;
                if(actual==null)return;
                owner.appliedDamage=null;
                var receipt=owner.preparedFeedback;owner.preparedFeedback=null;receipt.actual=actual;
                owner.feedback[(int)(owner.feedbackSequence%owner.feedback.Length)]=receipt;owner.feedbackSequence++;
            }
        }
        public long FeedbackSequence { get { return feedbackSequence; } }
        public enum FeedbackRead { Empty, Available, Gap }
        public FeedbackRead ReadFeedback(long after,out FeedbackReceipt receipt)
        {
            receipt=null;
            if(feedbackRetired)return FeedbackRead.Empty;
            if(after<0||after>feedbackSequence||after<feedbackSequence-feedback.Length)return FeedbackRead.Gap;
            if(after==feedbackSequence)return FeedbackRead.Empty;
            receipt=feedback[(int)(after%feedback.Length)];return FeedbackRead.Available;
        }
        HitEvidence current;
        long tick, sequence, serial;
        double now;
        bool reloading, pendingShot, requireTriggerRelease;
        float reloadClock;
        public LocalRifleSandbox(string[] targets, IInteractionQuery query)
            :this(targets,query,null,null,"local-rifle") { }
        // Source mode reuses the mechanical owner; definition is a bounded source profile,
        // reserve belongs to the loadout, and this stable instance is never recreated on selection.
        public LocalRifleSandbox(string[] targets,IInteractionQuery query,SourceRifleDefinition definition,IAmmoReserve sourceReserve,string stableInstanceId)
        {
            if(targets==null||targets.Length>63||query==null)throw new ArgumentException("sandbox targets/query");
            if(String.IsNullOrEmpty(stableInstanceId)||(definition!=null&&sourceReserve==null))throw new ArgumentException("source instance/reserve");
            sourceRifle=definition;instanceId=stableInstanceId;
            reserve=definition==null?new FiniteAmmoReserve(120,96):sourceReserve;
            var lifetimes=new WeaponLifetimes(Session,this);
            WeaponHandling created;
            Require(definition==null?lifetimes.CreateNew(Command(),instanceId,WeaponMechanism.ServiceRifle,24,reserve,this,out created):lifetimes.CreateSourceRifle(Command(),instanceId,definition,reserve,this,out created));
            weapon=created;
            health=new HealthService(Session,0,0,false,this,this,query,60,5,0);
            Require(health.Register(Command(),Actor,-1,false));
            foreach(string target in targets)Require(health.Register(Command(),target,-1,false));
            combat=new CombatRules(Session,0,256,4096,this,this,query,Actor);
            lease=combat.Equip(Command(),instanceId,Actor,definition==null?"service_rifle":definition.Id,1,tick);
        }
        static void Require(RuleResult result){if(!result.Accepted)throw new InvalidOperationException(result.Reason);}
        CommandContext Command(){return new CommandContext(Session,Actor,"local/"+(++sequence),tick,0,true);}
        public RuleResult Validate(CommandContext c){return !feedbackRetired&&c.IsAuthority&&c.SessionId==Session&&c.ActorId==Actor&&c.AuthorityEpoch==0?RuleResult.Accept():RuleResult.Reject("local sandbox entitlement only");}
        public RuleResult AuthorizeNew(CommandContext c,string id,WeaponMechanism m,int rounds){return Validate(c).Accepted&&id==instanceId&&m==WeaponMechanism.ServiceRifle&&rounds==(sourceRifle==null?24:sourceRifle.Capacity)?RuleResult.Accept():RuleResult.Reject("local starter grant only");}
        public RuleResult AuthorizeRestore(CommandContext c,string id,string actor,long epoch){return RuleResult.Reject("transfers disabled");}
        public double Now{get{return now;}}
        public bool InMatch{get{return true;}}
        public bool TeamChangesAllowed{get{return false;}}
        public bool TryGetPose(string id,out RescuePose pose){pose=new RescuePose();return false;}
        internal bool ReconcileNetworkAmmo(int rounds){return weapon.ReconcileNetworkMagazine(rounds);}
        public HandlingSnapshot Snapshot{get{return weapon.Snapshot;}}
        public int Reserve{get{return reserve.Available;}}
        public bool Reloading{get{return sourceRifle==null?reloading:weapon.Snapshot.Phase!=ReloadPhase.Ready;}}
        public int MagazineCapacity{get{return sourceRifle==null?24:sourceRifle.Capacity;}}
        public int LoadedRounds{get{var state=weapon.Snapshot;return sourceRifle!=null&&state.Phase==ReloadPhase.NeedsRack?state.Seated:state.Loaded;}}
        public string InstanceId{get{return instanceId;}}
        public string SourceWeaponId{get{return sourceRifle==null?"service_rifle":sourceRifle.Id;}}
        public int BurstRemaining{get{return burstRemaining;}}
        public HealthSnapshot Read(string id){return health.Read(id);}
        public void SuspendControls(){pendingShot=false;requireTriggerRelease=true;burstRemaining=0;burstArmed=false;if(sourceRifle!=null)weapon.SuspendSourceRifleControls(sourceRifle);}
        // Advance actual source mechanics once per frame; fire is never accompanied by a shadow debit.
        public bool Step(float dt,bool trigger,bool reload,bool paused)
        {
            if(sourceRifle!=null)return Step(dt,new HandlingInput{Trigger=trigger,Desktop=true,Enabled=true,PrimaryTracked=true,SupportTracked=true},reload,paused);
            if(feedbackRetired)return false;
            pendingShot=false;
            if(paused){SuspendControls();return false;}
            if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0||dt>.25f)throw new ArgumentOutOfRangeException("dt");
            if(requireTriggerRelease){if(!trigger&&!reload)requireTriggerRelease=false;else{trigger=false;reload=false;}}
            tick++;now+=dt;
            var input=new HandlingInput{Sequence=++sequence,DeltaSeconds=dt,Enabled=true,PrimaryTracked=true,SupportTracked=true,Desktop=true,Trigger=trigger};
            if(reload&&!reloading&&reserve.Available>0&&weapon.Snapshot.Loaded<24){reloading=true;reloadClock=0;}
            HandlingAction action=HandlingAction.Sample;
            if(reloading){input.Trigger=false;reloadClock-=dt;if(reloadClock<=0){action=HandlingAction.ReloadStep;reloadClock=.24f;}}
            else if(trigger)action=HandlingAction.Fire;
            long before=weapon.Snapshot.Shots;
            weapon.Apply(Command(),action,input);
            if(reloading&&weapon.Snapshot.Phase==ReloadPhase.Ready&&action==HandlingAction.ReloadStep)reloading=false;
            pendingShot=weapon.Snapshot.Shots>before;return pendingShot;
        }
        public bool Step(float dt,HandlingInput input,bool reload,bool paused)
        {
            if(sourceRifle==null)return Step(dt,input.Trigger,reload,paused);
            return StepSource(dt,input,reload?HandlingAction.ReloadStep:HandlingAction.Sample,paused);
        }
        // Typed source controls keep real VR eject distinct from the legacy desktop
        // staged-reload shortcut. Same native instance/counters/health owner.
        public bool StepSource(float dt,HandlingInput input,HandlingAction sourceAction,bool paused){
            if(sourceRifle==null)throw new InvalidOperationException("Source profile required");
            if(!Enum.IsDefined(typeof(HandlingAction),sourceAction))throw new ArgumentOutOfRangeException("sourceAction");
            bool reload=sourceAction==HandlingAction.ReloadStep||sourceAction==HandlingAction.Eject;
            pendingShot=false;if(feedbackRetired)return false;
            if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0||dt>.25f)throw new ArgumentOutOfRangeException("dt");
            if(paused||!input.Enabled||!input.PrimaryTracked||input.PrimaryClimbing||input.HeadObstructed){SuspendControls();return false;}
            tick++;now+=dt;
            bool held=input.Trigger;
            if(requireTriggerRelease){if(!held&&!reload){requireTriggerRelease=false;burstArmed=true;}else{held=false;reload=false;}}
            if(!held&&burstRemaining==0)burstArmed=true;
            bool starting=sourceRifle.BurstSize>1&&burstRemaining==0&&burstArmed&&held;
            bool requested=sourceRifle.BurstSize==1?held:burstRemaining>0||starting;
            if(requireTriggerRelease)requested=false;
            int beforeLoaded=weapon.Snapshot.Loaded;long before=weapon.Snapshot.Shots;
            bool final=sourceRifle.BurstSize>1&&(burstRemaining==1||(starting&&beforeLoaded==1));
            input.Sequence=++sequence;input.DeltaSeconds=dt;input.Trigger=requested;
            var action=reload?(sourceAction==HandlingAction.Eject?HandlingAction.Eject:HandlingAction.ReloadStep):requested?HandlingAction.Fire:HandlingAction.Sample;
            // The explicit reload button performs one source desktop step. Physical samples use
            // tracked offer -> upward insertion -> released grip -> rearward handle travel.
            if(reload){if(sourceAction==HandlingAction.ReloadStep)input.Desktop=true;input.Trigger=false;burstRemaining=0;}
            weapon.ApplySourceRifle(Command(),action,input,sourceRifle,final);
            pendingShot=weapon.Snapshot.Shots>before;
            if(pendingShot&&sourceRifle.BurstSize>1){burstRemaining=starting?Math.Min(sourceRifle.BurstSize,beforeLoaded)-1:Math.Max(0,burstRemaining-1);burstArmed=false;}
            else if(burstRemaining>0&&weapon.Snapshot.Phase!=ReloadPhase.Ready)burstRemaining=0;
            return pendingShot;
        }
        public RuleResult Hit(string target,Float3 origin,Float3 point)
        {
            if(!pendingShot)return RuleResult.Reject("no fresh local shot");
            pendingShot=false;
            HealthSnapshot state=health.Read(target);
            if(state==null||state.Life!=LifeState.Alive)return RuleResult.Reject("target unavailable");
            var ev=combat.PublishEvent(Command(),++serial,tick+1,DamageSource.CurrentActorIntent,DamageKind.Bullet,lease,tick);
            var hit=combat.PublishHit(Command(),ev,"ray",target,0,tick);
            current=new HitEvidence{Identity=hit,HealthRevision=state.Revision,TargetId=target,WeaponId=lease.Archetype,Kind=DamageKind.Bullet,
                ActorAlive=true,ActorInputReady=true,TargetAlive=true,MatchActive=true,Health=state.Health,Shield=state.Shield,
                BaseDamage=sourceRifle==null?25:sourceRifle.Id=="mk18"?22:20,DamageMultiplier=1,HeadRatio=1,Origin=origin,ViewOrigin=origin,Point=point,TargetHead=point,MaximumRange=100};
            var decision=combat.Evaluate(Command(),"current-ray",tick);
            applyingDecision=decision;applyingBefore=state;
            try { return decision.Accepted?combat.Commit(decision,this,tick):RuleResult.Reject(decision.Reason); }
            finally { current=null;applyingDecision=null;applyingBefore=null;preparedFeedback=null; }
        }
        bool ICombatEvidence.TryResolve(CommandContext c,string id,out HitEvidence evidence){evidence=current;return id=="current-ray"&&current!=null;}
        RuleResult ICombatApplication.TryApply(DamageDecision d)
        {
            if(d==null||!Object.ReferenceEquals(d,applyingDecision)||applyingBefore==null)return RuleResult.Reject("not this local commit");
            preparedFeedback=FeedbackReceipt.Prepare(this);
            var actual=health.ApplyDamage(Command(),"shot/"+d.LogicalEvent,d.TargetId,d.ExpectedRevision,d.HealthLoss,d.ShieldLoss,false);
            if(actual.Result.Accepted) {
                appliedDamage=actual;FeedbackReceipt.Record(this);
            }
            return actual.Result;
        }
    }
}


using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Modules.Pickups;
using Ashfall.Modules.Flight;
using Ashfall.Integration.InventoryTransfers;
namespace Ashfall.Integration.RuntimeLoop
{
    public interface IActorRoot : IRootCollisionPreflight
    {
        Float3 Position { get; }
        MotionEnvironment Environment { get; }
        // Sole root/collider writer; no-fail after Validate on the same host lane.
        // Returns authoritative post-collision velocity for the next Player step.
        Float3 Commit(long tick, RootMotionProposal proposal, Float3 velocity);
    }
    public interface IRuntimeActorBinding
    {
        string SessionId {get;} string CanonicalActorId {get;} string ConnectionId {get;}
        long AuthorityEpoch {get;} long LifeGeneration {get;} long ConnectionGeneration {get;} long RootGeneration {get;}
    }
    public enum TickDisposition { RejectedBeforeEffects, CommittedPending, Committed }
    public interface IRuntimeEvents
    {
        // Must deduplicate SessionId/AuthorityEpoch/Sequence on retries, including partial sink delivery.
        RuleResult Deliver(HealthTransition transition);
    }
    public enum RuntimeAction { None, Reserve, Collect, Equip, Fire, Reload, Damage, Charge, Revive, Respawn }
    public struct ActorFrame
    {
        public string Actor, InstanceOrHit, Target;
        public DeviceSample Device;
        public RuntimeAction Action;
        public RuntimeMotionMode MotionMode;
    }
    public sealed class ActorResult
    {
        public readonly string Actor;
        public readonly RuleResult Motion, Action;
        public readonly MotionStep Step;
        public ActorResult(string actor,RuleResult motion,RuleResult action,MotionStep step)
        { Actor=actor;Motion=motion;Action=action;Step=step; }
    }
    // One synchronous, non-reentrant lane. Fixed actor order is ordinal, independent of ingress order.
    // A tick is ordered composition, NOT an all-or-nothing transaction across modules.
    public sealed class HostLoop
    {
        sealed class Actor
        {
            public string Id, Connection; public IActorRoot Root; public long RootGeneration, LifeGeneration, ConnectionGeneration;
            public int SpawnRevision;
            public readonly PlayerInput Input=new PlayerInput();
            public readonly PlayerTuning Tuning=new PlayerTuning();
            public readonly Locomotion Motor;
            public PlayerSettingsPublication Settings;
            public Actor(){Motor=new Locomotion(Tuning);}
            public FlightMachine Flight; public readonly PadContacts Pads=new PadContacts();
        }
        readonly object lane=new object(); readonly SortedDictionary<string,Actor> actors=new SortedDictionary<string,Actor>(StringComparer.Ordinal);
        readonly string session,host;readonly long epoch;readonly IAuthorityGate gate;
        readonly HealthService health;readonly CombatHealthBridge combat;readonly TransferBridge inventory;readonly IRuntimeEvents events;
        readonly Ashfall.Modules.Sessions.Session sessions;readonly Ashfall.Integration.MatchDirector.Director director;
        long tick=-1;bool active,pending;
        public bool HasPendingPublication {get{return pending;}}
        public TickDisposition LastDisposition {get;private set;}
        public HostLoop(string session,long epoch,string host,IAuthorityGate gate,HealthService health,CombatHealthBridge combat,TransferBridge inventory,IRuntimeEvents events,
            Ashfall.Modules.Sessions.Session sessions=null,Ashfall.Integration.MatchDirector.Director director=null)
        {if(String.IsNullOrEmpty(session)||String.IsNullOrEmpty(host)||epoch<0||gate==null||health==null||combat==null||inventory==null||events==null)throw new ArgumentException("loop dependencies");
         this.session=session;this.epoch=epoch;this.host=host;this.gate=gate;this.health=health;this.combat=combat;this.inventory=inventory;this.events=events;this.sessions=sessions;this.director=director;}
        public RuleResult AddActor(string id,IActorRoot root){return health.Serialize(delegate{return AddActorSerialized(id,root);});}
        RuleResult AddActorSerialized(string id,IActorRoot root)
        {lock(lane){if(active||tick>=0||String.IsNullOrEmpty(id)||root==null||actors.ContainsKey(id)||actors.Count==64)return RuleResult.Reject("actor binding");
            foreach(var a in actors.Values)if(Object.ReferenceEquals(a.Root,root))return RuleResult.Reject("shared root");
            var initialHealth=health.Read(id);if(initialHealth==null||!initialHealth.Connected)return RuleResult.Reject("health member required");
            var candidate=new Actor{Id=id,Root=root};
            var binding=root as IRuntimeActorBinding;if(binding==null)return RuleResult.Reject("root lifecycle binding required");
            candidate.RootGeneration=binding.RootGeneration;
            candidate.Connection=binding.ConnectionId;candidate.LifeGeneration=binding.LifeGeneration;candidate.ConnectionGeneration=binding.ConnectionGeneration;
            if(sessions!=null){var member=sessions.Get(candidate.Connection);if(member==null)return RuleResult.Reject("session member required");candidate.SpawnRevision=member.SpawnRevision;
                var position=root.Position;if(!position.Equals(member.Position))return RuleResult.Reject("root spawn placement");}
            if(!Bound(candidate))return RuleResult.Reject("actor membership/lifecycle binding");
            candidate.Settings=new PlayerSettingsPublication(candidate.Input,candidate.Tuning,gate,session,id,epoch);
            candidate.Flight=new FlightMachine(session,id,epoch,gate,PlayerTuning.WalkSpeed,1);actors.Add(id,candidate);return RuleResult.Accept();}}
        // Trusted immutable settings publication uses the same input/tuning/motor
        // instances as Tick. Never target the independently owned native driver.
        public RuleResult PublishSettings(CommandContext c,long revision,PlayerTuning next,IPlayerSettingsPort effective,DominantHand dominant,SprintBinding sprint)
        {return health.Serialize(delegate{lock(lane){Actor a;
            if(active||pending||c.Tick<tick||!actors.TryGetValue(c.ActorId,out a)||!Bound(a))return RuleResult.Reject("settings actor/lane binding");
            var result=a.Settings.Publish(c,revision,next,effective,dominant,sprint);
            if(result.Accepted){a.Motor.Settings=a.Settings.Effective;a.Motor.OwnershipSprint=a.Input.Ownership.Sprint;}
            return result;
        }});}
        static bool Finite(float x){return !Single.IsNaN(x)&&!Single.IsInfinity(x);}
        bool Bound(Actor a)
        {
            var b=a.Root as IRuntimeActorBinding;var h=health.Read(a.Id);Ashfall.Modules.InventoryItems.InventorySnapshot inv;
            if(b==null||b.ConnectionId!=a.Connection||b.LifeGeneration!=a.LifeGeneration||b.ConnectionGeneration!=a.ConnectionGeneration)return false;
            if(b==null||h==null||!h.Connected||b.SessionId!=session||b.AuthorityEpoch!=epoch||b.CanonicalActorId!=a.Id||b.RootGeneration<1||b.RootGeneration!=a.RootGeneration||b.LifeGeneration!=h.LifeGeneration||b.ConnectionGeneration!=h.ConnectionGeneration||!inventory.TryInventorySnapshot(a.Id,out inv)||inv.SessionId!=session||inv.Epoch!=epoch||inv.ActorId!=a.Id||inv.Handoff)return false;
            if(sessions!=null){var m=sessions.Get(a.Connection);if(sessions.Id!=session||sessions.Epoch!=epoch||m==null||m.ActorId!=a.Connection||m.CanonicalActorId!=a.Id||m.Team!=h.Team||
                (sessions.Stage!=Ashfall.Modules.Sessions.SessionStage.Match&&sessions.Stage!=Ashfall.Modules.Sessions.SessionStage.Lobby)||
                !m.Loaded||m.WaitingRound||!m.HasPosition||!m.Position.IsFinite||m.SpawnRevision<1||m.SpawnRevision!=a.SpawnRevision||!h.Connected||
                (m.Life!=Ashfall.Modules.Sessions.SessionLife.Alive&&m.Life!=Ashfall.Modules.Sessions.SessionLife.Eliminated)||h.Life==LifeState.Spectator||
                (h.Life==LifeState.Alive&&m.Life!=Ashfall.Modules.Sessions.SessionLife.Alive))return false;}
            return true;
        }
        CommandContext C(string actor,string operation,long t){return new CommandContext(session,actor,"runtime/"+t+"/"+operation,t,epoch,true);}
        public RuleResult Tick(CommandContext authority,float dt,ActorFrame[] frames,out ActorResult[] results)
        {
            ActorResult[] output=null;
            RuleResult r=health.Serialize(delegate {lock(lane){return TickSerialized(authority,dt,frames,out output);}});
            results=output;return r;
        }
        RuleResult TickSerialized(CommandContext authority,float dt,ActorFrame[] frames,out ActorResult[] results)
        {
            results=new ActorResult[0];LastDisposition=TickDisposition.RejectedBeforeEffects;
            if(active)return RuleResult.Reject("reentrant host tick");
            if(pending)return RuleResult.Reject("pending publication: call Drain");
            if(authority.ActorId!=host||authority.SessionId!=session||authority.AuthorityEpoch!=epoch||!authority.IsAuthority||String.IsNullOrEmpty(authority.CommandId)||!gate.Validate(authority).Accepted||authority.Tick<=tick||
               Single.IsNaN(dt)||Single.IsInfinity(dt)||dt<=0||dt>.1f||frames==null||frames.Length!=actors.Count)return RuleResult.Reject("host tick envelope");
            var inputs=new Dictionary<string,ActorFrame>(StringComparer.Ordinal);
            foreach(var f in frames)if(f.Actor==null||!actors.ContainsKey(f.Actor)||inputs.ContainsKey(f.Actor)||f.Device.IsXr||!Enum.IsDefined(typeof(RuntimeAction),f.Action))return RuleResult.Reject("actor batch/desktop only");else inputs.Add(f.Actor,f);
            // Full composition cannot use the legacy trusted root Commit port as a
            // physical certificate. Refuse before input, timers, mechanics or ammo.
            foreach(var f in frames)if(f.MotionMode!=RuntimeMotionMode.PlayerOnly)
                return RuleResult.Reject(RuntimeMotionPreparation.PublicationBlocker);
            // Capture every stable port before any actor publication. No root reads during commit.
            var positions=new Dictionary<string,Float3>();var environments=new Dictionary<string,MotionEnvironment>();
            foreach(var a in actors.Values){
                if(!Bound(a))return RuleResult.Reject("actor membership/lifecycle drift");
                Float3 p=a.Root.Position;MotionEnvironment e=a.Root.Environment;
                if(!p.IsFinite||!Finite(e.HeadingRadians)||!Finite(e.DesktopYawRadians)||Math.Abs(e.DesktopYawRadians)>Math.PI)return RuleResult.Reject("root environment");
                e.IsXr=false;positions.Add(a.Id,p);environments.Add(a.Id,e);
            }
            // External producers may already have filled both queues. Drain before advancing any producer.
            var drained=Publish();if(!drained.Accepted){pending=true;return drained;}
            foreach(var a in actors.Values)if(!Bound(a))return RuleResult.Reject("actor lifecycle changed during drain");
            HealthService.PreparedAdvance advance;RuleResult prepared=health.PrepareAdvance(C(host,"advance",authority.Tick),out advance);if(!prepared.Accepted)return prepared;
            active=true;
            try {
                if(!advance.Validate())return RuleResult.Reject("health advance drift");
                foreach(var a in actors.Values)if(!Bound(a))return RuleResult.Reject("actor lifecycle changed before advance");
                // Timer projections publish first, so all actions observe projected life.
                advance.CommitValidated();tick=authority.Tick;LastDisposition=TickDisposition.CommittedPending;pending=true;
                inventory.Advance(tick);var output=new List<ActorResult>();
                var steps=new Dictionary<string,MotionStep>();var plans=new Dictionary<string,RootMotionProposal>();var moves=new Dictionary<string,RuleResult>();var sampled=new Dictionary<string,InputFrame>();
                foreach(var a in actors.Values){var f=inputs[a.Id];InputFrame input=a.Input.Read(tick,dt,f.Device);
                    HealthSnapshot life=health.Read(a.Id);bool alive=life.Connected&&life.Life==LifeState.Alive;
                    if(!alive){input.Paused=true;a.Input.Disarm();}
                    MotionStep step=a.Motor.Step(input,environments[a.Id],dt);
                    RootMotionProposal plan=RootMotionSafety.Plan(positions[a.Id],positions[a.Id],step.Displacement,step.YawRadians,step.OwnsMotion);
                    RuleResult moved=RootMotionSafety.Approve(plan,true,a.Root);
                    steps.Add(a.Id,step);plans.Add(a.Id,plan);moves.Add(a.Id,moved);sampled.Add(a.Id,input);
                }
                foreach(var a in actors.Values){var f=inputs[a.Id];var input=sampled[a.Id];var step=steps[a.Id];var moved=moves[a.Id];
                    var current=health.Read(a.Id);
                    if(moved.Accepted&&current.Connected&&current.Life==LifeState.Alive&&Bound(a)){Float3 v=a.Root.Commit(tick,plans[a.Id],step.Velocity);a.Motor.SetVelocity(v);}
                    else {a.Motor.CancelMotion();a.Input.Disarm();}
                    var life=health.Read(a.Id);bool ready=Bound(a)&&life.Connected&&life.Life==LifeState.Alive&&input.Tracked&&!input.Paused&&a.Input.DesktopReady&&moved.Accepted;
                    RuleResult action=RuleResult.Reject("input/life/collision not ready");
                    if(ready)action=Act(a,f,input,dt);
                    else {var selected=inventory.Inventory(a.Id);if(!String.IsNullOrEmpty(selected.SelectedInstanceId))inventory.ApplyWeapon(C(a.Id,"safety",tick),selected.SelectedInstanceId,HandlingAction.Sample,new HandlingInput{Sequence=input.Sequence,DeltaSeconds=dt,Enabled=false,Paused=true,Desktop=true});if(f.Action==RuntimeAction.None)action=RuleResult.Accept();}
                    output.Add(new ActorResult(a.Id,moved,action,step));
                }
                results=output.ToArray();return DrainSerialized();
            }finally{advance.Abort();active=false;}
        }
        public RuleResult Drain(){return health.Serialize(delegate {lock(lane){if(active)return RuleResult.Reject("reentrant drain");active=true;try{return DrainSerialized();}finally{active=false;}}});}
        RuleResult DrainSerialized()
        {
            // Drain is also callable for receipts from independent producers.
            var prefix=Publish();if(!prefix.Accepted){pending=true;LastDisposition=TickDisposition.CommittedPending;return RuleResult.Reject("committed/pending: "+prefix.Reason);}
            if(director!=null){var beat=director.Heartbeat();if(!beat.Accepted){pending=true;LastDisposition=TickDisposition.CommittedPending;return RuleResult.Reject("committed/pending: "+beat.Reason);}}
            var r=Publish();pending=!r.Accepted;LastDisposition=pending?TickDisposition.CommittedPending:TickDisposition.Committed;
            return pending?RuleResult.Reject("committed/pending: "+r.Reason):RuleResult.Accept();
        }
        RuleResult Act(Actor a,ActorFrame f,InputFrame input,float dt)
        {
            CommandContext c=C(a.Id,"action",tick);
            switch(f.Action){
            case RuntimeAction.None:var selected=inventory.Inventory(a.Id); if(selected!=null&&!String.IsNullOrEmpty(selected.SelectedInstanceId)) inventory.ApplyWeapon(c,selected.SelectedInstanceId,HandlingAction.Sample,new HandlingInput{Sequence=input.Sequence,DeltaSeconds=dt,Enabled=true,PrimaryTracked=true,SupportTracked=true,Desktop=true,Trigger=input.TriggerRight});return RuleResult.Accept();
            case RuntimeAction.Reserve:return inventory.Execute(c,PickupAction.Reserve,f.InstanceOrHit);
            case RuntimeAction.Collect:return inventory.Execute(c,PickupAction.Collect,f.InstanceOrHit);
            case RuntimeAction.Equip:return inventory.Equip(c,f.InstanceOrHit,false);
            case RuntimeAction.Fire:case RuntimeAction.Reload:
                if(f.Action==RuntimeAction.Fire&&!input.TriggerRight||f.Action==RuntimeAction.Reload&&!input.Reload)return RuleResult.Reject("action input mismatch");
                return inventory.ApplyWeapon(c,f.InstanceOrHit,f.Action==RuntimeAction.Fire?HandlingAction.Fire:HandlingAction.ReloadStep,
                    new HandlingInput{Sequence=input.Sequence,DeltaSeconds=dt,Enabled=true,PrimaryTracked=true,SupportTracked=true,Desktop=true,Trigger=input.TriggerRight});
            case RuntimeAction.Damage:return combat.Apply(c,f.InstanceOrHit,tick,true).Result;
            case RuntimeAction.Charge:return health.SamplePaddles(c);
            case RuntimeAction.Revive:var victim=health.Read(f.Target);return victim==null?RuleResult.Reject("missing target"):health.Revive(c,f.Target,victim.Revision);
            case RuntimeAction.Respawn:return RuleResult.Reject("UNKNOWN coordinated session/health/root lifecycle reservation");
            default:return RuleResult.Reject("unsupported action");}
        }
        RuleResult Publish()
        {
            var receipt=combat.Pump(C(host,"pump",Math.Max(0,tick)));
            // Pump authenticates before Capture. Pressure still carries a valid retained prefix.
            if(!receipt.Result.Accepted&&receipt.Result.Reason!="publication-pressure")return receipt.Result;
            var offered=receipt.Events;int deliveredCount=0;
            foreach(var e in offered){if(deliveredCount==256)return RuleResult.Reject("publication-continuation");
                if(director!=null){var routed=director.Deliver(e);if(!routed.Accepted)return routed;}
                RuleResult delivered=events.Deliver(e);if(!delivered.Accepted)return delivered;
                RuleResult ack=combat.Acknowledge(C(host,"ack/"+e.Sequence,Math.Max(0,tick)),e.Sequence);if(!ack.Accepted)return ack;deliveredCount++;}
            return receipt.PendingHealthEvents>0?RuleResult.Reject("publication-continuation"):RuleResult.Accept();
        }
        // Host observation ingress only; device input cannot manufacture pad geometry.
        public RuleResult LaunchPad(CommandContext c,LaunchPad pad,IFlightWorld world,double now){return health.Serialize(delegate{return LaunchPadSerialized(c,pad,world,now);});}
        RuleResult LaunchPadSerialized(CommandContext c,LaunchPad pad,IFlightWorld world,double now)
        {lock(lane){Actor a;if(active||pending||c.SessionId!=session||c.AuthorityEpoch!=epoch||!c.IsAuthority||!gate.Validate(c).Accepted||c.Tick!=tick||!actors.TryGetValue(c.ActorId,out a))return RuleResult.Reject("pad host lane");
            var h=health.Read(a.Id);if(!Bound(a)||h==null||!h.Connected||h.Life!=LifeState.Alive||!a.Input.DesktopReady)return RuleResult.Reject("pad eligibility");
            var r=a.Pads.TryLaunch(c,a.Flight,pad,world,a.Root.Position,a.Root.Position,false,now,true,false,false,false);
            if(r.Accepted)a.Motor.SetVelocity(a.Flight.Velocity);return r;}}
        public RuleResult RecordPosition(CommandContext c){return health.Serialize(delegate{return RecordPositionSerialized(c);});}
        RuleResult RecordPositionSerialized(CommandContext c)
        {lock(lane){Actor a;if(active||pending||sessions==null||c.Tick!=tick||!actors.TryGetValue(c.ActorId,out a)||!Bound(a))return RuleResult.Reject("session/root not bound");return sessions.RecordPosition(c,a.Root.Position);}}
    }
}


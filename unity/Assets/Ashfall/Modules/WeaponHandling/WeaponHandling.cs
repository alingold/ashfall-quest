using System;
using System.Collections.Generic;
using Ashfall.Core;

namespace Ashfall.Modules.WeaponHandling
{
    // Bounded service-rifle/precision-rifle candidate. This owns mechanical state,
    // not damage, inventory definitions, tracked transforms, or collision authority.
    public sealed partial class WeaponHandling
    {
        readonly string session;
        string actor;
        long epoch;
        readonly IAuthorityGate authority;
        readonly IAmmoReserve reserve;
        readonly WeaponMechanism mechanism;
        readonly int capacity;
        readonly float interval;
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        Queue<string> order = new Queue<string>();
        long tick = -1, sequence = -1, shots;
        ReloadPhase phase = ReloadPhase.Ready;
        int loaded, seated, pattern;
        bool chambered, spent, pending, supported, latched, previousGrip;
        bool contact, releaseGrip, releaseTrigger;
        Float3 grab, boltGrab;
        float overrun;
        long lastShotTick = -1;
        bool retired;
        float boltTravel, cooldown, recoil, pitch, yaw, idle;
        bool gripToggle;
        public bool GripToggle { get { return gripToggle; } set { if(gripToggle!=value){gripToggle=value;revision++;} } }
        static readonly float[] Vertical = { 1f, .28f, .42f, .31f, .55f, .37f };
        static readonly float[] PrecisionVertical = { 1f, .90f, .70f, .85f, 1.10f };
        static readonly float[] Yaw = { 0f, -.12f, .18f, .10f, -.20f, -.08f };
        public WeaponHandling(string sessionId, string actorId, long authorityEpoch,
            WeaponMechanism mechanism, IAmmoReserve reserve, IAuthorityGate authority)
        {
            if (String.IsNullOrEmpty(sessionId) || String.IsNullOrEmpty(actorId) || authorityEpoch < 0 || reserve == null || authority == null || !Enum.IsDefined(typeof(WeaponMechanism),mechanism))
                throw new ArgumentException("Identity, reserve and authority are required");
            session = sessionId; actor = actorId; epoch = authorityEpoch;
            this.mechanism = mechanism; this.reserve = reserve; this.authority = authority;
            capacity = MagazineCapacity(mechanism);
            interval = mechanism == WeaponMechanism.MagnumRevolver ? 1f / 3f : mechanism == WeaponMechanism.PrecisionRifle ? .95f : .115f;
            phase = mechanism == WeaponMechanism.MagnumRevolver ? ReloadPhase.CylinderOpen : ReloadPhase.NeedsMagazine;
        }
        // Construction never grants ammunition. Only the session lifetime ledger may
        // authorize a new stable instance's initial magazine.
        internal void InitializeMagazine(int rounds)
        { loaded = rounds; chambered = rounds > 0; phase = rounds > 0 ? ReloadPhase.Ready : mechanism == WeaponMechanism.MagnumRevolver ? ReloadPhase.CylinderOpen : ReloadPhase.NeedsMagazine; }
        internal RuleResult AuthorizeCapture(CommandContext c)
        {
            if (retired || c.Tick < tick || seen.Contains(c.CommandId)) return RuleResult.Reject("stale capture");
            RuleResult r = authority.Validate(c); if (!r.Accepted) return r;
            RecordEnvelope(c);
            return RuleResult.Accept();
        }
        internal void RecordEnvelope(CommandContext c)
        {
            revision++; tick = c.Tick; seen.Add(c.CommandId); order.Enqueue(c.CommandId);
            if (order.Count > 256) seen.Remove(order.Dequeue());
        }
        internal RuleResult AuthorizeRestore(CommandContext c)
        {
            if (c.Tick < tick || seen.Contains(c.CommandId)) return RuleResult.Reject("stale restore");
            return authority.Validate(c);
        }
        internal WeaponHandling Freeze()
        {
            if (retired) throw new InvalidOperationException("Lifetime already transferred");
            WeaponHandling copy = (WeaponHandling)MemberwiseClone();
            copy.flick = flick.Copy(); copy.seen = new HashSet<string>(seen, StringComparer.Ordinal);
            copy.order = new Queue<string>(order);
            copy.Suspend(); copy.overrun = 0;
            retired = true; revision++;
            return copy;
        }
        internal WeaponHandling Resume(CommandContext context)
        {
            WeaponHandling copy = (WeaponHandling)MemberwiseClone();
            copy.flick = flick.Copy(); copy.seen = new HashSet<string>(seen, StringComparer.Ordinal);
            copy.order = new Queue<string>(order);
            copy.actor = context.ActorId; copy.epoch = context.AuthorityEpoch;
            copy.tick = context.Tick; copy.seen.Add(context.CommandId); copy.order.Enqueue(context.CommandId);
            if (copy.order.Count > 256) copy.seen.Remove(copy.order.Dequeue()); copy.retired = false;
            copy.Suspend(); copy.overrun = 0;
            return copy;
        }
        public const string ApiVersion = "WeaponHandling-r3/1";
        public HandlingSnapshot Snapshot
        {
            get { if(sourceMechanics!=null) return new HandlingSnapshot { Loaded=sourceMechanics.Loaded, Shots=shots, Chambered=sourceMechanics.Loaded>0, Phase=sourceMechanics.Loaded>0?ReloadPhase.Ready:sourceMechanics.Held?ReloadPhase.HeldMagazine:ReloadPhase.NeedsMagazine, TriggerReleaseRequired=sourceMechanics.Release, ClaimsSupport=sourceMechanics.Held, CooldownSeconds=(float)Math.Max(0,sourceMechanics.ReadyAt-sourceMechanics.Seconds) }; return new HandlingSnapshot { Phase = phase, Loaded = loaded, Seated = seated,
                Chambered = chambered, SpentChamber = spent, Supported = supported,
                ClaimsSupport = supported || contact || phase == ReloadPhase.HeldMagazine,
                TriggerReleaseRequired = releaseTrigger, ReloadPending = pending, CooldownSeconds = cooldown, RecoilPattern = pattern, IdleSeconds = idle, BoltTravel = boltTravel,
                RecoilDistance = recoil * 1.25f, RecoilPitch = pitch * 1.25f,
                RecoilYaw = yaw * 1.25f, Shots = shots }; }
        }
        public RuleResult Apply(CommandContext c, HandlingAction action, HandlingInput input)
        {
            if(sourceMechanics!=null)return action==HandlingAction.ReloadStep&&input.Desktop?ReloadSourceDesktop(c,input,sourceMechanics.Seconds+input.DeltaSeconds):RuleResult.Reject("use source explosive mechanics API");
            if (retired) return RuleResult.Reject("lifetime transferred");
            if (!c.IsAuthority || c.SessionId != session || c.ActorId != actor || c.AuthorityEpoch != epoch)
                return RuleResult.Reject("authority identity");
            if (String.IsNullOrEmpty(c.CommandId) || c.CommandId.Length > 128 || c.Tick < 0 || c.Tick < tick || input.Sequence <= sequence || seen.Contains(c.CommandId))
                return RuleResult.Reject("replay or stale input");
            if (!Enum.IsDefined(typeof(HandlingAction), action) || !input.SupportLocal.IsFinite || !Finite(input.ForegripDistance) || input.ForegripDistance < 0 ||
                !Finite(input.DeltaSeconds) || input.DeltaSeconds < 0 || input.DeltaSeconds > .25f)
                return RuleResult.Reject("invalid sample");
            if (c.Tick == tick && input.DeltaSeconds > 0) return RuleResult.Reject("time already advanced for tick");
            RuleResult allowed = authority.Validate(c);
            if (!allowed.Accepted) return allowed;
            // Consume validated commands even when the requested operation is inapplicable.
            bool newTick = c.Tick != tick;
            revision++; tick = c.Tick; sequence = input.Sequence; seen.Add(c.CommandId); order.Enqueue(c.CommandId);
            if (order.Count > 256) seen.Remove(order.Dequeue());
            // Pause supplies no simulation time. Unavailable hands still recover mechanics,
            // but cannot accumulate cadence credit or retain transient contacts.
            if (newTick && !input.Paused) Recover(input.DeltaSeconds);
            if (!input.Enabled || input.Paused || !input.PrimaryTracked || input.PrimaryClimbing || input.HeadObstructed)
            { Suspend(); overrun = 0; return RuleResult.Reject("input unavailable"); }
            if (!input.Trigger) overrun = 0;
            if (!input.Trigger) releaseTrigger = false;
            if (action == HandlingAction.Sample) PrimaryFlick(input);
            bool supportFree = input.SupportTracked && !input.SupportReserved;
            if (!supportFree) SuspendSupport();
            else
            {
                if (releaseGrip && !input.Grip) releaseGrip = false;
                UpdateSupport(input);
                if (action == HandlingAction.Sample) Gesture(input);
            }
            RuleResult result = RuleResult.Accept();
            if (action == HandlingAction.Fire) result = Fire(input);
            else if (action == HandlingAction.Eject || action == HandlingAction.ReloadStep)
            {
                // Source primary opening precedes the offhand mechanism availability gate.
                if (mechanism == WeaponMechanism.MagnumRevolver && phase == ReloadPhase.Ready &&
                    (action == HandlingAction.Eject || input.Desktop)) result = OpenCylinder();
                else if (!supportFree || releaseGrip) result = RuleResult.Reject("support unavailable");
                else if (action == HandlingAction.Eject) result = Eject();
                else if (!input.Desktop) result = RuleResult.Reject("physical gesture required");
                else result = DesktopStep();
            }
            if (phase != ReloadPhase.Ready) { supported = false; latched = false; }
            previousGrip = supportFree && input.Grip;
            return result;
        }
        RuleResult Fire(HandlingInput i)
        {
            if (lastShotTick == tick || !i.Trigger || releaseTrigger || cooldown > .000001f || phase != ReloadPhase.Ready || !chambered || loaded <= 0)
                return RuleResult.Reject("not fireable");
            loaded--; shots++; lastShotTick = tick;
            cooldown = mechanism == WeaponMechanism.ServiceRifle ? Math.Max(.001f, interval - overrun) : interval; overrun = 0;
            float reduction = supported ? .38f : 1f;
            float angle = (float)((mechanism == WeaponMechanism.MagnumRevolver ? 7 * 1.1 : 5.5) * Math.PI / 180);
            float[] vertical = mechanism == WeaponMechanism.PrecisionRifle ? PrecisionVertical : Vertical;
            recoil = Math.Min(.13f, recoil + (mechanism == WeaponMechanism.MagnumRevolver ? .065f : .052f) * reduction);
            pitch = Math.Min((float)Math.PI / 4, pitch + angle * reduction * vertical[pattern % vertical.Length]);
            yaw = Math.Max((float)(-80 * Math.PI / 180), Math.Min((float)(80 * Math.PI / 180), yaw + angle * reduction * Yaw[pattern % Yaw.Length]));
            pattern = (pattern + 1) % 30; idle = 0;
            if (mechanism == WeaponMechanism.MagnumRevolver)
            { releaseTrigger = true; if (loaded == 0) Eject(); }
            else if (mechanism == WeaponMechanism.PrecisionRifle)
            {
                chambered = false; spent = true; pending = loaded == 0;
                phase = ReloadPhase.NeedsBolt; contact = false; releaseGrip = true; releaseTrigger = true;
            }
            else if (loaded == 0) Eject();
            return RuleResult.Accept();
        }
        RuleResult Eject()
        {
            if (mechanism == WeaponMechanism.MagnumRevolver) return OpenCylinder();
            if (phase != ReloadPhase.Ready && phase != ReloadPhase.NeedsRack && phase != ReloadPhase.NeedsBolt)
                return RuleResult.Reject("no magazine to eject");
            if (mechanism == WeaponMechanism.PrecisionRifle)
            { if (contact) return RuleResult.Reject("bolt in hand"); pending = true; chambered = false; SetStage(ReloadPhase.NeedsBolt); releaseTrigger = true; return RuleResult.Accept(); }
            return ReturnMagazine();
        }
        RuleResult ReturnMagazine()
        {
            int rounds = phase == ReloadPhase.NeedsRack ? seated : loaded;
            int returned = reserve.Refund(rounds);
            if (returned < 0 || returned > rounds) throw new InvalidOperationException("Reserve violated refund contract");
            if (phase == ReloadPhase.NeedsRack) seated -= returned; else loaded -= returned;
            if (returned != rounds) return RuleResult.Reject("reserve cannot accept loaded rounds");
            loaded = seated = 0; chambered = spent = pending = false;
            SetStage(ReloadPhase.NeedsMagazine); releaseTrigger = true; contact = false; boltTravel = 0;
            return RuleResult.Accept();
        }
        void Grab(Float3 point)
        { if (phase == ReloadPhase.NeedsMagazine && reserve.Available != 0) { grab = point; SetStage(ReloadPhase.HeldMagazine); } }
        void Insert()
        {
            int taken = reserve.Take(capacity);
            if (taken < 0 || taken > capacity) throw new InvalidOperationException("Reserve violated take contract");
            if (taken == 0) { SetStage(ReloadPhase.NeedsMagazine); return; }
            chambered = spent = pending = false; contact = false; boltTravel = 0;
            if (mechanism == WeaponMechanism.PrecisionRifle) { loaded = taken; SetStage(ReloadPhase.NeedsBolt); }
            else { seated = taken; SetStage(ReloadPhase.NeedsRack); }
        }
        RuleResult Cycle()
        {
            contact = false; boltTravel = 0; releaseTrigger = true;
            if (phase == ReloadPhase.NeedsRack) { loaded = seated; seated = 0; }
            else if (pending || loaded == 0) return ReturnMagazine();
            spent = false; chambered = loaded > 0; SetStage(ReloadPhase.Ready);
            return RuleResult.Accept();
        }
        RuleResult DesktopStep()
        {
            if (mechanism == WeaponMechanism.MagnumRevolver) return RevolverStep();
            switch (phase)
            {
                case ReloadPhase.Ready:
                    RuleResult r = Eject();
                    return r.Accepted && mechanism == WeaponMechanism.PrecisionRifle ? Cycle() : r;
                case ReloadPhase.NeedsMagazine: Grab(new Float3(0, -.278f, .15f)); if (phase == ReloadPhase.HeldMagazine) Insert(); break;
                case ReloadPhase.HeldMagazine: Insert(); break;
                default: return Cycle();
            }
            return RuleResult.Accept();
        }
        void Gesture(HandlingInput i)
        {
            if (mechanism == WeaponMechanism.MagnumRevolver) { RevolverGesture(i); return; }
            if (releaseGrip) return;
            bool fresh = i.Grip && !previousGrip;
            float radius = mechanism == WeaponMechanism.PrecisionRifle ? .25f : .10f;
            if (phase == ReloadPhase.NeedsMagazine && fresh && Reach(i.SupportLocal, new Float3(0, -.278f, .15f), radius)) Grab(i.SupportLocal);
            else if (phase == ReloadPhase.HeldMagazine)
            {
                if (!i.Grip) SetStage(ReloadPhase.NeedsMagazine);
                else if (i.SupportLocal.Y - grab.Y >= .06f && Reach(i.SupportLocal, new Float3(0, -.18f, .15f), radius)) Insert();
            }
            else if (phase == ReloadPhase.NeedsRack || phase == ReloadPhase.NeedsBolt)
            {
                Float3 center = phase == ReloadPhase.NeedsBolt ? new Float3(.072f, .045f, .07f) : new Float3(-.045f, .064f, .10f);
                bool reach = phase == ReloadPhase.NeedsBolt ? BoltReach(i.SupportLocal, center) : Reach(i.SupportLocal, center, .18f);
                if (fresh && !contact && reach) { contact = true; boltGrab = i.SupportLocal; }
                if (!i.Grip) { contact = false; boltTravel = 0; }
                if (contact && i.Grip)
                {
                    float pull = boltGrab.Z - i.SupportLocal.Z; // reflected Godot +Z rearward
                    boltTravel = Math.Max(0, Math.Min(pull, phase == ReloadPhase.NeedsBolt ? .115f : .09f));
                    if (pull >= (phase == ReloadPhase.NeedsBolt ? .115f : .075f)) Cycle();
                }
            }
        }
        void UpdateSupport(HandlingInput i)
        {
            bool near = i.ForegripDistance < (supported ? .45f : .25f);
            if (!near || phase != ReloadPhase.Ready || releaseGrip) latched = false;
            else if (GripToggle && i.Grip && !previousGrip) latched = !latched;
            supported = near && phase == ReloadPhase.Ready && !releaseGrip && (GripToggle ? latched : i.Grip);
        }
        void SuspendSupport()
        { supported = latched = contact = false; boltTravel = 0; previousGrip = true; releaseGrip = true;
          if (phase == ReloadPhase.HeldMagazine) phase = ReloadPhase.NeedsMagazine; }
        void Suspend() { SuspendSource(); SuspendSupport(); releaseTrigger = true; flick.Reset(); }
        void SetStage(ReloadPhase next)
        {
            phase = next; overrun = 0;
            cooldown = mechanism == WeaponMechanism.PrecisionRifle ? Math.Max(cooldown, .12f) : .12f;
        }
        void Recover(float dt)
        {
            overrun = cooldown > 0 ? Math.Max(0, Math.Min(interval, dt - cooldown)) : 0;
            cooldown = Math.Max(0, cooldown - dt); idle += dt;
            float decay = (float)Math.Exp(-dt * 9); recoil *= decay;
            if (idle > Math.Max(.18f, Math.Min(interval * 1.15f, .5f))) { pitch *= decay; yaw *= decay; }
            if (idle >= .8f) { pattern = 0; recoil = pitch = yaw = 0; }
        }
        static bool Finite(float f) { return !float.IsNaN(f) && !float.IsInfinity(f); }
        public static bool Reach(Float3 p, Float3 center, float radius)
        {
            if (!p.IsFinite || !center.IsFinite || !Finite(radius) || radius <= 0) return false;
            float x = p.X - center.X, y = p.Y - center.Y, z = p.Z - center.Z;
            if (x*x + y*y + z*z > .34f*.34f) return false;
            x = Math.Max(0, Math.Abs(x) - .035f); if (z > 0) z = Math.Max(0, z - .04f);
            float r = Math.Min(radius + .055f, .30f);
            return x*x + y*y + z*z < r*r;
        }
        static bool BoltReach(Float3 p, Float3 c)
        {
            float x = (p.X-c.X)/.24f, y = (p.Y-c.Y)/.20f, z = (p.Z-c.Z)/(p.Z>c.Z ? .30f : .22f);
            return x*x+y*y+z*z <= 1;
        }
    }
}


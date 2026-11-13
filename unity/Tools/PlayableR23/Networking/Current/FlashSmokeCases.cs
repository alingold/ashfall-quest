using System;
using System.IO;
using Ashfall.Core;
using Ashfall.Integration.Playable.Networking;
using Ashfall.Integration.Playable.Match;
using Ashfall.Integration.Playable.Explosives;
using Ashfall.Integration.Playable.FlashSmoke;

// flashsmoke CPU tests (features/flashsmoke.md). Registered as D46-D49 in RegressionCases.cs.
partial class LobbyCases
{
    static bool Near(float a, float b, float tolerance) { return Math.Abs(a - b) <= tolerance; }

    // D46: distance/facing/occlusion table, monotonicity and thresholds.
    static void FlashSmokeExposure()
    {
        Need(R25FlashExposure.DistanceFactor(0) == 1, "Flash at the eye has full distance factor");
        Need(Near(R25FlashExposure.DistanceFactor(11), 1 - (float)Math.Pow(.5, 1.5), .0001f), "kd(11 m) = 1 - 0.5^1.5");
        Need(R25FlashExposure.DistanceFactor(22) == 0 && R25FlashExposure.DistanceFactor(30) == 0, "kd reaches 0 at the 22 m radius");
        Need(R25FlashExposure.DistanceFactor(float.NaN) == 0 && R25FlashExposure.DistanceFactor(-1) == 0, "Invalid distance never blinds");
        Need(R25FlashExposure.FacingFactor(1) == 1 && R25FlashExposure.FacingFactor(.5f) == 1, "Looking at the flash: kf = 1");
        Need(R25FlashExposure.FacingFactor(-.2f) == .35f && R25FlashExposure.FacingFactor(-1) == .35f, "Facing away: kf = 0.35");
        Need(Near(R25FlashExposure.FacingFactor(.15f), .675f, .0001f), "kf is linear between -0.2 and 0.5");
        Need(R25FlashExposure.OcclusionFactor(true, 20) == 1, "Clear line: kl = 1");
        Need(R25FlashExposure.OcclusionFactor(false, 5.9f) == .15f && R25FlashExposure.OcclusionFactor(false, 6) == 0, "Blocked: bounce light 0.15 only inside 6 m");
        float last = 2;
        for (float d = 0; d <= 23; d += .5f)
        {
            float e = R25FlashExposure.Exposure(d, 1, true);
            Need(e <= last + .00001f, "Exposure never rises with distance (" + d + " m)");
            last = e;
        }
        float lastFacing = -1;
        for (float c = -1; c <= 1.0001f; c += .05f)
        {
            float e = R25FlashExposure.Exposure(4, c, true);
            Need(e + .00001f >= lastFacing, "Exposure never falls as the view turns toward the flash");
            lastFacing = e;
        }
        Need(R25FlashExposure.Exposure(21.5f, 1, true) == 0, "Exposures below 0.08 are ignored");
        Need(R25FlashExposure.Exposure(3, -1, false) == 0, "Facing away behind cover: 0.35*0.15*kd < threshold");
        Need(R25FlashExposure.Exposure(1, 1, false) >= R25FlashExposure.Threshold, "Blocked flash at 1 m still bounces light");
        Need(R25FlashExposure.Exposure(8, 1, false) == 0, "Blocked flash beyond 6 m gives nothing");
        Need(Near(R25FlashExposure.Exposure(new Float3(0, 1.6f, 0), new Float3(0, 0, 2), new Float3(0, 1.6f, 5), true), R25FlashExposure.DistanceFactor(5), .0001f), "Vector form: forward length is irrelevant, cos = 1");
        Need(R25FlashExposure.Exposure(new Float3(0, 1.6f, 0), new Float3(0, 0, 1), new Float3(float.NaN, 0, 0), true) == 0, "Non-finite detonation is ignored");
    }

    // D47: blindness timeline: 80 ms ramp, hold 0.35 T, ease out, total T.
    static void FlashSmokeBlindness()
    {
        Need(Near(R25FlashBlindness.Duration(0), .6f, .0001f) && Near(R25FlashBlindness.Duration(1), 4f, .0001f), "T = 0.6 + 3.4 E");
        Need(R25FlashBlindness.Envelope(.04f, 1) > .49f && R25FlashBlindness.Envelope(.04f, 1) < .51f, "Envelope ramps linearly over 80 ms");
        Need(R25FlashBlindness.Envelope(.08f + .35f * 4 - .01f, 1) == 1, "Envelope holds for 0.35 T");
        Need(R25FlashBlindness.Envelope(4, 1) == 0 && R25FlashBlindness.Envelope(-1, 1) == 0, "Envelope is zero outside [0, T)");
        Need(R25FlashBlindness.Peak(1) == 1 && R25FlashBlindness.Peak(.08f) < .6f, "Strong flashes white out, weak ones veil");
        foreach (float exposure in new[] { .08f, .3f, .7f, 1f })
        {
            var b = new R25FlashBlindness();
            Need(b.Trigger(exposure), "Exposure above the threshold triggers");
            float t = 0, timeline = 0, previous = 0, peak = 0, lastVisible = 0;
            // Mixed frame times, including hitches above the 0.1 s clamp.
            float[] steps = { .011f, .013f, .0167f, .2f, .009f, .05f, .0139f };
            for (int i = 0; t < 8; i++)
            {
                float dt = steps[i % steps.Length];
                b.Step(dt);
                t += dt;
                float clamped = Math.Min(dt, R25FlashBlindness.MaxStep);
                timeline += clamped;
                Need(b.Alpha - previous <= clamped / R25FlashBlindness.Ramp + .0001f, "No rise faster than the 80 ms ramp (E=" + exposure + ")");
                Need(previous - b.Alpha <= clamped / R25FlashBlindness.ReleaseSeconds + .0001f, "No fall faster than the release limit");
                if (b.Alpha > 0) lastVisible = timeline;
                peak = Math.Max(peak, b.Alpha);
                previous = b.Alpha;
            }
            Need(Near(peak, R25FlashBlindness.Peak(exposure), .02f), "Timeline reaches its peak (E=" + exposure + ")");
            float total = R25FlashBlindness.Duration(exposure);
            Need(lastVisible <= total + R25FlashBlindness.ReleaseSeconds + .25f && lastVisible >= total * .6f, "Total duration bounded by T (E=" + exposure + ")");
            Need(!b.Active && b.DuckDb == 0 && Near(b.DuckGain, 1, .00001f), "Audio is fully restored after T");
        }
        var duck = new R25FlashBlindness();
        duck.Trigger(1); duck.Step(.05f);
        Need(Near(duck.DuckDb, 18, .001f) && duck.DuckGain < .13f && duck.DuckGain > .12f, "Full exposure ducks world audio by 18 dB");
        var half = new R25FlashBlindness();
        half.Trigger(.5f); half.Step(.05f);
        Need(Near(half.DuckDb, 9, .001f), "Duck depth scales with E");
        var weak = new R25FlashBlindness();
        Need(!weak.Trigger(.07f) && !weak.Trigger(float.NaN), "Below-threshold or invalid exposure is ignored");
        // Repeated flashes never make the fade dip (no strobe), and are bounded.
        var repeat = new R25FlashBlindness();
        repeat.Trigger(.9f);
        for (int i = 0; i < 20; i++) repeat.Step(.02f);
        float held = repeat.Alpha;
        for (int i = 0; i < 12; i++)
        {
            repeat.Trigger(.2f);
            repeat.Step(.02f);
            Need(repeat.Alpha >= held - .0001f, "A weaker overlapping flash never lowers the fade");
            Need(repeat.Events <= R25FlashBlindness.Capacity, "Overlapping flashes are bounded");
        }
        repeat.Release();
        float before = repeat.Alpha;
        repeat.Step(.05f);
        Need(repeat.Alpha < before && repeat.Alpha > 0 && repeat.DuckDb == 0, "Release eases the view back and restores audio at once");
        repeat.Reset();
        Need(repeat.Alpha == 0 && !repeat.Active, "Reset clears everything");
        var nan = new R25FlashBlindness();
        nan.Trigger(1); nan.Step(float.NaN); nan.Step(-1); nan.Step(float.PositiveInfinity);
        Need(nan.Alpha == 0 && nan.Active, "Invalid steps do not advance the timeline");
    }

    // D48: smoke cloud radius vs time, expiry, clock branches and loot share.
    static void FlashSmokeCloud()
    {
        Need(R25SmokeCloud.EmissionStart(-1) == 2 && Near(R25SmokeCloud.EmissionStart(.3f), 1.5f, .0001f) && R25SmokeCloud.EmissionStart(1.5f) == 2, "Emission at min(impact + 1.2 s, launch + 2.0 s)");
        Need(R25SmokeCloud.Radius(0) == 0 && R25SmokeCloud.Radius(2.5f) == 5.5f && R25SmokeCloud.Radius(10) == 5.5f, "Radius grows 0 -> 5.5 m over 2.5 s then holds");
        float lastRadius = 0;
        for (float t = .05f; t < 16; t += .05f)
        {
            float r = R25SmokeCloud.Radius(t);
            Need(r >= lastRadius - .00001f && r <= 5.5f && r < 15, "Radius is monotone and within the 15 m row limit");
            lastRadius = r;
        }
        Need(R25SmokeCloud.Density(12) == 1 && Near(R25SmokeCloud.Density(14.5f), .5f, .0001f) && R25SmokeCloud.Density(16) == 0, "Thins over the last 3 s of a 16 s life");
        Need(R25SmokeCloud.Expired(16) && !R25SmokeCloud.Expired(15.9f), "Cloud expires at 16 s");
        Need(R25SmokeCloud.LaunchDelay + R25SmokeCloud.Life < 20, "Launch age stays under the 20 s client row limit");
        var canister = new Float3(3, 0, 4);
        Need(R25SmokeCloud.ViewAmount(new Float3(3, 2.2f, 4), canister, 5) > .99f, "Camera at the cloud centre is fully greyed");
        Need(R25SmokeCloud.ViewAmount(new Float3(3, 2.2f, 14), canister, 5) == 0, "Camera outside is unaffected");
        Need(R25SmokeCloud.ViewAmount(new Float3(3, 2.2f, 4), canister, 15) < .5f, "Grey-out thins with the cloud");
        Need(R25SmokeCloud.SegmentObscured(new Float3(-10, 2, 4), new Float3(16, 2, 4), canister, 5) && !R25SmokeCloud.SegmentObscured(new Float3(-10, 2, 20), new Float3(16, 2, 20), canister, 5), "Segment test sees through only clear air");
        uint seed = R25SmokeCloud.Seed("projectile/7");
        Need(seed == R25SmokeCloud.Seed("projectile/7") && seed != R25SmokeCloud.Seed("projectile/8"), "Cloud seed is stable per projectile id");
        for (int i = 0; i < R25SmokeCloud.PuffCount; i++)
        {
            Float3 a, b; float sa, sb, aa, ab;
            R25SmokeCloud.Puff(seed, i, out a, out sa, out aa);
            R25SmokeCloud.Puff(seed, i, out b, out sb, out ab);
            Need(a.X == b.X && a.Y == b.Y && a.Z == b.Z && sa == sb && aa == ab, "Puff layout is deterministic");
            Need(a.IsFinite && Math.Abs(a.X) <= 1 && Math.Abs(a.Y) <= 1 && Math.Abs(a.Z) <= 1 && sa > .2f && sa < .7f && aa > .5f && aa <= 1, "Puffs stay inside the unit cloud");
        }

        // Clock: flash never inherits zone ticks; smoke emits on schedule with no damage.
        var flash = new R23GrenadeClock("f", "flash");
        int blasts = 0, warnings = 0; bool detonated = false; float detonatedAt = 0;
        Action<R23GrenadeBlast> count = delegate(R23GrenadeBlast blast) { blasts++; };
        Need(!flash.Contact(), "Flash does not stick like a zone grenade");
        for (int i = 0; i < 200 && !flash.Ended; i++)
        {
            var frame = flash.Advance(.02f, true, new Float3(), count);
            if (frame.Warning) warnings++;
            if (frame.Detonated) { detonated = true; detonatedAt = flash.Age; }
            Need(frame.DamageTicks == 0 && !flash.Active && flash.Radius == 0, "Flash has no zone volume or damage ticks");
        }
        Need(detonated && Near(detonatedAt, 1.6f, .021f) && blasts == 0 && warnings == 0, "Flash pops once at 1.6 s with no blast callback or zone warnings");
        var smoke = new R23GrenadeClock("s", "smoke");
        bool activated = false; float lastAge = 0;
        for (int i = 0; i < 1200 && !smoke.Ended; i++)
        {
            if (i == 15) Need(smoke.Contact(), "Smoke sticks on first contact");
            var frame = smoke.Advance(.02f, true, new Float3(), count);
            if (frame.Activated) { activated = true; Need(Near(smoke.EmitAt, .30f + 1.2f, .021f), "Emission 1.2 s after impact"); }
            Need(frame.DamageTicks == 0 && !frame.Detonated && smoke.Radius <= 5.5f, "Smoke never ticks damage or detonates");
            lastAge = smoke.Age;
        }
        Need(activated && smoke.Ended && blasts == 0 && Near(lastAge, smoke.EmitAt + 16, .03f), "Smoke ends 16 s after emission with no blasts");
        var unstuck = new R23GrenadeClock("u", "smoke");
        for (int i = 0; i < 120; i++) unstuck.Advance(.02f, true, new Float3(), count);
        Need(unstuck.Active && unstuck.EmitAt == 2 && Near(unstuck.CloudAge, .4f, .03f), "A smoke that never lands still emits at 2.0 s");
        var zone = new R23GrenadeClock("z", "zone");
        zone.Contact();
        int zoneBlasts = 0;
        for (int i = 0; i < 700 && !zone.Ended; i++) zone.Advance(.02f, true, new Float3(), delegate(R23GrenadeBlast blast) { zoneBlasts++; });
        Need(zoneBlasts == 19 && zone.EmitAt == -1, "Zone grenade behaviour is unchanged (19 nonzero blast callbacks; final tick has zero radius)");

        // Kinds, labels and loot share.
        Need(R23GrenadeState.ValidKind("flash") && R23GrenadeState.ValidKind("smoke") && !R23GrenadeState.ValidKind("flashbang"), "Kind ids are the 5-byte strings");
        Need(R23GrenadeState.KindLabel("flash") == "FLASHBANG" && R23GrenadeState.KindLabel("smoke") == "SMOKE GRENADE" && R23GrenadeState.KindLabel("zone") == "ZONE GRENADE", "Labels per kind");
        var state = new R23GrenadeState();
        Need(state.Register("a", "flash") && state.Register("b", "flash") && state.Pickup("a", 1, true) && state.Pickup("b", 1, true), "Flashbangs can be carried");
        R23GrenadeItemState ia, ib;
        state.TryRead("a", out ia); state.TryRead("b", out ib);
        Need(ia.Slot != ib.Slot, "New kinds do not use the frag two-per-slot stack");
        int flashes = 0, smokes = 0, kept = 0, draws = 0;
        for (int site = 0; site < 400; site++)
            for (int slot = 0; slot < 6; slot++)
            {
                string kind = R25Throwables.LootKind(slot % 2 == 0 ? "frag" : "zone", "valley", site, slot, null);
                Need(kind == R25Throwables.LootKind(slot % 2 == 0 ? "frag" : "zone", "valley", site, slot, null), "Loot share is deterministic");
                draws++;
                if (kind == "flash") flashes++; else if (kind == "smoke") smokes++; else kept++;
            }
        Need(flashes > draws / 6 && flashes < draws / 3 && smokes > draws / 6 && smokes < draws / 3 && kept > draws / 3, "About a quarter each become flash and smoke");
        Func<string, bool> noFlash = delegate(string kind) { return kind != "flash"; };
        for (int site = 0; site < 200; site++)
            Need(R25Throwables.LootKind("frag", "range", site, 0, noFlash) != "flash", "Disabled flashbangs never spawn as loot");
        Need(R25Throwables.LootKind("smaw", "range", 1, 1, null) == "smaw", "Only grenade draws are shared");
        Need(R25Throwables.Allowed("flash", false, true) == false && R25Throwables.Allowed("smoke", true, false) == false && R25Throwables.Allowed("frag", false, false), "Switches gate only their own kind");

        // Match rules: defaults, codec round trip.
        var defaults = R24PlayableMatchRules.ForPreset("team_revive", "range");
        Need(defaults.Flashbangs && defaults.SmokeGrenades, "Both kill switches default ON");
        var off = defaults.With("flashbangs", false).With("smoke_grenades", false);
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) R24LanRoundCodec.Write(writer, off);
            stream.Position = 0;
            var read = R24LanRoundCodec.Read(new BinaryReader(stream), "range");
            Need(!read.Flashbangs && !read.SmokeGrenades && read.ZoneEnabled == off.ZoneEnabled, "Kill switches replicate through the rules codec");
        }
        Need(Object.Equals(defaults.With("flashbangs", 1).ReadSourceValue("flashbangs"), true), "Strict bool flashbangs");
        Need(defaults.Pretty("smoke_grenades") == "ON", "Rule row shows ON/OFF");
    }

    // D49: host authority end to end on the production lobby and test transport.
    static void FlashSmokeHostAuthority()
    {
        foreach (string kind in new[] { "flash", "smoke" })
            using (var pair = new Pair())
            {
                for (int i = 0; i < 5; i++) pair.Tick();
                pair.Host.BindProjectileWorld(new ProjectileWorld { Contact = true });
                string hash = new string('b', 64);
                StartRound(pair, new HazardWorld(), R24PlayableMatchRules.ForPreset("free_for_all", "range").With("zone_enabled", false).With("protection", 0).With("starting_shield", 0), hash);
                for (int i = 0; i < 4; i++) KeepRaw(pair, hash, 0, 8);
                pair.Client.RequestLoot("cx4", 1, true, new Float3(0, 1.2f, .2f));
                for (int i = 0; i < 4; i++) KeepRaw(pair, hash, 0, 8);
                string id = "shared/" + kind;
                Need(pair.Host.RegisterSharedGrenade(id, kind, new Float3(0, 1.2f, .2f)), "Host registers one " + kind + " payload");
                Need(!pair.Client.RegisterSharedGrenade("forged", kind, new Float3()), "Guest cannot mint a " + kind);
                pair.Client.RequestGrenade(id, 1, 0, new Float3());
                for (int i = 0; i < 8; i++) KeepRaw(pair, hash, 0, 8);
                R24LanGrenadeState item;
                Need(pair.Host.TryReadGrenade(id, out item) && item.Owner == pair.Client.OwnActor, "Guest picks up a " + kind + " through host custody");
                Need(pair.Client.TryReadGrenade(id, out item) && item.Kind == kind, "Kind string round-trips on the grenade row: " + kind);
                R24LanHealthState hostBefore, clientBefore, hostAfter, clientAfter;
                pair.Host.TryReadHealth(pair.Host.OwnActor, out hostBefore);
                pair.Host.TryReadHealth(pair.Client.OwnActor, out clientBefore);
                pair.Client.RequestGrenade(id, 1, 1, new Float3());
                for (int i = 0; i < 4; i++) KeepRaw(pair, hash, 0, 8);
                Need(pair.Host.TryReadGrenade(id, out item) && item.Spent, "Throw consumes the " + kind + " once");
                Need(pair.Host.SharedProjectiles.Length == 1 && pair.Host.SharedProjectiles[0].Kind == kind, "Host simulates one " + kind + " projectile");
                pair.Client.RequestGrenade(id, 1, 1, new Float3());
                bool sawLive = false, sawEnded = false, sawActive = false, emitValid = true;
                float maxRadius = 0, emitAt = -1;
                Float3 smokeStart=new Float3();bool hasSmokeStart=false;
                for (int i = 0; i < 420 && !sawEnded; i++)
                {
                    KeepRaw(pair, hash, 0, 8);
                    Need(pair.Host.SharedProjectiles.Length <= 1, "Duplicate throw cannot create another " + kind);
                    foreach (var row in pair.Client.SharedProjectiles)
                    {
                        Need(row.Kind == kind, "Guest projectile row keeps its kind: " + kind);
                        if (!row.Ended) sawLive = true;
                        if(kind=="smoke"&&row.Active){if(!hasSmokeStart){smokeStart=row.Position;hasSmokeStart=true;}Need(row.Position.X==smokeStart.X&&row.Position.Y==smokeStart.Y&&row.Position.Z==smokeStart.Z,"Emission position remains fixed");}
                        if (row.Active) { sawActive = true; maxRadius = Math.Max(maxRadius, row.Radius); emitAt = row.EmitAt; }
                        if (kind == "smoke" && row.Active && !(row.EmitAt >= 0 && row.EmitAt <= row.Age)) emitValid = false;
                        if (kind == "flash" && row.EmitAt != -1) emitValid = false;
                        if (row.Ended) { sawEnded = true; pair.Client.AcknowledgeProjectilePresentation(row); }
                    }
                }
                Need(sawLive && sawEnded && emitValid, kind + " flies, ends and replicates to the guest; live="+sawLive+" ended="+sawEnded+" emitValid="+emitValid+" host="+(pair.Host.SharedProjectiles.Length==0?"none":pair.Host.SharedProjectiles[0].Age+"/"+pair.Host.SharedProjectiles[0].EmitAt));
                if (kind == "flash") Need(!sawActive && maxRadius == 0, "Flash has no zone volume on the wire");
                else Need(sawActive && Near(emitAt, 1.22f, .06f) && maxRadius > 5 && maxRadius <= 5.5f, "Smoke emits 1.2 s after its first contact and grows to 5.5 m: emit " + emitAt + " r " + maxRadius);
                for (int i = 0; i < 4; i++) KeepRaw(pair, hash, 0, 8);
                pair.Host.TryReadHealth(pair.Host.OwnActor, out hostAfter);
                pair.Host.TryReadHealth(pair.Client.OwnActor, out clientAfter);
                Need(hostAfter.Health == hostBefore.Health && hostAfter.Shield == hostBefore.Shield && clientAfter.Health == clientBefore.Health && clientAfter.Shield == clientBefore.Shield
                    && hostAfter.Life == hostBefore.Life && clientAfter.Life == clientBefore.Life, "No health change for anyone from a " + kind);
                foreach (var row in pair.Host.SharedProjectiles) Need(row.Ended, "Host keeps only the ended tombstone of the " + kind);
            }

        // Rule off => registration, catalogue and throws are refused (per kind).
        using (var pair = new Pair())
        {
            for (int i = 0; i < 5; i++) pair.Tick();
            pair.Host.BindProjectileWorld(new ProjectileWorld { Contact = true });
            string hash = new string('b', 64);
            StartRound(pair, new HazardWorld(), R24PlayableMatchRules.ForPreset("practice", "range").With("loot", false).With("flashbangs", false), hash);
            for (int i = 0; i < 4; i++) KeepRaw(pair, hash, 0, 8);
            Need(!pair.Host.RegisterSharedGrenade("off/flash", "flash", new Float3(0, 1.2f, .2f)), "flashbangs OFF: host refuses a flash payload");
            Need(pair.Host.RegisterSharedGrenade("on/smoke", "smoke", new Float3(0, 1.2f, .2f)), "smoke_grenades ON: smoke still allowed");
            string flashId, smokeId;
            pair.Client.RequestCatalog("weapon", "flash", 1, 1, new Float3(0, 1.2f, 0), out flashId);
            pair.Client.RequestCatalog("weapon", "smoke", 1, 1, new Float3(0, 1.2f, 0), out smokeId);
            for (int i = 0; i < 8; i++) KeepRaw(pair, hash, 0, 8);
            R24LanGrenadeState item;
            Need(!pair.Host.TryReadGrenade(flashId, out item), "flashbangs OFF: catalogue grants no flash");
            Need(pair.Host.TryReadGrenade(smokeId, out item) && item.Kind == "smoke", "Catalogue still grants smoke");
        }
        using (var pair = new Pair())
        {
            for (int i = 0; i < 5; i++) pair.Tick();
            pair.Host.BindProjectileWorld(new ProjectileWorld { Contact = true });
            string hash = new string('b', 64);
            StartRound(pair, new HazardWorld(), R24PlayableMatchRules.ForPreset("free_for_all", "range").With("zone_enabled", false).With("smoke_grenades", false), hash);
            for (int i = 0; i < 4; i++) KeepRaw(pair, hash, 0, 8);
            Need(!pair.Host.RegisterSharedGrenade("off/smoke", "smoke", new Float3(0, 1.2f, .2f)), "smoke_grenades OFF: host refuses a smoke payload");
            Need(pair.Host.RegisterSharedGrenade("on/frag", "frag", new Float3(0, 1.2f, .2f)), "Frag is never gated by the new switches");
        }

        // Late joiner receives an active smoke with its emission time.
        using (var pair = new Pair())
        {
            for (int i = 0; i < 5; i++) pair.Tick();
            pair.Host.BindCombatWorld(delegate(Float3 o, Float3 d, float range) { return range; });
            pair.Host.BindProjectileWorld(new ProjectileWorld { Contact = true });
            string hash = new string('b', 64);
            StartRound(pair, new HazardWorld(), R24PlayableMatchRules.ForPreset("free_for_all", "range").With("zone_enabled", false), hash);
            Action raw = delegate
            {
                pair.Host.PublishRawTracking(Body(hash, Rev(pair.Host), 8, 0, 0, 7, rx: 1.5f, ry: 1.2f, rz: 1.5f));
                pair.Client.PublishRawTracking(Body(hash, Rev(pair.Client), 0, 0, 0));
                pair.Tick();
            };
            for (int i = 0; i < 4; i++) raw();
            pair.Host.RequestLoot("cx4", 1, true, new Float3(8, 1.2f, 0));
            Need(pair.Host.RegisterSharedGrenade("late/smoke", "smoke", new Float3(9.5f, 1.2f, 1.5f)), "Host registers a smoke before the late join");
            pair.Host.RequestGrenade("late/smoke", 1, 0, new Float3());
            for (int i = 0; i < 8; i++) raw();
            Need(pair.Host.RequestGrenade("late/smoke", 1, 1, new Float3()), "Host throws the smoke: " + pair.Host.LastExplosiveRejection);
            for (int i = 0; i < 50; i++) raw();
            Need(pair.Host.SharedProjectiles.Length == 1 && pair.Host.SharedProjectiles[0].Active, "Smoke is emitting before the late join");
            var pipe = new Pipe();
            var late = new R24LanLobby(pipe, pipe.Close, delegate { return "unused"; }, new string('a', 64));
            pipe.Lost = late.TransportDisconnected;
            pair.HostPipe.Links.Add("late", new Link { Remote = pipe, Back = "server" });
            pipe.Links.Add("server", new Link { Remote = pair.HostPipe, Back = "late" });
            late.StartClient("server", "Late", pair.Now);
            pair.Host.TransportConnected("late");
            try
            {
                for (int i = 0; i < 10; i++) { raw(); late.Tick(pair.Now); }
                Need(late.State == R24LanLobbyState.Connected, "Late peer connects");
                bool found = false;
                foreach (var row in late.SharedProjectiles)
                    if (row.Kind == "smoke" && row.Active && row.EmitAt >= 0 && row.EmitAt <= R25SmokeCloud.LaunchDelay && row.Age > row.EmitAt && row.Radius > 0) found = true;
                Need(found, "Late joiner receives the active smoke with its emission time");
            }
            finally { late.Dispose(); pipe.Dispose(); }
        }
    }
}

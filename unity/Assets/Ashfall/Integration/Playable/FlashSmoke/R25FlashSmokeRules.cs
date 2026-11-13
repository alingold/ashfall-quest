using System;
using Ashfall.Core;

namespace Ashfall.Integration.Playable.FlashSmoke
{
    // Flashbang ("flash") and smoke ("smoke") grenades: features/flashsmoke.md.
    // Pure C# 5 rules shared by the host simulation, every client's presentation
    // and the CPU tests. Nothing here touches Health, the wire or Unity.
    public static class R25Throwables
    {
        // Kind ids are sent as text of at most 8 UTF-8 bytes.
        public const string Flash = "flash";
        public const string Smoke = "smoke";
        // Replicated match-rule keys (R24PlayableMatchRules) acting as kill switches.
        public const string FlashRule = "flashbangs";
        public const string SmokeRule = "smoke_grenades";

        public static bool IsFlashOrSmoke(string kind) { return kind == Flash || kind == Smoke; }

        public static string Label(string kind)
        {
            if (kind == Flash) return "FLASHBANG";
            if (kind == Smoke) return "SMOKE GRENADE";
            return null;
        }

        // Frag and zone are never gated here; only the two new kinds have switches.
        public static bool Allowed(string kind, bool flashbangs, bool smokeGrenades)
        {
            if (kind == Flash) return flashbangs;
            if (kind == Smoke) return smokeGrenades;
            return true;
        }

        // Length of the held trajectory preview (s), matching each kind's flight.
        public static float ArcSeconds(string kind)
        {
            if (kind == "frag") return 3.5f;
            if (kind == Flash) return R25FlashExposure.Fuse;
            return 6f;
        }

        // Loot share: a deterministic quarter of cache grenade draws become a
        // flashbang and another quarter a smoke grenade. It uses its own hash of
        // (map, site, slot), so the existing cache draw stream and row counts are
        // unchanged. A disabled kind falls back to the original draw.
        public static string LootKind(string baseKind, string mapId, int site, int slot, Func<string, bool> allowed)
        {
            if (baseKind != "frag" && baseKind != "zone") return baseKind;
            uint hash = R25SmokeCloud.Seed(mapId ?? "");
            hash = R25SmokeCloud.Mix(hash ^ unchecked((uint)site * 7907u) ^ unchecked((uint)slot * 131u) ^ 0x464c5348u);
            uint pick = hash % 8u;
            string chosen = pick < 2u ? Flash : pick < 4u ? Smoke : baseKind;
            if (chosen != baseKind && allowed != null && !allowed(chosen)) return baseKind;
            return chosen;
        }
    }

    // Per-viewer flash exposure (presentation only; the host never needs it).
    public static class R25FlashExposure
    {
        public const float Fuse = 1.6f;          // s from launch to detonation
        public const float Radius = 22f;         // m, kd reaches 0 here
        public const float Threshold = .08f;     // exposures below this are ignored
        public const float BounceDistance = 6f;  // m, blocked flashes closer than this still bounce light
        public const float BlockedBounce = .15f;
        public const float FacingFull = .5f;     // cos at/above which kf = 1
        public const float FacingBack = -.2f;    // cos at/below which kf = FacingRear
        public const float FacingRear = .35f;

        static bool Finite(float v) { return !Single.IsNaN(v) && !Single.IsInfinity(v); }

        public static float DistanceFactor(float distance)
        {
            if (!Finite(distance) || distance < 0) return 0;
            float x = distance / Radius;
            if (x >= 1) return 0;
            return Clamp01(1 - (float)Math.Pow(x, 1.5));
        }

        public static float FacingFactor(float cosine)
        {
            if (!Finite(cosine)) return FacingRear;
            if (cosine >= FacingFull) return 1;
            if (cosine <= FacingBack) return FacingRear;
            float t = (cosine - FacingBack) / (FacingFull - FacingBack);
            return FacingRear + (1 - FacingRear) * t;
        }

        public static float OcclusionFactor(bool lineClear, float distance)
        {
            if (lineClear) return 1;
            return Finite(distance) && distance < BounceDistance ? BlockedBounce : 0;
        }

        // E = kd * kf * kl; anything below Threshold counts as 0.
        public static float Exposure(float distance, float facingCosine, bool lineClear)
        {
            float e = DistanceFactor(distance) * FacingFactor(facingCosine) * OcclusionFactor(lineClear, distance);
            return e < Threshold ? 0 : Clamp01(e);
        }

        // Convenience for the presenter: eye position, view forward (any length),
        // detonation point and the world line-of-sight result.
        public static float Exposure(Float3 eye, Float3 forward, Float3 flash, bool lineClear)
        {
            if (!eye.IsFinite || !forward.IsFinite || !flash.IsFinite) return 0;
            var d = new Float3(flash.X - eye.X, flash.Y - eye.Y, flash.Z - eye.Z);
            float distance = (float)Math.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z);
            float f = (float)Math.Sqrt(forward.X * forward.X + forward.Y * forward.Y + forward.Z * forward.Z);
            // At the eye itself the flash fills the view.
            float cosine = distance < .001f || f < .000001f ? 1 : (d.X * forward.X + d.Y * forward.Y + d.Z * forward.Z) / (distance * f);
            return Exposure(distance, cosine, lineClear);
        }

        internal static float Clamp01(float v) { return v < 0 ? 0 : v > 1 ? 1 : v; }
    }

    // Blindness/deafness timeline for one viewer. Deterministic state machine:
    // Trigger(exposure) per detonation, Step(dt) per frame, read Alpha/DuckGain.
    // Overlapping flashes combine by maximum, and the presented alpha is slew
    // limited, so it can never step faster than the 80 ms ramp nor strobe.
    public sealed class R25FlashBlindness
    {
        public const float Ramp = .08f;          // s fade-in (never instantaneous)
        public const float HoldFraction = .35f;  // of T
        public const float BaseSeconds = .6f;
        public const float ExposureSeconds = 3.4f;
        public const float MaxDuckDb = 18f;
        public const float ReleaseSeconds = .25f; // minimum time to clear a full fade
        public const float MaxStep = .1f;         // long frames are clamped
        public const int Capacity = 4;

        readonly float[] age = new float[Capacity];
        readonly float[] exposure = new float[Capacity];
        readonly float[] duration = new float[Capacity];
        int count;
        float alpha, duckDb;

        public float Alpha { get { return alpha; } }
        public float DuckDb { get { return duckDb; } }
        public float DuckGain { get { return (float)Math.Pow(10, -duckDb / 20.0); } }
        public bool Active { get { return count > 0 || alpha > 0; } }
        public int Events { get { return count; } }

        public static float Duration(float e) { return BaseSeconds + ExposureSeconds * R25FlashExposure.Clamp01(e); }

        // Peak whiteout: weak exposures veil the view, strong ones (E >= 0.5) white it out.
        public static float Peak(float e) { return Math.Min(1f, .45f + 1.1f * R25FlashExposure.Clamp01(e)); }

        // 0..1 shape of one flash at time t since detonation (total length T).
        public static float Envelope(float t, float e)
        {
            float total = Duration(e);
            if (Single.IsNaN(t) || t < 0 || t >= total) return 0;
            if (t < Ramp) return t / Ramp;
            float hold = HoldFraction * total;
            if (t < Ramp + hold) return 1;
            float span = total - Ramp - hold;
            float x = (t - Ramp - hold) / span;
            return 1 - x * x * (3 - 2 * x);
        }

        // Linear audio duck in dB: full depth from the bang, eased back over T.
        public static float DuckEnvelopeDb(float t, float e)
        {
            float total = Duration(e);
            if (Single.IsNaN(t) || t < 0 || t >= total) return 0;
            float hold = Ramp + HoldFraction * total;
            float shape = t < hold ? 1 : 1 - Smooth((t - hold) / (total - hold));
            return MaxDuckDb * R25FlashExposure.Clamp01(e) * shape;
        }

        static float Smooth(float x) { x = R25FlashExposure.Clamp01(x); return x * x * (3 - 2 * x); }

        // Returns false for exposures under the threshold (nothing happens).
        public bool Trigger(float e)
        {
            if (Single.IsNaN(e) || e < R25FlashExposure.Threshold) return false;
            e = R25FlashExposure.Clamp01(e);
            int slot = count;
            if (count >= Capacity)
            {
                // Replace the flash with the least time left.
                slot = 0;
                float least = float.MaxValue;
                for (int i = 0; i < count; i++) { float left = duration[i] - age[i]; if (left < least) { least = left; slot = i; } }
            }
            else count++;
            age[slot] = 0; exposure[slot] = e; duration[slot] = Duration(e);
            return true;
        }

        public void Step(float dt)
        {
            if (Single.IsNaN(dt) || Single.IsInfinity(dt) || dt <= 0) return;
            if (dt > MaxStep) dt = MaxStep;
            float target = 0, db = 0;
            for (int i = 0; i < count; i++)
            {
                age[i] += dt;
                if (age[i] >= duration[i])
                {
                    count--; age[i] = age[count]; exposure[i] = exposure[count]; duration[i] = duration[count]; i--;
                    continue;
                }
                target = Math.Max(target, Envelope(age[i], exposure[i]) * Peak(exposure[i]));
                db = Math.Max(db, DuckEnvelopeDb(age[i], exposure[i]));
            }
            float up = dt / Ramp, down = dt / ReleaseSeconds;
            alpha = target > alpha ? Math.Min(target, alpha + up) : Math.Max(target, alpha - down);
            if (alpha < .0005f && target <= 0) alpha = 0;
            duckDb = db;
        }

        // Death/teleport: drop pending flashes; the view eases back (not a cut).
        public void Release() { count = 0; duckDb = 0; }

        public void Reset() { count = 0; alpha = 0; duckDb = 0; }
    }

    // Deterministic smoke cloud: every peer derives the same cloud from the
    // replicated canister position and emission time.
    public static class R25SmokeCloud
    {
        public const float ImpactDelay = 1.2f;   // s after first impact
        public const float LaunchDelay = 2.0f;   // s after launch, whichever comes first
        public const float GrowSeconds = 2.5f;
        public const float MaxRadius = 5.5f;     // m (clients reject rows wider than 15 m)
        public const float Life = 16f;           // s of emission (launch age <= 18 s < 20 s row limit)
        public const float ThinSeconds = 3f;
        public const float Lift = .4f;           // cloud centre above the canister, as a fraction of R
        public const int PuffCount = 40;
        public const int ReducedPuffs = 12;

        // stuckAge < 0 means the canister has not touched anything yet.
        public static float EmissionStart(float stuckAge)
        {
            if (Single.IsNaN(stuckAge) || stuckAge < 0) return LaunchDelay;
            return Math.Min(stuckAge + ImpactDelay, LaunchDelay);
        }

        // Grows 0 -> MaxRadius with an ease-out over GrowSeconds, then holds.
        public static float Radius(float cloudAge)
        {
            if (Single.IsNaN(cloudAge) || cloudAge <= 0 || cloudAge >= Life) return 0;
            if (cloudAge >= GrowSeconds) return MaxRadius;
            float x = 1 - cloudAge / GrowSeconds;
            return MaxRadius * (1 - x * x);
        }

        // Opacity multiplier: 1, then thins linearly over the last ThinSeconds.
        public static float Density(float cloudAge)
        {
            if (Single.IsNaN(cloudAge) || cloudAge < 0 || cloudAge >= Life) return 0;
            float left = Life - cloudAge;
            return left >= ThinSeconds ? 1 : left / ThinSeconds;
        }

        public static bool Expired(float cloudAge) { return Single.IsNaN(cloudAge) || cloudAge >= Life; }

        public static Float3 Center(Float3 canister, float radius)
        { return new Float3(canister.X, canister.Y + Lift * radius, canister.Z); }

        // How strongly a camera at `eye` is inside the cloud (0 outside, 1 deep inside).
        public static float ViewAmount(Float3 eye, Float3 canister, float cloudAge)
        {
            float r = Radius(cloudAge);
            if (r <= 0 || !eye.IsFinite || !canister.IsFinite) return 0;
            var c = Center(canister, r);
            float dx = eye.X - c.X, dy = eye.Y - c.Y, dz = eye.Z - c.Z;
            float d = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            float depth = R25FlashExposure.Clamp01((.92f * r - d) / (.35f * r));
            return depth * depth * (3 - 2 * depth) * Density(cloudAge);
        }

        // True when the segment a-b passes through the dense core (bots, optional).
        public static bool SegmentObscured(Float3 a, Float3 b, Float3 canister, float cloudAge)
        {
            float r = Radius(cloudAge);
            if (r <= 0 || Density(cloudAge) < .5f || !a.IsFinite || !b.IsFinite || !canister.IsFinite) return false;
            var c = Center(canister, r);
            float ux = b.X - a.X, uy = b.Y - a.Y, uz = b.Z - a.Z, len2 = ux * ux + uy * uy + uz * uz;
            float t = len2 < .000001f ? 0 : ((c.X - a.X) * ux + (c.Y - a.Y) * uy + (c.Z - a.Z) * uz) / len2;
            t = R25FlashExposure.Clamp01(t);
            float px = a.X + ux * t - c.X, py = a.Y + uy * t - c.Y, pz = a.Z + uz * t - c.Z;
            return px * px + py * py + pz * pz < .75f * r * .75f * r;
        }

        // FNV-1a over UTF-16 code units: stable on every peer.
        public static uint Seed(string id)
        {
            uint hash = 2166136261u;
            if (id != null) for (int i = 0; i < id.Length; i++) hash = unchecked((hash ^ id[i]) * 16777619u);
            return hash;
        }

        // Murmur3 finaliser.
        public static uint Mix(uint h)
        {
            unchecked { h ^= h >> 16; h *= 0x85ebca6bu; h ^= h >> 13; h *= 0xc2b2ae35u; h ^= h >> 16; }
            return h;
        }

        static float Unit(uint seed, int index, int channel)
        { return (Mix(seed ^ Mix(unchecked((uint)index * 0x9e3779b9u + (uint)channel * 0x632be5abu))) & 0xffffff) / 16777216f; }

        // Puff i of a cloud in unit-radius space (multiply by Radius). The first
        // eight puffs form a dense core; the rest fill a flattened ellipsoid.
        public static void Puff(uint seed, int index, out Float3 offset, out float size, out float alpha)
        {
            float u = Unit(seed, index, 0), v = Unit(seed, index, 1), w = Unit(seed, index, 2);
            float azimuth = u * 6.2831853f, z = v * 2 - 1, ring = (float)Math.Sqrt(Math.Max(0, 1 - z * z));
            bool core = index < 8;
            float reach = core ? .15f + .25f * w : .35f + .5f * (float)Math.Pow(w, .6);
            offset = new Float3((float)Math.Cos(azimuth) * ring * reach, z * reach * .55f - .05f, (float)Math.Sin(azimuth) * ring * reach);
            size = core ? .52f + .12f * Unit(seed, index, 3) : .36f + .2f * Unit(seed, index, 3) * (1.1f - reach);
            alpha = core ? 1f : .62f + .3f * Unit(seed, index, 4);
        }
    }
}

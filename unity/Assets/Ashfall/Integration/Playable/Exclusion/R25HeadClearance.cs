using System;
using Ashfall.Core;
namespace Ashfall.Integration.Playable.Exclusion
{
    // World supplies a continuous hull sweep and an exact overlap predicate.
    // Every leg (including support elevation) commits only a verified endpoint.
    public sealed class R25HeadClearance
    {
        public const float Skin = .008f;
        public const float PushLimit = .06f;
        public Float3 Move(Float3 head, Float3 requested, Func<Float3, Float3, Float3> sweep, Func<Float3, bool> clear, float contactBackoff = Skin)
        {
            if (!head.IsFinite || !requested.IsFinite || float.IsNaN(contactBackoff) || float.IsInfinity(contactBackoff) || contactBackoff < 0 || contactBackoff > .05f || !clear(head)) return new Float3();
            Float3 safe = sweep(head, requested);
            if (!safe.IsFinite) return new Float3();
            float length = Length(safe);
            if (Length(Sub(safe, requested)) > .00001f && length > 0)
                safe = Scale(safe, Math.Max(0, length - contactBackoff) / length);
            // Never publish a grazing TOI that disagrees with the overlap oracle.
            // Refuse the leg rather than binary-search across a multi-slide path.
            return clear(Add(head, safe)) ? safe : new Float3();
        }
        public bool Rise(Float3 head, float rise, Func<Float3, Float3, Float3> sweep, Func<Float3, bool> clear, out Float3 accepted)
        {
            accepted = Move(head, new Float3(0, rise, 0), sweep, clear);
            if (Length(Sub(accepted, new Float3(0, rise, 0))) > .0001f) { accepted = new Float3(); return false; }
            return clear(Add(head, accepted));
        }
        public Float3 Push(Float3 blocked, float budget, Func<Float3, Float3> body, Func<Float3, bool> supported)
        {
            Float3 flat = new Float3(blocked.X, 0, blocked.Z);
            float length = Length(flat);
            if (!flat.IsFinite || length < .00001f || budget <= 0) return new Float3();
            flat = Scale(flat, Math.Min(Math.Min(budget, PushLimit), length) / length);
            Float3 safe = body(flat);
            // Do not permit a query to add vertical motion or amplify correction.
            if (!safe.IsFinite || Math.Abs(safe.Y) > .00001f || Length(safe) > Length(flat) + .0001f || !supported(safe)) return new Float3();
            return safe;
        }
        public static Float3 Add(Float3 a, Float3 b) { return new Float3(a.X+b.X,a.Y+b.Y,a.Z+b.Z); }
        public static Float3 Sub(Float3 a, Float3 b) { return new Float3(a.X-b.X,a.Y-b.Y,a.Z-b.Z); }
        public static Float3 Scale(Float3 a, float b) { return new Float3(a.X*b,a.Y*b,a.Z*b); }
        public static float Length(Float3 a) { return (float)Math.Sqrt(a.X*a.X+a.Y*a.Y+a.Z*a.Z); }
    }
}

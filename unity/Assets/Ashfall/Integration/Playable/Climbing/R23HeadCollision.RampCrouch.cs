#if UNITY_6000_0_OR_NEWER
using Ashfall.Integration.Playable.Exclusion;
using UnityEngine;

namespace Ashfall.Integration.Playable.Climbing
{
    public sealed partial class R23HeadCollision
    {
        readonly RaycastHit[] crouchHits = new RaycastHit[128];

        // Upward full-sphere cast, including mesh backfaces. The starting side
        // check distinguishes an overhead ramp from a ramp being walked on.
        public float CrouchCeiling(Vector3 currentEye, Vector3 candidateEye, float floorY, bool sideEntry = false)
        {
            float radius = EyeSafetyRadius;
            Vector3 start = new Vector3(candidateEye.x, floorY + radius + .015f, candidateEye.z);
            float length = Mathf.Max(0, candidateEye.y - start.y + .25f);
            if (length <= 0) return float.PositiveInfinity;
            Physics.SyncTransforms();
            bool backfaces = Physics.queriesHitBackfaces;
            int count;
            try
            {
                Physics.queriesHitBackfaces = true;
                count = Physics.SphereCastNonAlloc(start, radius, Vector3.up, crouchHits,
                    length, Bots.R24RagdollRules.GameplayQueryMask, QueryTriggerInteraction.Ignore);
            }
            finally { Physics.queriesHitBackfaces = backfaces; }
            if (count == crouchHits.Length) return floorY; // Saturation fails closed.
            float ceiling = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = crouchHits[i];
                if (!Relevant(hit.collider) || hit.distance <= 0 || Mathf.Abs(hit.normal.y) < .35f) continue;
                Vector3 horizontal = currentEye - hit.point; horizontal.y = 0;
                float surfaceHere = hit.point.y - Vector3.Dot(horizontal, hit.normal) / hit.normal.y;
                Vector3 move = candidateEye - currentEye; move.y = 0;
                bool lateral = move.sqrMagnitude > .0000001f && Mathf.Abs(Vector3.Dot(move.normalized, hit.normal)) < .1f;
                if (currentEye.y >= surfaceHere - .005f)
                {
                    if (!sideEntry || !lateral) continue;
                    // An eye above the extrapolated plane can enter underneath
                    // from a finite side edge. An eye already over the actual
                    // footprint belongs on top instead and must never cross it.
                    Bounds bounds=hit.collider.bounds;RaycastHit roof;
                    if(hit.collider.Raycast(new Ray(new Vector3(currentEye.x,bounds.max.y+.1f,currentEye.z),Vector3.down),out roof,bounds.size.y+.2f))continue;
                }
                float bodyReserve=sideEntry&&lateral?.05f:0;
                ceiling = Mathf.Min(ceiling, start.y + hit.distance - R25RampCrouch.Reserve - bodyReserve);
            }
            return ceiling;
        }
    }
}
#endif

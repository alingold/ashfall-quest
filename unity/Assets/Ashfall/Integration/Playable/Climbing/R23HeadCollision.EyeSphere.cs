#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using Ashfall.Integration.Playable.Expansion;
using Ashfall.Integration.Playable.Exclusion;
using UnityEngine;

namespace Ashfall.Integration.Playable.Climbing
{
    public sealed partial class R23HeadCollision
    {
        public static bool EyeSafetyEnabled = true;
        public static bool FullEyeSafety { get { return R25Features.HeadClearance && EyeSafetyEnabled; } }
        const float EyeSurfaceMinimum = .03f;
        readonly RaycastHit[] eyeSweepHits = new RaycastHit[128];
        SphereCollider eyeSphere;
        Camera eyeCamera;
        readonly Vector3[] eyeCorners = new Vector3[4];
        public float EyeSafetyRadius
        {
            get
            {
                if (eyeCamera == null && head != null) eyeCamera = head.GetComponent<Camera>();
                float radius = .12f;
                if (eyeCamera == null) return radius;
                if (eyeCamera.stereoEnabled)
                {
                    for (int index = 0; index < 2; index++)
                    {
                        var side = index == 0 ? Camera.StereoscopicEye.Left : Camera.StereoscopicEye.Right;
                        Matrix4x4 projection = eyeCamera.GetStereoProjectionMatrix(side);
                        float tx = R25ObstructionFade.FrustumTangent(projection.m00, projection.m02);
                        float ty = R25ObstructionFade.FrustumTangent(projection.m11, projection.m12);
                        Vector4 column = eyeCamera.GetStereoViewMatrix(side).inverse.GetColumn(3);
                        float offset = Vector3.Distance(head.position, new Vector3(column.x, column.y, column.z));
                        radius = Mathf.Max(radius, offset + eyeCamera.nearClipPlane * Mathf.Sqrt(1 + tx * tx + ty * ty) + EyeSurfaceMinimum + .005f);
                    }
                }
                else
                {
                    eyeCamera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), eyeCamera.nearClipPlane, Camera.MonoOrStereoscopicEye.Mono, eyeCorners);
                    for (int i = 0; i < 4; i++) radius = Mathf.Max(radius, eyeCorners[i].magnitude + EyeSurfaceMinimum + .005f);
                }
                return radius;
            }
        }
        void EnsureEyeSphere()
        {
            if (eyeSphere == null) { eyeSphere = proxy.gameObject.AddComponent<SphereCollider>(); eyeSphere.enabled = false; }
            eyeSphere.radius = EyeSafetyRadius; eyeSphere.contactOffset = Margin;
        }
        bool EyeSphereClear(Vector3 point, Vector3 separatingMotion)
        {
            int count = Physics.OverlapSphereNonAlloc(point, EyeSafetyRadius, overlaps,
                Bots.R24RagdollRules.GameplayQueryMask, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Collider c = overlaps[i]; if (!Relevant(c)) continue;
                if (separatingMotion.sqrMagnitude > 1e-12f)
                {
                    Vector3 away; float depth;
                    if (EyeContact(c, point, out away, out depth) && Vector3.Dot(separatingMotion, away) >= -.00000001f) continue;
                }
                return false;
            }
            return true;
        }
        bool EyeNearest(Vector3 point, Vector3 motion, out RaycastHit closest, float queryRadius = -1)
        {
            closest = new RaycastHit(); float length = motion.magnitude;
            if (length < .000001f) return false;
            bool old = Physics.queriesHitBackfaces; int count;
            try
            {
                Physics.queriesHitBackfaces = true;
                count = Physics.SphereCastNonAlloc(point, queryRadius > 0 ? queryRadius : EyeSafetyRadius, motion / length, eyeSweepHits,
                    length + Margin, Bots.R24RagdollRules.GameplayQueryMask, QueryTriggerInteraction.Ignore);
            }
            finally { Physics.queriesHitBackfaces = old; }
            if (count == eyeSweepHits.Length) { closest.distance = 0; closest.normal = -motion / length; return true; }
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = eyeSweepHits[i]; if (!Relevant(hit.collider)) continue;
                if (hit.distance <= 0 && hit.point == Vector3.zero) continue;
#if UNITY_EDITOR
                if (CaptureSweepDiagnostics) LastSweepDiagnostics += "\neyeSphere raw=" + hit.collider.name + " distance=" + hit.distance
                    + " normal=" + hit.normal.ToString("F6") + " originSide=" + Vector3.Dot(point-hit.point,hit.normal) + " incoming=" + Vector3.Dot(motion,hit.normal);
#endif
                // Preserve separating front-side near contacts. Blindly reversing
                // every positive-dot normal makes tangential floor escape stick.
                if (hit.collider is MeshCollider && Vector3.Dot(motion, hit.normal) > 0
                    && (hit.distance > Margin || Vector3.Dot(point-hit.point, hit.normal) < -.0001f)) hit.normal = -hit.normal;
                if (hit.distance <= Margin && Vector3.Dot(motion, hit.normal) > 0) continue;
                if (Vector3.Dot(motion, hit.normal) >= -.000001f || hit.distance >= nearest) continue;
                closest = hit; nearest = hit.distance;
            }
            return !float.IsPositiveInfinity(nearest);
        }
        float EyeOverlapDepth(Vector3 point, out Vector3 deepestAway)
        {
            deepestAway = Vector3.zero; float maximum = 0;
            int count = Physics.OverlapSphereNonAlloc(point, EyeSafetyRadius, overlaps,
                Bots.R24RagdollRules.GameplayQueryMask, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider c = overlaps[i]; if (!Relevant(c)) continue;
                Vector3 away; float depth;
                if (EyeContact(c, point, out away, out depth) && depth > maximum)
                { maximum = depth; deepestAway = away; }
            }
            return maximum;
        }
        public Vector3 EscapeEyeMotion(Vector3 point, Vector3 requested, bool slide, out bool ceiling)
        {
            ceiling = false;
            if (!FullEyeSafety || !Finite(point) || !Finite(requested)) return Vector3.zero;
            Physics.SyncTransforms();
            Vector3 away; float initialDepth = EyeOverlapDepth(point, out away);
            if (float.IsInfinity(initialDepth)) return Vector3.zero;
            // A farther contact may get closer while the nearest one separates.
            // Keep the starting MINIMUM clearance continuously, rather than
            // demanding separation from every surface in the protection sphere.
            // Shrinking slightly makes the cast start outside mesh contacts;
            // the endpoint check restores the exact measured clearance bound.
            float radius = Mathf.Max(.00001f, EyeSafetyRadius - initialDepth - .0005f);
            if (initialDepth > EyeSafetyRadius && Vector3.Dot(requested, away) < -.00000001f) return Vector3.zero;
            // R5 (playtest 4 wall rub): inside the margin, never move toward the
            // nearest surface; only along it or away. A near-zero escape sphere
            // otherwise slid through thick mesh walls once the far face became
            // "nearest", then recovery popped the player back (vibration).
            if (away.sqrMagnitude > .000001f)
            {
                Vector3 n = away.normalized; float into = Vector3.Dot(requested, n);
                if (into < 0) requested -= into * n;
            }
            Vector3 total = Vector3.zero, remaining = requested;
            for (int pass = 0; pass < (slide ? 6 : 1); pass++)
            {
                float length = remaining.magnitude; if (length < .000001f) break;
                RaycastHit hit;
                if (!EyeNearest(point + total, remaining, out hit, radius) || hit.distance >= length)
                { total += remaining; break; }
                Vector3 safe = remaining / length * Mathf.Max(0, hit.distance - .0005f);
                total += safe; remaining -= safe;
                if (hit.normal.y < -.1f && remaining.y > 0) ceiling = true;
                if (!slide) break;
                remaining = Vector3.ProjectOnPlane(remaining, hit.normal);
            }
            float endDepth = EyeOverlapDepth(point + total, out away);
            return endDepth <= initialDepth + .00002f ? total : Vector3.zero;
        }
        // ComputePenetration ignores mesh backfaces. For recovery only, find the
        // actual nearest triangle without its winding, and push to the same side.
        // Cached vertex/index arrays avoid mesh API allocations on later queries.
        sealed class MeshData { public Mesh mesh; public Vector3[] vertices; public int[] triangles; }
        readonly Dictionary<Mesh, MeshData> recoveryMeshes = new Dictionary<Mesh, MeshData>();
        bool EyeContact(Collider collider, Vector3 point, out Vector3 away, out float depth)
        {
            EnsureEyeSphere();
            var meshCollider = collider as MeshCollider;
            if (meshCollider == null || meshCollider.convex)
            {
                eyeSphere.enabled = true;
                try { return Physics.ComputePenetration(eyeSphere, point, Quaternion.identity, collider,
                    collider.transform.position, collider.transform.rotation, out away, out depth); }
                finally { eyeSphere.enabled = false; }
            }
            Mesh mesh = meshCollider.sharedMesh; away = Vector3.zero; depth = 0;
            if (mesh == null || !mesh.isReadable) return false;
            MeshData data;
            if (!recoveryMeshes.TryGetValue(mesh, out data))
            {
                if (recoveryMeshes.Count >= 64) recoveryMeshes.Clear();
                data = new MeshData { mesh = mesh, vertices = mesh.vertices, triangles = mesh.triangles }; recoveryMeshes.Add(mesh, data);
            }
            float nearest = EyeSafetyRadius * EyeSafetyRadius + .000001f; Vector3 nearestPoint = point, normal = Vector3.up;
            bool found = false; Vector3 localPoint = collider.transform.InverseTransformPoint(point);
            Vector3 scale = collider.transform.lossyScale;
            float minimumScale = Mathf.Min(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            for (int i = 0; i < data.triangles.Length; i += 3)
            {
                Vector3 la=data.vertices[data.triangles[i]], lb=data.vertices[data.triangles[i+1]], lc=data.vertices[data.triangles[i+2]];
                Vector3 low=Vector3.Min(la,Vector3.Min(lb,lc)), high=Vector3.Max(la,Vector3.Max(lb,lc));
                Vector3 localGap=Vector3.Max(Vector3.zero,Vector3.Max(low-localPoint,localPoint-high));
                if (localGap.sqrMagnitude * minimumScale * minimumScale > nearest) continue;
                Vector3 a = collider.transform.TransformPoint(la), b = collider.transform.TransformPoint(lb), c = collider.transform.TransformPoint(lc);
                Vector3 faceNormal=Vector3.Cross(b-a,c-a);if(faceNormal.sqrMagnitude<1e-12f)continue;
                Vector3 candidate = ClosestEyeTriangle(point, a, b, c); float distance = (point - candidate).sqrMagnitude;
                if (distance >= nearest) continue;
                found = true; nearest = distance; nearestPoint = candidate; normal = faceNormal.normalized;
            }
            if (!found) return false;
            float gap = Mathf.Sqrt(nearest); depth = EyeSafetyRadius - gap;
            away = gap > .00001f ? (point - nearestPoint) / gap : normal;
            return depth > 0;
        }
        static Vector3 ClosestEyeTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab=b-a,ac=c-a,ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);if(d1<=0&&d2<=0)return a;
            Vector3 bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return b;
            float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return a+ab*(d1/(d1-d3));
            Vector3 cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return c;
            float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return a+ac*(d2/(d2-d6));
            float va=d3*d6-d5*d4;if(va<=0&&d4-d3>=0&&d5-d6>=0)return b+(c-b)*((d4-d3)/(d4-d3+d5-d6));
            float den=va+vb+vc;if(Mathf.Abs(den)<1e-15f)return a;return a+ab*(vb/den)+ac*(vc/den);
        }
    }
}
#endif

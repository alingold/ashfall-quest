#if UNITY_6000_0_OR_NEWER
using Ashfall.Integration.Playable.Exclusion;
using Ashfall.Integration.Playable.Expansion;
using UnityEngine;

namespace Ashfall.Integration.Playable
{
    // Projection-aware obstruction guard. The full-eye safety path is opaque
    // before an unsafe render and eases out only after clearance is restored.
    // The historical timed fade remains available with full-eye safety disabled.
    [DefaultExecutionOrder(1100)]
    public sealed class R25ObstructedViewFade : MonoBehaviour
    {
        // Lower bound on the eye sphere radius (m).
        public float MinimumRadius = 0.08f;
        // Upper bound, so an unusual projection cannot fade normal play.
        public float MaximumRadius = 0.16f;
        // Margin over the exact near-rectangle corner distance.
        public float RadiusSafety = 1.1f;
        // Drawn after world/weapons (Overlay+50) but before the VR UI (Overlay+100),
        // so menus stay usable even if the player is stuck in geometry.
        public int RenderQueue = 4050;
        // Fail-safe: if the eye stays obstructed this long (recovery failed, e.g. a
        // spawn deep inside a solid) the view is partly restored so the player is
        // never left blind; it stays dimmed until the eye is clear again.
        public float MaximumBlackSeconds = 6f;
        public float StuckAlpha = 0.35f;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        readonly R25ObstructionFade policy = new R25ObstructionFade();
        R23VRRig rig;
        Camera view;
        MeshRenderer quadRenderer;
        Material material;
        Mesh mesh;
        float presentedAlpha = -1;
        float obstructedSeconds, stuckBlend;
        bool stuck;
        Vector3 carrierClearEye;
        bool haveCarrierEye, carrierViewBlocked;

        public float Alpha { get { return policy.Alpha; } }
        public bool Stuck { get { return stuck; } }
        public bool FullyFaded { get { return R25Features.ObstructedViewFade && isActiveAndEnabled && policy.FullyFaded && !stuck; } }
        public float LastRadius { get; private set; }
        public bool CarrierPathBlocked { get { return carrierViewBlocked; } }
        public void NotifyRootReset() { haveCarrierEye = false; carrierViewBlocked = false; }
#if UNITY_EDITOR
        // The XR-off native harness advances one explicit simulation step per
        // rendered frame, including 72/120 Hz and hitch cases, without sleeping.
        public float NativeProbeSeconds;
        public void NativeProbeRenderGuard() { ProtectBeforeRender(); }
#endif
        float FrameSeconds
        {
            get
            {
#if UNITY_EDITOR
                if (NativeProbeSeconds > 0 && R25NetBot.Requested) return NativeProbeSeconds;
#endif
                return Time.unscaledDeltaTime;
            }
        }

        public void Initialize(R23VRRig owner, Camera camera)
        {
            rig = owner;
            view = camera;
            var shader = Resources.Load<Shader>("R23VrUi");
            if (shader == null || view == null)
            {
                Debug.LogWarning("R25 obstructed-view fade disabled: R23VrUi shader or head camera unavailable");
                enabled = false;
                return;
            }
            material = new Material(shader);
            material.name = "R25 obstructed view fade";
            material.SetColor(ColorId, new Color(0, 0, 0, 0));
            material.renderQueue = RenderQueue;
            mesh = BuildQuad(2f);
            var node = new GameObject("R25 obstructed view fade");
            // Layer 18 (own presentation art) is drawn by the head camera only; the
            // scopes and tactical map exclude it.
            node.layer = 18;
            node.transform.SetParent(view.transform, false);
            node.transform.localPosition = new Vector3(0, 0, Mathf.Max(view.nearClipPlane * 1.5f, view.nearClipPlane + .01f));
            node.transform.localRotation = Quaternion.identity;
            node.AddComponent<MeshFilter>().sharedMesh = mesh;
            quadRenderer = node.AddComponent<MeshRenderer>();
            quadRenderer.sharedMaterial = material;
            quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            quadRenderer.receiveShadows = false;
            quadRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            quadRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            quadRenderer.enabled = false;
            Application.onBeforeRender += ProtectBeforeRender;
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += ProtectCamera;
        }

        void LateUpdate()
        {
            if (quadRenderer == null) return;
            var collision = rig == null ? null : rig.HeadCollision;
            bool active = R25Features.ObstructedViewFade && rig != null && rig.isActiveAndEnabled && rig.Tracking && rig.UseSourceHeadHull
                && (Climbing.R23HeadCollision.FullEyeSafety || !rig.CarrierActive) && collision != null && collision.Ready;
            if (!active)
            {
                policy.Reset();
                obstructedSeconds = 0;
                stuckBlend = 0;
                stuck = false;
                Present();
                return;
            }
            float radius = Radius();
            LastRadius = radius;
            bool enter = false, exit = false;
            if (!policy.Obstructed) enter = Obstructed(collision, radius);
            else exit = Obstructed(collision, Climbing.R23HeadCollision.FullEyeSafety ? radius + .002f : radius * policy.ExitRadiusScale);
            policy.Step(enter || rig.HeadClearanceBlocked || carrierViewBlocked,
                exit || rig.HeadClearanceBlocked || carrierViewBlocked, FrameSeconds);
            if (Climbing.R23HeadCollision.FullEyeSafety)
            {
                // An actual safety breach is opaque before this frame can render.
                // Never restore an unsafe
                // dimmed scene after a timer; menus remain above the black quad.
                stuck = false; stuckBlend = 0; obstructedSeconds = 0;
                ProtectBeforeRender(); Present(); return;
            }
            float step = Mathf.Min(Mathf.Max(FrameSeconds, 0), .1f);
            if (policy.Obstructed) obstructedSeconds += step;
            else
            {
                obstructedSeconds = 0;
                stuck = false;
            }
            if (!stuck && obstructedSeconds > MaximumBlackSeconds)
            {
                stuck = true;
                Debug.LogWarningFormat(this, "R25 obstructed view held {0:F1} s: restoring a dimmed view (head {1})", obstructedSeconds, view.transform.position.ToString("F3"));
            }
            // Ease between the full fade and the dimmed stuck view (never a hard cut).
            stuckBlend = Mathf.MoveTowards(stuckBlend, stuck ? 1 : 0, step * 2f);
            Present();
        }

        [BeforeRenderOrder(2000)]
        void ProtectBeforeRender()
        {
            if (!Climbing.R23HeadCollision.FullEyeSafety || !R25Features.ObstructedViewFade || quadRenderer == null
                || rig == null || !rig.Tracking || !rig.UseSourceHeadHull || rig.HeadCollision == null || !rig.HeadCollision.Ready) return;
            GuardOwnedHeadPath();
            float radius = Mathf.Max(.07f, rig.HeadCollision.EyeSafetyRadius - .005f);
            if (rig.HeadCollision.EyeObstructed(view.transform.position, radius) || rig.HeadClearanceBlocked || carrierViewBlocked)
            { policy.Protect(); stuck = false; stuckBlend = 0; Present(); }
        }
        void GuardOwnedHeadPath()
        {
            Vector3 eye = view.transform.position;
            if (!rig.CarrierActive)
            {
                carrierClearEye = eye; haveCarrierEye = !rig.HeadClearanceBlocked; carrierViewBlocked = false;
                return;
            }
            if (!haveCarrierEye)
            {
                carrierClearEye = eye; haveCarrierEye = true;
            }
            // A host-owned carrier keeps its canonical root target. If its head
            // path crosses a face, remain opaque even after the target has moved
            // beyond overlap range. Only a clear path back from the retained side
            // (or an explicit validated root reset/exit) releases this guard.
            Vector3 requested = eye - carrierClearEye; bool ceiling;
            Vector3 accepted = rig.HeadCollision.Sweep(carrierClearEye, requested, false, out ceiling, false, true);
            carrierViewBlocked = (accepted - requested).sqrMagnitude > .00000001f || !rig.HeadCollision.HeadClear(eye);
            if (!carrierViewBlocked) carrierClearEye = eye;
        }
        void ProtectCamera(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
        { if (camera == view) ProtectBeforeRender(); }

        bool Obstructed(Climbing.R23HeadCollision collision, float radius)
        {
            // Full-eye radius already includes both stereo eye offsets. Testing
            // it at each eye again would double-count IPD and fade safe contacts.
            if (Climbing.R23HeadCollision.FullEyeSafety || !view.stereoEnabled) return collision.EyeObstructed(view.transform.position, radius);
            return collision.EyeObstructed(EyePosition(Camera.StereoscopicEye.Left), radius)
                || collision.EyeObstructed(EyePosition(Camera.StereoscopicEye.Right), radius);
        }

        Vector3 EyePosition(Camera.StereoscopicEye eye)
        {
            Vector4 column = view.GetStereoViewMatrix(eye).inverse.GetColumn(3);
            Vector3 position = new Vector3(column.x, column.y, column.z);
            return float.IsNaN(position.x) || float.IsInfinity(position.x) ? view.transform.position : position;
        }

        // Near-rectangle corner distance from the actual projection(s) in use.
        float Radius()
        {
            float near = view.nearClipPlane, tanX = 0, tanY = 0;
            if (view.stereoEnabled)
            {
                Accumulate(view.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left), ref tanX, ref tanY);
                Accumulate(view.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right), ref tanX, ref tanY);
            }
            else Accumulate(view.projectionMatrix, ref tanX, ref tanY);
            float radius = R25ObstructionFade.EyeRadius(near, tanX, tanY, RadiusSafety, MinimumRadius, MaximumRadius);
            return Climbing.R23HeadCollision.FullEyeSafety && rig != null && rig.HeadCollision != null
                ? Mathf.Max(.07f, rig.HeadCollision.EyeSafetyRadius - .005f) : radius;
        }

        static void Accumulate(Matrix4x4 projection, ref float tanX, ref float tanY)
        {
            tanX = Mathf.Max(tanX, R25ObstructionFade.FrustumTangent(projection.m00, projection.m02));
            tanY = Mathf.Max(tanY, R25ObstructionFade.FrustumTangent(projection.m11, projection.m12));
        }

        void Present()
        {
            float alpha = Mathf.Lerp(policy.Alpha, Mathf.Min(policy.Alpha, StuckAlpha), stuckBlend);
            if (Mathf.Abs(alpha - presentedAlpha) < .002f) return;
            presentedAlpha = alpha;
            material.SetColor(ColorId, new Color(0, 0, 0, alpha));
            quadRenderer.enabled = alpha > .001f;
        }

        static Mesh BuildQuad(float size)
        {
            float h = size * .5f;
            var result = new Mesh();
            result.name = "R25 obstructed view fade quad";
            result.vertices = new[] { new Vector3(-h, -h, 0), new Vector3(h, -h, 0), new Vector3(h, h, 0), new Vector3(-h, h, 0) };
            result.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            result.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            result.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            // Never frustum-culled while attached just in front of the eye.
            result.bounds = new Bounds(Vector3.zero, new Vector3(size, size, size));
            return result;
        }

        void OnDestroy()
        {
            Application.onBeforeRender -= ProtectBeforeRender;
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= ProtectCamera;
            if (material != null) Destroy(material);
            if (mesh != null) Destroy(mesh);
        }
    }
}
#endif

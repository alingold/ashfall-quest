using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Ashfall.Integration.Playable.Rendering
{
    /// <summary>URP17 render-graph fullscreen source FILMIC. Disabled unless source and qualified-input session flags are both set.</summary>
    public sealed class R23GodotFilmicFeature : ScriptableRendererFeature
    {
        [SerializeField] Shader filmicShader;
        Material material;
        FilmicPass pass;
        public override void Create()
        {
            CoreUtils.Destroy(material); material = null;
            if (filmicShader == null) filmicShader = Resources.Load<Shader>("R23GodotFilmic");
            if (filmicShader != null) material = CoreUtils.CreateEngineMaterial(filmicShader);
            pass = new FilmicPass();
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var session = R23GodotFilmic.Session;
            if (!session.SourceActive) { R23GodotFilmic.LastStatus = "Source environment disabled"; return; }
            if (!session.InputQualified) { R23GodotFilmic.LastStatus = "Input domain not qualified"; return; }
            if (material == null || !material.shader.isSupported) { R23GodotFilmic.LastStatus = "Source FILMIC shader unavailable/unsupported"; return; }
            pass.ResourcesMaterial = material;
            renderer.EnqueuePass(pass);
        }
        protected override void Dispose(bool disposing) { CoreUtils.Destroy(material); material = null; pass = null; }

        sealed class FilmicPass : ScriptableRenderPass
        {
            static readonly int Parameters = Shader.PropertyToID("_R23FilmicParameters");
            public FilmicPass()
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                requiresIntermediateTexture = true;
            }
            static bool HdrFormat(GraphicsFormat format)
            {
                return format == GraphicsFormat.R16G16B16A16_SFloat || format == GraphicsFormat.B10G11R11_UFloatPack32 || format == GraphicsFormat.R32G32B32A32_SFloat;
            }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var camera = frameData.Get<UniversalCameraData>();
                var session = R23GodotFilmic.Session;
                if (!session.SourceActive || !session.InputQualified) return;
                var actualCamera = camera.camera;
                if ((actualCamera.cameraType != CameraType.Game && actualCamera.cameraType != CameraType.VR) ||
                    !camera.resolveFinalTarget || (session.TargetCamera != null && session.TargetCamera != actualCamera)) return;
                if (camera.isHDROutputActive) { R23GodotFilmic.LastStatus = "HDR display output requires a separate output transform; SDR source pass bypassed"; return; }
                if (resources.isActiveTargetBackBuffer) { R23GodotFilmic.LastStatus = "No HDR intermediate camera color"; return; }
                if (!camera.isHdrEnabled) { R23GodotFilmic.LastStatus = "Camera HDR disabled"; return; }
                var source = resources.activeColorTexture;
                var descriptor = graph.GetTextureDesc(source);
                if (!HdrFormat(descriptor.colorFormat)) { R23GodotFilmic.LastStatus = "LDR scene target; bypassed without fake recovery"; return; }
                var featureMaterial = ResourcesMaterial;
                if (featureMaterial == null) return;
                var block = new MaterialPropertyBlock();
                // Snapshot per graph pass: other cameras/session changes cannot mutate its constants.
                block.SetVector(Parameters, new Vector4(session.Exposure, session.White, (float)session.InputDomain, QualitySettings.activeColorSpace == ColorSpace.Linear ? 1 : 0));
                descriptor.name = "Godot source FILMIC HDR color"; descriptor.clearBuffer = false;
                // Clone dimensions, XR array slices, dynamic resolution and HDR format unchanged.
                var destination = graph.CreateTexture(descriptor);
                var parameters = new RenderGraphUtils.BlitMaterialParameters(source, destination, featureMaterial, 0);
                parameters.propertyBlock = block; // defaults numSlices=-1: all XR slices.
                graph.AddBlitPass(parameters, passName: "Godot source FILMIC");
                resources.cameraColor = destination;
                R23GodotFilmic.RecordedPasses++; R23GodotFilmic.LastStatus = "Recorded source FILMIC HDR pass (GPU execution unverified)";
            }
            // Material is assigned by the owning feature just before enqueue, never a global shader lookup per eye.
            public Material ResourcesMaterial;
        }
    }
}

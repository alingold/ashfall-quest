using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Ashfall.Integration.Playable.Rendering
{
    public struct R23FilmicSession
    {
        public readonly bool SourceActive, InputQualified;
        public readonly float Exposure, White;
        public readonly R23FilmicInputDomain InputDomain;
        public readonly Camera TargetCamera;
        public R23FilmicSession(bool active, bool qualified, float exposure, float white, R23FilmicInputDomain domain, Camera camera)
        { SourceActive = active; InputQualified = qualified; Exposure = exposure; White = white; InputDomain = domain; TargetCamera = camera; }
    }

    /// <summary>Opt-in source environment/session switch. No project/pipeline settings change implicitly.</summary>
    public static class R23GodotFilmic
    {
        static R23FilmicSession state = new R23FilmicSession(false, false, 1, 1, R23FilmicInputDomain.LinearSceneReferred, null);
        public static R23FilmicSession Session { get { return state; } }
        public static string LastStatus { get; internal set; }
        // Counts render-graph recordings only, never asserted GPU/XR delivery.
        public static int RecordedPasses { get; internal set; }

        public static bool SetSourceEnvironment(bool active, float exposure = 1, float white = 1)
        {
            if (!R23GodotFilmicMath.ValidParameters(exposure, white)) return false;
            state = new R23FilmicSession(active, state.InputQualified, exposure, white, state.InputDomain, state.TargetCamera);
            LastStatus = active ? "Waiting for qualified HDR camera" : "Source environment disabled";
            return true;
        }
        /// <summary>Root asserts scene-referred HDR and a standard SDR final-output path. Gamma lighting is not qualified by choosing an encoding.</summary>
        public static void QualifyInput(bool qualified, R23FilmicInputDomain domain = R23FilmicInputDomain.LinearSceneReferred)
        {
            if (domain != R23FilmicInputDomain.LinearSceneReferred && domain != R23FilmicInputDomain.SrgbEncodedSceneReferred) throw new ArgumentOutOfRangeException("domain");
            state = new R23FilmicSession(state.SourceActive, qualified, state.Exposure, state.White, domain, state.TargetCamera);
            if (!qualified) LastStatus = "Input domain not qualified";
        }
        // Null means final Game/VR cameras; an explicit camera excludes probes/other game cameras.
        public static void SetCamera(Camera camera)
        { state = new R23FilmicSession(state.SourceActive, state.InputQualified, state.Exposure, state.White, state.InputDomain, camera); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession()
        {
            state = new R23FilmicSession(false, false, 1, 1, R23FilmicInputDomain.LinearSceneReferred, null);
            RecordedPasses = 0; LastStatus = "Source environment disabled";
        }
        /// <summary>Adds/reuses the feature. Editor caller owns AddObjectToAsset/persistence of the returned feature.</summary>
        public static R23GodotFilmicFeature Install(ScriptableRendererData rendererData)
        {
            if (rendererData == null) throw new ArgumentNullException("rendererData");
            foreach (var entry in rendererData.rendererFeatures)
            {
                var current = entry as R23GodotFilmicFeature;
                if (current != null) { current.SetActive(true); return current; }
            }
            var feature = ScriptableObject.CreateInstance<R23GodotFilmicFeature>();
            feature.name = "Godot source FILMIC (opt-in HDR)";
            rendererData.rendererFeatures.Add(feature); feature.Create(); rendererData.SetDirty();
            return feature;
        }
        /// <summary>Explicit small setup seam. This does not change ColorSpace or qualify scene radiance.</summary>
        public static void PrepareHdr(UniversalRenderPipelineAsset pipeline, Camera camera)
        {
            if (pipeline == null || camera == null) throw new ArgumentNullException("pipeline/camera");
            pipeline.supportsHDR = true;
            pipeline.hdrColorBufferPrecision = HDRColorBufferPrecision._64Bits;
            camera.allowHDR = true;
        }
        /// <summary>Qualification is explicit: HDR storage alone cannot prove physically linear scene radiance.</summary>
        public static bool QualifyPipeline(UniversalRenderPipelineAsset pipeline, Camera camera, bool producerIsSceneReferred,
            R23FilmicInputDomain domain = R23FilmicInputDomain.LinearSceneReferred)
        {
            QualifyInput(false, domain);
            if (pipeline == null || camera == null || !pipeline.supportsHDR || !camera.allowHDR)
            { LastStatus = "Pipeline and camera HDR must both be enabled"; return false; }
            if (!producerIsSceneReferred)
            { LastStatus = "HDR buffer producer has not been verified scene-referred; Gamma lighting is not repaired here"; return false; }
            QualifyInput(true, domain); LastStatus = "Input producer qualified; actual graph HDR format still checked per camera";
            return true;
        }
    }
}

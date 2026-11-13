#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.Appearance;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ashfall.Editor.Playable
{
    public static partial class R24Feedback6237Smoke
    {
        [Serializable] sealed class NonmetalV7Rows { public int version=0; public NonmetalV7Row[] entries=null; }
        [Serializable] sealed class NonmetalV7Row
        {
            public string styleId="";
            public int materialIndex=0;
            public bool publishedNonmetal=false, rawMapFactors=false;
            public float roughnessFloor=0, dielectricScale=1;
        }
        static readonly string[] NonmetalV7Targets = {
            "camo", "pban", "pj", "roy", "hattie", "warthog", "jack_o", "broodmother",
            "the_pilgrim", "bone_collector", "tormentus", "ace"
        };
        static bool V7Target(string style) { return Array.IndexOf(NonmetalV7Targets, style) >= 0; }
        static NonmetalV7Rows V7Rows()
        {
            var resource = Resources.Load<TextAsset>("R23AppearanceMaterialResponses");
            Need(resource != null, "Actual production response Resource");
            var rows = JsonUtility.FromJson<NonmetalV7Rows>(resource.text);
            Need(rows != null && rows.version == 1 && rows.entries != null && rows.entries.Length == 354,
                "Exact actual354 production rows required; no green fallback for old342");
            return rows;
        }
        static void V7Shader(Shader shader)
        {
            Need(shader != null, "Actual production shader loaded");
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
                Need(message.severity.ToString() != "Error", shader.name + ": " + message.message);
        }
        static void V7PairPixels(string baselinePath, string currentPath, bool protectedStyle, Action<string> note)
        {
            Texture2D baseline = null, current = null;
            try
            {
                baseline = new Texture2D(2, 2, TextureFormat.RGB24, false);
                current = new Texture2D(2, 2, TextureFormat.RGB24, false);
                Need(ImageConversion.LoadImage(baseline, File.ReadAllBytes(baselinePath)) &&
                     ImageConversion.LoadImage(current, File.ReadAllBytes(currentPath)), "Actual saved PNG decode");
                Need(baseline.width == 640 && baseline.height == 800 && current.width == 640 && current.height == 800,
                    "Actual PNG dimensions");
                var a = baseline.GetPixels32(); var b = current.GetPixels32();
                int changed = 0, minimum = 765, maximum = 0;
                for (int i = 0; i < a.Length; i++)
                {
                    if (a[i].r != b[i].r || a[i].g != b[i].g || a[i].b != b[i].b) changed++;
                    int sum = b[i].r + b[i].g + b[i].b; minimum = Math.Min(minimum, sum); maximum = Math.Max(maximum, sum);
                }
                Need(maximum - minimum > 20, "Actual output contains spatial color variation, not a uniform clear image");
                note("Actual saved GPU pair pixels changed=" + changed + " spatial RGB-sum range=" + minimum + ".." + maximum +
                     "; difference is not a visual improvement judgment. Protected pair=" + protectedStyle);
                if (protectedStyle) Need(changed == 0, "Protected current/current material control has no changed GPU pixels");
            }
            finally
            {
                if (baseline != null) UnityEngine.Object.Destroy(baseline);
                if (current != null) UnityEngine.Object.Destroy(current);
            }
        }
        static IEnumerator NonmetalV7Bindings(R23PlayableLoop loop, Action<string> note)
        {
            note("NONMETAL_V7_BINDINGS actual production JsonUtility/lookup fixture entered");
            var rows = V7Rows();
            var type = typeof(R23PlayerAppearance);
            var manifest = type.GetNestedType("Manifest", BindingFlags.NonPublic);
            var asset = type.GetNestedType("Asset", BindingFlags.NonPublic);
            var lookup = asset.GetMethod("SurfaceResponse", BindingFlags.Static | BindingFlags.NonPublic);
            Need(manifest != null && lookup != null, "Actual production manifest and material lookup");
            var profiles = new Dictionary<string, object>(StringComparer.Ordinal);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var named = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows.entries)
            {
                object profile;
                if (!profiles.TryGetValue(row.styleId, out profile))
                {
                    profile = JsonUtility.FromJson(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,
                        "PlayableR23/Appearance/" + row.styleId + ".json")), manifest);
                    profiles.Add(row.styleId, profile);
                }
                Need(keys.Add(row.styleId + "/" + row.materialIndex), "Unique material identity");
                var selected = lookup.Invoke(null, new[] { profile, (object)row.materialIndex });
                Need(selected != null, "Actual source-bound rule " + row.styleId + "/" + row.materialIndex);
                Need((bool)Read(selected, "publishedNonmetal") == row.publishedNonmetal,
                    "Runtime bool matches serialized table " + row.styleId);
                if (row.publishedNonmetal)
                {
                    Need(V7Target(row.styleId) && row.materialIndex == 0 && named.Add(row.styleId),
                        "Opt-in is exactly one body atlas per declared outfit");
                    Need(Mathf.Abs(row.roughnessFloor - (row.styleId == "pban" ? .92f : .90f)) < .000001f &&
                         Mathf.Abs(row.dielectricScale - (row.styleId == "pban" ? .15f : .20f)) < .000001f,
                         "Actual intended dry response " + row.styleId);
                    Need(row.rawMapFactors == (row.styleId == "camo"), "Raw versus baked domain " + row.styleId);
                }
            }
            Need(named.Count == 12, "All twelve declared named atlases opted in");
            var shader = Resources.Load<Shader>("R23AppearanceDoubleSided");
            var specular = Resources.Load<Shader>("R23AppearanceSpecular");
            var variants = new ShaderVariantCollection();
            try
            {
                foreach (var keywords in new[] {
                    new string[0], new[] { "_SPECULAR_SETUP" },
                    new[] { "_METALLICSPECGLOSSMAP", "_SPECULAR_SETUP" },
                    new[] { "_METALLICSPECGLOSSMAP", "_NORMALMAP", "_SPECULAR_SETUP" },
                    new[] { "_EMISSION", "_METALLICSPECGLOSSMAP", "_NORMALMAP", "_OCCLUSIONMAP", "_SPECULAR_SETUP" }
                }) variants.Add(new ShaderVariantCollection.ShaderVariant(shader, PassType.ScriptableRenderPipeline, keywords));
                variants.Add(new ShaderVariantCollection.ShaderVariant(specular, PassType.ScriptableRenderPipeline, new string[0]));
                variants.WarmUp();
            }
            finally { UnityEngine.Object.Destroy(variants); }
            V7Shader(shader); V7Shader(specular);
            note("Actual354 source bindings/12 named opts/factor domains; both production shaders and six declared variants checked. No all354 render or Android/HMD claim.");
            yield break;
        }
        static IEnumerator NonmetalV7Preview(R23PlayableLoop loop, string style, Action<string> note)
        {
            Need(V7Target(style) || Array.IndexOf(new[] { "obsidian", "eclipse_ronin", "argon", "polymer_gold" }, style) >= 0,
                "Explicit finite style only");
            note("NONMETAL_V7_PREVIEW actual production preview entered " + style);
            loop.ReturnToLobby();
            string originalStyle = loop.Preview.SelectedStyleId;
            float rotation = loop.Preview.RotationMultiplier;
            var originals = new Dictionary<MeshRenderer, Material>();
            var controls = new List<Material>();
            var controlsByOriginal = new Dictionary<Material, Material>();
            var layers = new Dictionary<GameObject, int>();
            Camera camera = null;
            try
            {
                loop.Preview.RotationMultiplier = 0;
                Need(loop.Preview.Show(style), "Actual preview request " + style);
                float end = Time.realtimeSinceStartup + 25;
                while ((loop.Preview.Loading || loop.Preview.SelectedStyleId != style) &&
                       loop.Preview.Failure == null && Time.realtimeSinceStartup < end) yield return null;
                Need(loop.Preview.Ready && !loop.Preview.Loading && loop.Preview.SelectedStyleId == style,
                    "Actual committed preview " + style + ": " + loop.Preview.Failure);
                yield return null;
                var appearance = (R23PlayerAppearance)Read(loop.Preview, "appearance");
                var avatar = (Transform)Read(loop.Preview, "avatar");
                var actualAsset = Read(appearance, "current");
                var materials = (Material[])Read(actualAsset, "Materials");
                var atlas = materials[0];
                bool targeted = V7Target(style);
                if (targeted)
                {
                    string property = atlas.shader.name == "Ashfall/R23AppearanceSpecular" ? "_AppearanceResponse" : "_SpecColor";
                    var bound = atlas.GetVector(property);
                    Need(bound.y < 0 && Mathf.Abs(bound.x - (style == "pban" ? .92f : .90f)) < .000001f &&
                         Mathf.Abs(bound.y + (style == "pban" ? .15f : .20f)) < .000001f,
                         "Actual committed shader sign-tag and recipe " + style);
                    if (property == "_SpecColor")
                        Need(atlas.IsKeywordEnabled("_SPECULAR_SETUP") && bound.w == (style == "camo" ? 1 : 0),
                            "Actual keyword and raw/baked map factors " + style);
                }
                foreach (var renderer in appearance.CurrentVisualRoot.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var current = renderer.sharedMaterial;
                    Need(current != null, "Actual body material");
                    var slot = Regex.Match(current.name, @" material (\d+)");
                    Need(slot.Success, "Exact source slot label " + current.name);
                    int index = Int32.Parse(slot.Groups[1].Value);
                    Material control;
                    if (!controlsByOriginal.TryGetValue(current, out control))
                    {
                        control = new Material(current); controlsByOriginal.Add(current, control); controls.Add(control);
                    }
                    originals.Add(renderer, current);
                    if (targeted && index == 0)
                    {
                        string property = current.shader.name == "Ashfall/R23AppearanceSpecular" ? "_AppearanceResponse" : "_SpecColor";
                        var baseline = current.GetVector(property); baseline.x = .84f; baseline.y = .35f;
                        control.SetVector(property, baseline);
                    }
                    foreach (string property in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap", "_EmissionMap" })
                        if (current.HasProperty(property)) Need(ReferenceEquals(current.GetTexture(property), control.GetTexture(property)),
                            "Identical control/current source texture " + style + "/" + index + "/" + property);
                    V7Shader(current.shader);
                }
                Need(originals.Count > 0 && controls.Count == controlsByOriginal.Count, "Actual renderer/material alias groups retained");
                note("Actual renderer count=" + originals.Count + " original/control shared material groups=" + controlsByOriginal.Count +
                    "; clone grouping correction is a fixture hypothesis, not proof of the previous272pixel cause.");
                foreach (var root in new[] { avatar, appearance.CurrentVisualRoot })
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    { if (!layers.ContainsKey(t.gameObject)) layers[t.gameObject] = t.gameObject.layer; t.gameObject.layer = 30; }
                camera = new GameObject("Owned nonmetal-v7 comparison camera").AddComponent<Camera>();
                camera.enabled = false; camera.GetUniversalAdditionalCameraData().allowXRRendering = false;
                camera.fieldOfView = 40; camera.aspect = .8f; camera.cullingMask = 1 << 30;
                camera.nearClipPlane = .03f; camera.farClipPlane = 20; camera.allowHDR = false;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f, .045f, .055f, 1);
                var target = avatar.position + Vector3.up * .92f;
                var eye = avatar.position + avatar.forward * 2.8f + avatar.right * .85f + Vector3.up * 1.15f;
                camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye, Vector3.up));
                string output = Path.Combine(Path.GetDirectoryName(R23PlayableBuild.Argument("-ashfallVRSmoke")), "nonmetal-v7");
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output, style + "-bindings.txt"), appearance.InspectMaterialBindings());
                for (int mode = 0; mode < 2; mode++)
                {
                    foreach (var pair in originals) pair.Key.sharedMaterial = mode == 0 ? controlsByOriginal[pair.Value] : pair.Value;
                    string path = Path.Combine(output, style + (mode == 0 ? "-48e-policy-control.png" : "-v7-current.png"));
                    Capture(camera, path);
                    foreach (var pair in originals) V7Shader(pair.Key.sharedMaterial.shader);
                    note("UNMATCHED Unity-only image=" + path + " eye=" + eye.ToString("F6") + " target=" + target.ToString("F6") +
                         " FOV40 640x800; same current wrapper, geometry, texture objects and lighting. Control is48e positive response, not installedc1 shader reconstruction.");
                }
                V7PairPixels(Path.Combine(output, style + "-48e-policy-control.png"),
                    Path.Combine(output, style + "-v7-current.png"), !targeted, note);
                note(style + ": actual material bindings and two renders completed. Personal image inspection and headset judgment still required; protected eyes/mixed atlas semantics not inferred from binding success.");
            }
            finally
            {
                Restore(originals, controls, layers);
                if (camera != null) UnityEngine.Object.Destroy(camera.gameObject);
                loop.Preview.RotationMultiplier = rotation;
                if (!String.IsNullOrEmpty(originalStyle)) loop.Preview.Show(originalStyle);
            }
        }
    }
}
#endif

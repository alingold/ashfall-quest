#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ashfall.Integration.DesktopPlaytest.Arena
{
    // Synthetic, metre-scale desktop course. No source map or production authority.
    public sealed class DesktopArena : MonoBehaviour, IDesktopPlaytestModule, IDisposable
    {
        private DesktopPlaytestContext context;
        private GameObject stage;
        private readonly List<Collider> targets = new List<Collider>();
        private readonly List<Material> materials = new List<Material>();

        public void Initialize(DesktopPlaytestContext value)
        {
            if (value == null) throw new ArgumentNullException("value");
            if (context != null) throw new InvalidOperationException("DesktopArena is already initialized.");
            if (!isActiveAndEnabled) throw new InvalidOperationException("DesktopArena requires an active enabled owner.");
            context = value;
            try
            {
                Shader shader = ResolveShader();
                Material floor = MakeMaterial(shader, "Floor", new Color(.14f, .19f, .23f));
                Material wall = MakeMaterial(shader, "Walls", new Color(.30f, .37f, .43f));
                Material course = MakeMaterial(shader, "Movement", new Color(.12f, .58f, .70f));
                Material target = MakeMaterial(shader, "Targets", new Color(1f, .38f, .08f));
                Material guide = MakeMaterial(shader, "Direction", new Color(.85f, .95f, .38f));
                stage = new GameObject("DesktopArena - SYNTHETIC LOCAL COURSE");
                // World-anchored even if the owner is moved/scaled by a local reset.
                stage.transform.position = Vector3.zero;
                stage.transform.rotation = Quaternion.identity;
                stage.transform.localScale = Vector3.one;
                Box("Ground", new Vector3(0, -.25f, 3), new Vector3(24, .5f, 28), floor);
                Box("West boundary", new Vector3(-12.25f, 1.5f, 3), new Vector3(.5f, 3, 29), wall);
                Box("East boundary", new Vector3(12.25f, 1.5f, 3), new Vector3(.5f, 3, 29), wall);
                Box("Rear boundary", new Vector3(0, 1.5f, -11.25f), new Vector3(24, 3, .5f), wall);
                Box("Backstop", new Vector3(0, 2, 17.25f), new Vector3(24, 4, .5f), wall);
                // Left lane: climbable steps and a gentle real inclined box collider.
                for (int i = 0; i < 3; ++i)
                    Box("Step " + (i + 1), new Vector3(-8, .15f * (i + 1), -4 + i), new Vector3(2.8f, .3f * (i + 1), 1), course);
                GameObject ramp = Box("Ramp 18 degrees", new Vector3(-8, .64f, 2), new Vector3(2.8f, .25f, 4), course);
                ramp.transform.rotation = Quaternion.Euler(-18, 0, 0);
                Box("Ramp landing", new Vector3(-8, .55f, 4.4f), new Vector3(2.8f, 1.1f, 1.2f), course);
                // Right lane: jump bar and a roof with 2.15m standing clearance.
                Box("Jump bar", new Vector3(8, .30f, -3), new Vector3(2.8f, .6f, .55f), course);
                Box("Ceiling", new Vector3(8, 2.4f, 2), new Vector3(3.2f, .5f, 4), course);
                Box("Ceiling west support", new Vector3(6.5f, 1.075f, 2), new Vector3(.2f, 2.15f, 4), wall);
                Box("Ceiling east support", new Vector3(9.5f, 1.075f, 2), new Vector3(.2f, 2.15f, 4), wall);
                // Flat, collider-free direction marks keep the central firing lane open.
                for (int i = 0; i < 5; ++i)
                {
                    GameObject mark = Box("Forward marker " + i, new Vector3(0, .012f, -6 + i * 2), new Vector3(.18f, .02f, .8f), guide);
                    mark.GetComponent<Collider>().enabled = false;
                }
                for (int i = 0; i < 5; ++i)
                {
                    float x = (i - 2) * 3;
                    float z = 12 - Mathf.Abs(i - 2) * 2;
                    GameObject body = Box("Desktop target " + (i + 1), new Vector3(x, 1.1f, z), new Vector3(1, 2.2f, .45f), target);
                    body.layer = 9; // Root-reserved WorldInteraction; ordinary colliders, no triggers.
                    targets.Add(body.GetComponent<Collider>());
                }
                GameObject sun = new GameObject("Arena key light");
                sun.transform.SetParent(stage.transform, false);
                sun.transform.rotation = Quaternion.Euler(48, -32, 0);
                Light light = sun.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1, .96f, .88f);
                light.intensity = 1.2f;
                light.shadows = LightShadows.Soft;
                context.Targets.AddRange(targets);
                context.Spawn = new Vector3(0, 1.1f, -8);
                // No reset subscription: target identity and world transforms remain stable.
            }
            catch { Dispose(); throw; }
        }

        private static Shader ResolveShader()
        {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            string shaderName;
            if (pipeline == null) shaderName = "Standard";
            else if (pipeline.GetType().FullName == "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset")
                shaderName = "Universal Render Pipeline/Lit";
            else throw new NotSupportedException("DesktopArena supports Builtin or URP only: " + pipeline.GetType().FullName);
            Shader shader = Shader.Find(shaderName);
            if (shader == null || !shader.isSupported)
                throw new InvalidOperationException("DesktopArena requires supported shader: " + shaderName + ". Root must include it in the build.");
            return shader;
        }

        private Material MakeMaterial(Shader shader, string label, Color color)
        {
            Material material = new Material(shader);
            materials.Add(material);
            material.name = "DesktopArena/" + label;
            material.color = color;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .2f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", .2f);
            return material;
        }

        private GameObject Box(string label, Vector3 position, Vector3 size, Material material)
        {
            GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.transform.SetParent(stage.transform, false);
            item.name = label;
            item.layer = 8; // Root-reserved WorldStructure.
            item.transform.position = position;
            item.transform.localScale = size;
            item.GetComponent<Renderer>().sharedMaterial = material;
            return item;
        }

        public void Dispose()
        {
            if (context != null)
                foreach (Collider target in targets) context.Targets.RemoveAll(candidate=>System.Object.ReferenceEquals(candidate,target));
            targets.Clear();
            context = null;
            if (stage != null) { stage.SetActive(false); Release(stage); stage = null; }
            foreach (Material material in materials) Release(material);
            materials.Clear();
        }

        private static void Release(UnityEngine.Object resource)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(resource);
            else UnityEngine.Object.DestroyImmediate(resource);
        }
        private void OnDisable() { Dispose(); }
        private void OnDestroy() { Dispose(); }
    }
}
#endif

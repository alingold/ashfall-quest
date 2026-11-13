#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ashfall.Integration.DesktopPlaytest;
using Ashfall.Integration.DesktopPlaytest.Arena;

namespace Ashfall.Tools.Integration.DesktopPlaytest
{
    // Root invokes this coroutine in an otherwise empty PlayMode scene, with no XR.
    public static class DesktopArenaNativeFixture
    {
        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("DesktopArena fixture: " + message);
        }

        public static IEnumerator Run(Action<string> report)
        {
            Require(Application.isPlaying, "requires real PlayMode for destruction/physics barriers");
            GameObject owner = new GameObject("Arena fixture owner");
            GameObject foreign = new GameObject("Unrelated resource sentinel");
            foreign.transform.position = new Vector3(100, 100, 100);
            Collider sentinel = foreign.AddComponent<BoxCollider>();
            DesktopPlaytestContext context = new DesktopPlaytestContext();
            context.Targets.Add(sentinel);
            DesktopArena arena = owner.AddComponent<DesktopArena>();
            try
            {
                arena.Initialize(context);
                Physics.SyncTransforms();
                Require(context.Targets.Count == 6, "five new targets preserve foreign registry member");
                List<Collider> targets = new List<Collider>(context.Targets);
                targets.Remove(sentinel);
                Require(new HashSet<Collider>(targets).Count == 5, "five distinct target identities");
                GameObject stage = targets[0].transform.parent.gameObject;
                Renderer[] renderers = stage.GetComponentsInChildren<Renderer>();
                HashSet<Material> materials = new HashSet<Material>();
                foreach (Renderer renderer in renderers)
                {
                    Material material = renderer.sharedMaterial;
                    Require(material != null && material.shader != null && material.shader.isSupported, "supported explicit material");
                    Require(material.shader.name != "Hidden/InternalErrorShader", "no pink error shader");
                    materials.Add(material);
                }
                Require(materials.Count == 5, "bounded five owned materials");
                Require(stage.GetComponentsInChildren<Light>().Length == 1, "one owned light");
                Vector3 spawn = context.Spawn;
                // Covers a 1.8m centred capsule, plus a feet-root capsule shifted up .9m.
                Require(!Physics.CheckCapsule(spawn + Vector3.down * .5f, spawn + Vector3.up * 1.4f,
                    .4f, ~0, QueryTriggerInteraction.Ignore), "spawn capsule has floor/ceiling/side clearance");
                RaycastHit hit;
                Require(Physics.Raycast(spawn, Vector3.down, out hit, 3, ~0, QueryTriggerInteraction.Ignore), "spawn has floor");
                Require(hit.collider.transform.parent == stage.transform && hit.collider.name == "Ground", "spawn floor is arena ground");
                Require(Mathf.Abs(hit.point.y) < .001f && hit.normal.y > .99f, "floor top at zero faces up");
                Vector3[] positions = new Vector3[targets.Count];
                for (int i = 0; i < targets.Count; ++i)
                {
                    Collider target = targets[i];
                    positions[i] = target.transform.position;
                    Require(target.enabled && !target.isTrigger && target.attachedRigidbody == null, "static physical target");
                    Vector3 eye = spawn + Vector3.up * .6f;
                    Vector3 ray = target.bounds.center - eye;
                    Require(Physics.Raycast(eye, ray.normalized, out hit, ray.magnitude + 1, ~0, QueryTriggerInteraction.Ignore)
                        && hit.collider == target, "target visible from spawn: " + target.name);
                    Require(Physics.Raycast(target.bounds.center + Vector3.up * 3, Vector3.down, out hit, 5,
                        ~0, QueryTriggerInteraction.Ignore) && hit.collider == target, "target ray identity");
                }
                // Real shape queries verify the movement course, independently of target registration.
                Require(Physics.Raycast(new Vector3(-8, 4, -4), Vector3.down, out hit, 6)
                    && hit.collider.name == "Step 1" && Mathf.Abs(hit.point.y - .3f) < .01f, "first step height");
                Require(Physics.Raycast(new Vector3(-8, 4, 2), Vector3.down, out hit, 6)
                    && hit.collider.name == "Ramp 18 degrees" && hit.normal.y > .9f && hit.normal.y < .99f, "inclined ramp surface");
                Require(Physics.Raycast(new Vector3(8, 4, -3), Vector3.down, out hit, 6)
                    && hit.collider.name == "Jump bar", "jump obstacle");
                Require(Physics.Raycast(new Vector3(8, 1, 2), Vector3.up, out hit, 3)
                    && hit.collider.name == "Ceiling" && Mathf.Abs(hit.point.y - 2.15f) < .01f, "ceiling underside clearance");
                Vector3[] walls = { Vector3.left, Vector3.right, Vector3.back, Vector3.forward };
                foreach (Vector3 direction in walls)
                    Require(Physics.Raycast(new Vector3(0, 2.8f, 0), direction, out hit, 30)
                        && hit.collider.transform.parent == stage.transform && hit.collider.gameObject.layer == 8, "bounded walls");
                context.RequestReset();
                owner.transform.position = new Vector3(40, 0, 0);
                owner.transform.localScale = Vector3.one * 2;
                Physics.SyncTransforms();
                for (int i = 0; i < targets.Count; ++i)
                    Require(context.Targets.Contains(targets[i]) && targets[i].transform.position == positions[i], "reset preserves target world identity");
                bool duplicateRejected = false;
                try { arena.Initialize(context); }
                catch (InvalidOperationException) { duplicateRejected = true; }
                Require(duplicateRejected && context.Targets.Count == 6, "duplicate initialization rejects without mutation");
                arena.enabled = false;
                arena.Dispose();
                Require(!stage.activeSelf && context.Targets.Count == 1 && context.Targets[0] == sentinel,
                    "disable withdraws exactly owned colliders before deferred destruction");
                Physics.SyncTransforms();
                Require(!Physics.Raycast(spawn, Vector3.down, out hit, 3), "disabled stage leaves no queryable floor");
                yield return null;
                Require(stage == null, "stage destroyed after frame barrier");
                foreach (Material material in materials) Require(material == null, "owned material destroyed");
                Require(foreign != null && sentinel != null, "foreign resource survives cleanup");
                arena.enabled = true;
                arena.Initialize(context);
                Require(context.Targets.Count == 6, "fresh reinitialize after cleanup");
                GameObject secondStage = context.Targets[1].transform.parent.gameObject;
                UnityEngine.Object.Destroy(owner);
                yield return null;
                yield return null;
                Require(secondStage == null && context.Targets.Count == 1, "owner destruction cleans unparented stage and registry");
                if (report != null) report("DesktopArena native fixture PASS: spawn/floor/5 targets/course/reset/materials/disable/reinitialize/destroy");
            }
            finally
            {
                if (arena != null) arena.Dispose();
                if (owner != null) UnityEngine.Object.Destroy(owner);
                UnityEngine.Object.Destroy(foreign);
            }
        }
    }
}
#endif

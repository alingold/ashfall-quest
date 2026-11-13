#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using Ashfall.Integration.DesktopPlaytest;
using Ashfall.Integration.DesktopPlaytest.Hud;
using UnityEngine;

// Root invokes Run in PlayMode under engine_lane.py; no bootstrap or engine launch here.
public static class NativeHudFixture
{
    public static IEnumerator Run()
    {
        int[,] sizes = { {320,240}, {640,480}, {1280,720}, {1920,1080}, {3840,2160}, {800,1200} };
        for (int i = 0; i < sizes.GetLength(0); ++i)
        {
            float scale = DesktopHud.ScaleFor(sizes[i,0], sizes[i,1]);
            float w = sizes[i,0] / scale, h = sizes[i,1] / scale;
            Rect panel = DesktopHud.PanelFor(w,h);
            Check(panel.x >= 0 && panel.y >= 0 && panel.xMax <= w && panel.yMax <= h, "panel bounds");
            Check(panel.width > 200 && panel.height > 200, "usable panel");
        }
        var root = new GameObject("DesktopHud fixture ONLY");
        var pawn = new GameObject("Hud passive pawn sentinel");
        var target = new GameObject("Hud passive target sentinel");
        CursorLockMode cursor = Cursor.lockState;
        bool visible = Cursor.visible;
        try
        {
            var context = new DesktopPlaytestContext();
            context.PlayerRoot = pawn.transform;
            context.Targets.Add(target.AddComponent<BoxCollider>());
            context.MovementStatus = "Movement fixture";
            context.CombatStatus = "Disabled: fixture";
            context.ConstructionStatus = "Disabled: no construction owner";
            context.Hits = 3;
            context.RemainingTargets = 7;
            int resets = 0;
            context.ResetRequested += delegate { ++resets; };
            DesktopHud hud = root.AddComponent<DesktopHud>();
            hud.Initialize(context);
            Check(DesktopHud.StatusText(context, CursorLockMode.None, true).Contains("Disabled: fixture"), "actual disabled status");
            context.CombatStatus = "Ammo 4 / reserve 9";
            Check(DesktopHud.StatusText(context, CursorLockMode.None, true).Contains("Ammo 4 / reserve 9"), "live ammo status");
            for (int state = 0; state < 2; ++state)
            {
                context.Paused = state == 1;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                // Real frame passivity; interactive Game view is required to qualify OnGUI rendering.
                yield return null;
                yield return null;
                Check(context.Paused == (state == 1), "pause passivity");
                Check(Cursor.lockState == CursorLockMode.None && Cursor.visible, "cursor passivity");
                Check(context.Hits == 3 && context.RemainingTargets == 7 && resets == 0, "gameplay passivity");
                Check(context.PlayerRoot == pawn.transform && pawn.transform.position == Vector3.zero, "pawn passivity");
                Check(context.Targets.Count == 1 && context.Targets[0] == target.GetComponent<BoxCollider>() &&
                    target.transform.position == Vector3.zero, "target passivity");
                Check(context.CombatStatus == "Ammo 4 / reserve 9", "status passivity");
            }
            hud.enabled = false;
            yield return null;
            hud.enabled = true;
            yield return null;
            Check(context.Paused && resets == 0, "enable lifecycle passivity");
            Debug.Log("DesktopHud native layout/passivity PASS; visual legibility and actual button click/quit acceptance remain separate");
        }
        finally
        {
            UnityEngine.Object.Destroy(root);
            UnityEngine.Object.Destroy(pawn);
            UnityEngine.Object.Destroy(target);
            Cursor.lockState = cursor;
            Cursor.visible = visible;
        }
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("DesktopHud: " + name);
    }
}
#endif

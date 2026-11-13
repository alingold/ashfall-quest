# Human character/map UI fixes

The character arrows changed only the caption/index. Root's frame loop always called `Preview.Show(Appearance.RequestedStyleId ?? "camo")`, so it never requested the browsed model and would cancel a separate preview request back to the worn style. UI now publishes `CurrentViewedAppearanceId` and `AppearanceViewed(id)` (null outside the character page); root has changed its frame call to:

```csharp
Preview.Show(UI.CurrentViewedAppearanceId ?? Appearance.RequestedStyleId ?? "camo");
```

Both arrows use the production `UI.CycleAppearance(-1/+1)` path. Browse and actual `AppearanceSelected` remain separate: browsing previews a style, USE CHARACTER equips it. Option refresh preserves the viewed ID. Player loading status no longer rebuilds/rearms the independent browsed menu, and the unnecessary bottom SELECT YOUR APPEARANCE text is removed.

`R23PlayerAppearance` now checks the request serial explicitly before committing a freshly built asset and publishes `StyleChanged(id)` after actual commit. Existing old-current-until-ready and owned cleanup remain. `Preview.RequestedStyleId` and `Preview.InspectPresentation()` expose actual committed profile ID, mesh/material identities and visible renderers/head; they do not infer rendered style from the caption.

Map BACK now follows the actual final `front_menu1000.gd:234` override: (40,720), size (170,58), below the lower row. Canonical Valley/Range/Cyber Bird thumbnail bindings use the already-published unchanged source PNG resources. Root extended its existing RGBA32 importer fix to R23MapThumbnails; the sparse thumbnail metadata had the same risk as the previously verified Alpha8 icon imports. This task did not perform a native thumbnail import. Current Godot UI font, font metrics, 1280x800 source board coordinates and calibration/control logic are retained. Only the requested default wording changed to HOLD STILL TO NORMALIZE HEIGHT.

## Callable native preview-change check

In the coordinator's next reserved engine lane, run on the live root:

```csharp
loop.StartCoroutine(loop.UI.CheckAppearancePreviewChange(loop.Preview, message => Debug.Log(message), 30));
```

The coroutine opens the actual character page, cycles RIGHT then LEFT through the real arrow path, and waits for the host's normal frame binding. It deliberately never calls `Preview.Show` itself, so the old broken root binding fails the check. Each step requires a committed matching profile, visible body/head and distinct mesh/material identities. It restores the original page/index, does not equip the player's style, and performs no capture/teleport. Timeouts report requested/loaded IDs and identify the missing root binding. Native rendering/controller appearance still requires coordinator observation.

CPU checks:

```powershell
python unity/Tools/PlayableR23/UI/HumanFix/check.py
```

This validates actual source BACK coordinates, all 8 payload bindings, differing first-arrow mesh bytes, unchanged real map PNG bytes/RGB, preserved R23GodotUI font, current root/editor handoffs and installed Unity API compilation. Existing defaults, loadout, settings, map-flow and menu-audio CPU checks also pass. No engine/GPU/device/native coroutine was run here; runnable222943 remains unchanged.

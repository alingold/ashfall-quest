Implemented original local B/Y mark wheel and source mark rendering in `Input/R23TeamMarks*.cs`. No Root, Rig, other input, existing UI or installed APK changes. CPU only; no Unity/Godot execution, render, controller delivery or listening claim.

Root integration:

```csharp
TeamMarks.Initialize(Rig.Head, Rig.transform, Hands.transform /* optional */);
TeamMarks.SetSfxVolume(UI.Settings.SfxVolume); // Master already AudioListener
TeamMarks.Sample(dt, rawDominantSecondaryHeld, rawDominantStick,
                 correctedDominantAimRay, eligible, locomotionAnchor);
TeamMarks.Tick(Time.unscaledDeltaTime, tacticalMapOpen);
// Support B/Y, only on the existing neutral-rearmed eligible press edge:
TeamMarks.TryPlace(correctedSupportAimRay);
```

`eligible`: input enabled, focused/tracked primary hand, alive, not paused/complete, no owned-weapon wheel/backpack/field menu. Call `Sample(...,eligible:false,...)` or `CancelInput()` on invalidation. This cancels a held gesture and demands neutral before accepting another. Existing marks persist and age through pause/focus loss, as source does. Always call `Tick` with unscaled seconds. `ClearAll()` for map/reset/death; component disable also clears. Do not leave old `WorldPing.TryPlace` wired alongside this support shortcut: original `world_ping.gd:50–55` delegates to team marks when available.

Read-only seams: `InputHeld`, `WheelOpen`, `SelectedIndex`, `PreferredIndex`, immutable `Current` receipt, `Remaining`, `MarkVisible`, `VisibleColumnDots`, `IconWorldWidth`, `IconWorldPosition`, `SourceAudioReady`, `AudioEvents`, `Ready`/`Failure`; `MarkCreated` and `WheelOpenChanged` events. `Sample` returns true only on successful mark release. `TryPlace` returns actual surface-query success.

Reserve the dominant stick for choice while B/Y is held: pass raw stick to marks and suppress turn/manual-eject separately; walking remains enabled. This is the requested integration usability fix, **not original player turn suppression**. Preserved original `player.gd:465–466` suppresses turn for `inventory_open` only; `get_turn_axis:814–816` suppresses the manual-eject diagonal only. Source marks do not set `inventory_open` or pause gameplay. Source rack `weapon_rack.gd:154` reserves B/Y for team marks, leaving A/X inventory switching.

Exact preserved source input is `C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source/`:

- `scripts/team_marks.gd:57–109`: neutral re-arm, >.1000001 s wheel, press-captured target, stick threshold>.25 / ray-delta fallback, preferred index and release. Reticle center does not erase the last choice or cancel a captured mark; the port preserves this source behavior.
- `scripts/team_marks.gd:124–166`: one current mark, 30 s wall-clock lifetime, normal offsets .004 m for indices3–5 and .035 m otherwise; full source icon/action/color order. Primary query source bits1|4|8 maps to Unity0/8/20/19; self actor/rack excluded. Support shortcut is world/construction-only.
- `scripts/ping_wheel_086.gd:1–15` + `weapon_wheel.gd:8–12,61–105,194–252,276+`: source8 sectors, .34/.102 m radii ×1.2, .85 m ray opening distance, fixed orientation/locomotion translation, exact divider/cancel/pointer/colors. Icon size64×.0015=.096 m before display scale, despite embedded RGBA raster resolution256.
- `scripts/ping_sprite_r19.gd:9–30`: final source override gives **all** icons full billboard, no depth test and 4° width (TARGET5°), anchored to the actual point. It overrides the initial surface-facing paint orientation; no fake near-camera relocation.
- `scripts/ping_column_r23.gd:10–44`: nonpaint source columns, skyY80, icon rise.5 m, dot width matching angular depth, start=.5+width×1.25, spacing=width×1.37, max512. Dots use source sphere radius.5/height.49, radial8/rings4 and ordinary world depth testing. One owned combined mesh slices its index range instead of making512 MonoBehaviours.
- `assets/ui/ping072_0..4,6..7.svg` + **`ping0993_5.svg`** (actual alternate smile, not obsolete gun silhouette); actual source font `R23GodotUI` retained. Original SVGs rasterized by existing CPU M/L/H/V/Q parser at4×, then RGBA256, embedded and explicitly decoded RGBA32. `OriginalIcons/` permits direct inspection. Quadratic sampling/antialiasing can differ slightly from Godot SVG import.
- `scripts/team_marks.gd:167–169`, `audio_bank.gd:36–53`: actual fallback `assets/audio/sfx/mark_ping0993_00.wav` (no reference0992 override), mono48000Hz/17280frames/.36s PCM16, −8 dB × persisted SFX. Uses one bounded 2D voice; rapidly replacing a mark restarts its short cue rather than accumulating voices. Listener Master is never multiplied twice.

Contextual weapon action8 is explicitly unavailable (`WeaponContextAvailable=false` / diagnostic status). The source requires an actual pointed dropped weapon, its real catalog icon/name and pickup index. No fake weapon identity or inventory grant is substituted. All eight ordinary mark actions work. No networking/remote transport or tactical-map draw integration is claimed; `tacticalMap=true` hides the real-world mark as source does.

Focused checks: `python unity/Tools/PlayableR23/TeamMarks/check.py` passes41 production state/math cases, all eight RGBA/PNG bindings, original PCM bytes and source hashes. `python unity/Tools/PlayableR23/check_integration.py` passes Runtime/EditorRuntime/BuildAndSmoke installed Unity API compilation. Native fixture authored, not run: yield `R23TeamMarksFixture.Run(head,actor,actualUnobstructedWorldRay,report,capture)` inside root's next admitted smoke. It uses a separate production instance and actual world ray; checks original resources, press/release target capture, visible wheel, single-current replacement, source offsets, target paint, tactical hiding, pause/neutral/TTL/reset; requests two captures. Pass an unobstructed source-world floor/wall ray, not a layer19/20 target, because support shortcut intentionally excludes those layers. No terminal route or engine launch was added in this ownership lane.

# Local source support-hand ping and HUD change

`R23VrUI` no longer creates or updates the loaded/reserve ammo faces. Ammo snapshots/APIs remain compatible for the gun owner. Health, shield, stamina, build balance/grid and compass retain their existing source layout/colors. The unused obsolete A-reload fallback hint was removed; no A-jump or B-settings instruction remains in this UI file. Root has separately updated its connection/control hint.

New runtime `Ashfall.Integration.Playable.Input.R23WorldPing`:

```csharp
ping.Initialize(rig.Head.transform, rig.transform);
ping.Tick(seconds, gameplayFocusedTrackedAliveAndUnpaused);
// In the already-neutral-rearmed support-hand PingPressed callback:
ping.TryPlace(new Ray(supportRayOrigin, supportRayDirection));
// On map/home/restart boundaries:
ping.ClearAll();
```

Call Tick with valid=true before the first accepted placement. TryPlace returns false on invalid input/miss, preserving any previous valid marker on a miss. Read-only diagnostics: Visible, Remaining, Position, SurfacePoint, SurfaceNormal; Pinged(point) emits the actual hit point. The component does not read input, grant items, deal damage, move tracking or send network messages. Root supplies the current corrected support ray; do not apply a second grip-to-ray correction.

Primary source: preserved `quest-r24/source/scripts/world_ping.gd` and `upright_ui.gd`. It has one local blue `◇`, color (.1,.48,1), dark outline (.005,.01,.025), source 36px/.001m em and 8px outline. World range is max(100000, camera.far*2); placement is actual hit point + normal*.035m; lifetime is 20 seconds, refreshed on replacement; distance scale is clamp(distance*.12,.7,6). Billboard copies camera facing/pitch and removes roll, with the source deterministic vertical-view fallback. It does not track the hit object after placement.

The existing Godot UI font resource does not contain U+25C7, and the current overlay text shader disables depth testing. This component therefore draws the hollow diamond with two tiny owned contour meshes using the already-retained URP Unlit shader. Geometry is depth-tested, has no colliders/shadows and uses no new texture/shader/font dependency. Exact system-fallback font raster/outline shape is not claimed. The source colors, em, outline width, range, offset, scale and lifetime are retained.

Raycasts use main-world Unity layer0 plus constructed-world layer8 (the current equivalents of source world mask1), exclude the actor hierarchy, triggers, shot-only19 and player-only20. Backface querying is scoped/restored, matching the source world ray's backface support. Focus loss, application pause, invalid Tick or component disable clears the ping, preventing stale reappearance; explicit ClearAll resets map lifetime. This deliberately applies the requested reset behavior rather than resuming a hidden pre-pause marker. Owned meshes/materials are released on destruction.

Optional source team marks are not ported. No network/team synchronization is claimed.

Check with `python unity/Tools/PlayableR23/WorldPing/check.py`: 22 production CPU lifetime/scale/layer/cleanup cases, source constants/HUD preservation and complete installed Runtime/EditorRuntime/BuildAndSmoke API compilation pass. No engine, build, GPU, native ray/occlusion/billboard render or device job was run. First Quest APK003359 and existing executables remain unchanged.

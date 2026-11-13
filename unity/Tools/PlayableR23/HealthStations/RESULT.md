# Original R23 health stations

Ready for root integration. New runtime only: `Integration/Playable/JumpPads/R23HealthStations.cs`, namespace `Ashfall.Integration.Playable.JumpPads`. Existing jump-pad classes/data and root files were not edited.

```csharp
var stations = gameObject.AddComponent<R23HealthStations>();
stations.Initialize(Map.MapId, Rig.transform, Map.transform,
    gain => Gameplay.ApplyPlayerGain(gain, 0));

// After each native map load/placement, including round/map resets:
stations.SetMap(Map.MapId);
// Once per accepted gameplay frame, with actual source feet:
stations.Step(actualFeet, dt, gameplayEnabled);
```

`Initialize` asynchronously reads `StreamingAssets/PlayableR23/HealthStations/stations.json` through UnityWebRequest, supporting Android packaged assets and desktop. `SetMap` before loading completes remembers the latest requested ID. `Ready`, `Loading`, `Failure`, `StationCount`, and an immutable-copy `AuthoredStations` array expose loaded data. Paintball/alpha/alfa aliases resolve to current catalog IDs. `LastRate`, `LastRequestedGain` and `LastApplicationAccepted` report this step only; there is no parallel health counter, accumulated entitlement, deferred gain or inventory mutation. Real health owner rejection is returned unchanged. `ResetContact()` clears these observations, and map switch/disable already clears them. Valid steps require enabled gameplay and finite `0 < dt <= .1`; paused, invalid and out-of-range frames never invoke the effect callback.

Data comes exclusively from the preserved original `C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source/assets/data/map_semantics_r14.json`, checked against original archive source hashes. It retains all 430 authored health rows across 105 archive entries (30 with stations), with source order, radius and 10 HP/s rates preserved. Paintball has 17 health stations, ALFA 98. Paintball still has zero jump pads; this work adds no pad or hex geometry. Valley and Range are absent from this archive health dataset, so SetMap returns false/Ready=false/zero stations there instead of inventing rows. Data is 159,620 bytes and retains original cache fingerprints/source hashes for provenance.

Centers/normals reflect Godot Z exactly once into native-map local coordinates, then use the current map root transform. Native map scale must be one so authored metre distances remain unchanged. The runtime ports original `contains_face`/`healing_rate_at`/`_clear_contact`: signed face height −.20..+.75, circular authored radius, clear-contact endpoints at station center+.20*normal and feet+.12*normal, original squared-distance <.01 short-contact shortcut, and maximum clear overlap rate rather than sum. Float32 Vector3 arithmetic followed by script double bounds is preserved, including rounding at the rear boundary.

For longer contact segments, forward and reverse read-only raycasts preserve source `hit_back_faces=true` without changing global Physics settings. Queries use native source-mask-1 roles: Unity layer 0 world geometry and layer 8 constructed cover; actor and its children/triggers are excluded. Bullet-only 19 and player-only 20 do not block this source-mask-1 contact. Source's near-contact shortcut remains exact. Each successful active evaluation invokes the real gain callback once with `rate * dt`; death/full-health/pause policy belongs to the gameplay health owner and host eligibility.

Validation:

- `python unity/Tools/PlayableR23/HealthStations/check.py`: 643 original-source/hash/payload checks and 903 C# math/contact/application cases pass, covering all 430 genuine station centers, rear/front/radius bounds, oriented normals, max overlap, denied contacts, pause/invalid time, actual HealthService gains, full-health rejection and no resurrection.
- `python unity/Tools/PlayableR23/check_integration.py`: installed Unity runtime, editor-runtime and editor/build-smoke API compilation passes.
- `python unity/Tools/PlayableR23/HealthStations/check_fixture.py`: staged `Staged/R23HealthStationsSmoke.cs` compiles against installed Unity APIs. Future coordinated native entrypoint is public `IEnumerator R23HealthStationsSmoke.Run()`. It uses actual Paintball/ALFA manifest rows plus an isolated native wall/back-facing triangle/constructed-cover/ballistic-only fixture and a genuine R23RoundState/HealthService gain callback. It does not prove the live root Gameplay binding.

No engine/build/device/Blender was run. Native collision fixture and real-map station positioning/healing remain unrun. Source station charge/network/visual extraction behavior and archive out-of-bounds/warmup eligibility are outside this requested position/rate provider; host gameplay eligibility remains authoritative. No station meshes or source hexes were changed.

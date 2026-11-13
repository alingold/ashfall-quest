# ALFA source breakables

Published coherent CPU implementation at 09:54 UTC. World/Map ownership is released. Existing Classic integration and CameraFarDistance edits are preserved.

Root binding:

```csharp
// Existing accepted carried-shot WorldHit event, while the round owns gameplay:
Map.TryDamageCollider(hitCollider, sourceBodyDamage);
// Restart/round reset (map reload already restores its newly loaded geometry):
Map.ResetDestructibles();
```

Inspection APIs: `DestructibleCount`, `BrokenDestructibleCount`, `DestructibleDefinitions`, and `TryReadDestructible(Collider,out string id,out bool broken)`. Definition rows expose original assembly ID, glass flag, map-local center/size, actual main-face probe and normal, and source health=1. For a native glass ray fixture, select a glass definition and transform its `probe`/`normal` through Map.transform; cast from probe+.12*normal towards probe−.12*normal, require nearest native collider membership through TryReadDestructible, damage it, then require withdrawal and reset restoration. The field `probe` lies on a genuine original triangle, not on an invented box.

Original `archive_panel_r8.gd` starts at health1 and `take_damage(_amount)` ignores magnitude: the first accepted hit retires the assembly. The new owner follows that first-hit behavior for finite positive firearm damage. All registered render chunks and colliders for that assembly withdraw immediately; repeated/stale hits are inert. Reset restores original chunks/colliders and clears retirement. There is no replacement box, static invisible window collider, parallel firearm counter, root displacement or charge effect.

ALFA extraction uses the immutable original GLB SHA **0f0a7c9ee9b89314d7b3797fffd8344e8b310469b01e06a069fc9ac153bd2994**, original material provenance and preserved r8/r9 component rules. It reconstructs original material/4-cell draw batches, 1mm welded components, source pane and door predicates, pane-front/back and frame/door assembly grouping, plus the original r9 vc aperture clipping planes. Clipped source trim is attached to its panel and removed from static geometry/collision; interpolated positions/normals/UVs/colors remain source-derived. Glass uses the original clear_glass_r10 tint (.28,.48,.56), alpha .07+edge*.14, roughness .16 and specular .55 through a stereo-capable URP glass pass. Source shader pixel/depth-prepass parity is not claimed without rendering.

Published result: **930 assemblies (730 glass,200 doors)**; **42,917 owned panel triangles** in a total **2,144 chunks**. Intact map render and collision surface areas are conserved per original material (largest observed relative difference about1.2e−9, from float clipping). The total triangle count grows only because source trim clipping triangulates fragments. Actual source textures, their existing metadata, spawn choices, bounds, and unrelated static material roles remain unchanged. Existing ALFA semantic marker/leaf collision exclusions are preserved. New ALFA files are map.json, geometry.bytes and destructibles-provenance.json; new runtime/shader files are under World/Destructibles. Existing map.json/geometry metadata GUIDs are preserved.

CPU probes after retiring panel geometry find clear static passage at **all730 glass and126 doors**. Remaining74 doors have original textured backing (34 s45,40 s14); source r9 only carves vc, so those original walls are preserved. No remaining vc or s48 blocker intersects any of the930 probe segments. `check-results.json` names those74 IDs. This slice therefore does not claim every recognized door opens a traversable path through all source scenery.

Checks:1,872 actual first-hit/reset state cases pass; source hash, original texture identity, exact membership, chunk/index/finite-data checks, per-material render/collision area conservation, all930 registered assemblies, and static opening probes pass. Installed Unity runtime/editor-runtime/editor build-smoke API compilation passes. No native engine, shader/GPU compiler/render, build, Blender, headset or device job was run. Native cooking, actual carried-gun impact→hook→withdrawal/reset and real visual performance remain unverified. Source break sounds and proximity/touch/melee/network triggers are outside this bullet slice.

Reproduce with `python unity/Tools/PlayableR23/World/Destructibles/prepare.py` then `python unity/Tools/PlayableR23/World/Destructibles/check.py`. Producer is read-only against original source and writes StagedPayload. It retains an exact original ALFA baseline under owned Tools/OriginalPayload, so re-running after publication cannot re-extract from already modified geometry. Regeneration is about59 seconds CPU, with no engine invocation. Original released builds/APKs are untouched.

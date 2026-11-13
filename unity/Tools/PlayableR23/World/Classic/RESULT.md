# Classic CPU payload

Ready for root import, staged outside Assets at `unity/Tools/PlayableR23/World/Classic/StagedPayload/`. No existing payload, map catalog, loader, root, source archive or shared staging file was changed.

Copy this folder's contents into `unity/Assets/StreamingAssets/PlayableR23/Maps/classic-bigbox-vr/`, create Unity metadata through the integrating owner, and add the catalog choice:

```csharp
new R23MapChoice("archive:classic-bigbox-vr", "CLASSIC", "Maps/classic-bigbox-vr")
// Existing loader API after catalog inclusion:
Map.Load("archive:classic-bigbox-vr");
```

The payload is existing loader v1: magic 0x52323357, 20-byte chunk headers, twelve floats per vertex (position/normal/UV/color). All 280 chunks render and collide; this source has only world nodes, with no boundary/playfield/false/killzone/trigger layers to reinterpret. All 1,946 source mesh nodes, 1,685,957 vertices and 1,319,789 triangles are retained. There is no decimation, replacement platform or terrain approximation. Positions/normals reflect Z once, normals use inverse transpose, UV V flips once, and source indices remain unchanged for this flat identity-transform GLB. 935 zero source normals are repaired from actual adjacent triangle geometry; all source positions, UVs, colors and valid normals are preserved.

Fifty-one original provenance materials and 96 exact source JPEG/PNG files are included, with no missing textures. Material roles match original `tools/convert_archive1003.gd` (.72 roughness, optional .35 metal, .6 normal strength, vertex colors, double-sided). Existing v1 loader interprets the three original alpha-blended rows as its existing alpha-cutout roles; this bounded conversion does not change that shader/loader limitation. Original local-only source license metadata is retained in the payload.

Raw GLB identity is **0179873e65c372825682725737bfcd34762132cf9d759342425fff0d2bb8079d** (47,343,704 bytes), verified against both preserved original r14/r9 semantic datasets, the external manifest and provenance. The supplied **a687f6b9bb18aceadeb030f0ce91eae5630b7d52ee87217fd06048fdfeed35d1** is the original prepared `.res` identity in `archive_adapters_r10.json`; it does not identify the raw GLB. Both are recorded without modifying source.

Classic has zero recorded, authored deathmatch and authored warmup spawn rows. The CPU port follows original `archive_map1003._settle`: 19×19 samples across solid/playfield intersection, nearest actual floor normal >.7, four ±.4m footprint support rays within .3m height, source .3m-radius/1.9288m-height capsule clearance, nearest-center 16 candidates with 2m spacing, then source height separation. It found 138 valid grid sites. Game entry is **(83.8539467, 20.4637769, 40.6662598)**; warmup entry is **(-21.8648071, 26.62, 40.6662598)**, Unity map-local coordinates. Each is actual floor+.12m. Manifest `spawn`/`gameSpawn` use the game entry and `warmupSpawn` uses the warmup entry; no `recordedSpawn` is invented. Detailed source triangle witnesses and footprint/capsule proofs are in `spawn-proof.json`. An additional conservative 1.95m capsule at each published root also passes against the emitted collision triangles.

Run `python unity/Tools/PlayableR23/World/Classic/prepare.py`, then `python unity/Tools/PlayableR23/World/Classic/check.py`. Conversion took 1.49 seconds and peaked at approximately 253 MiB working set. Complete staged payload is **100,589,055 bytes**, below 300 MB. Independent readback passes checks of every original position/index/UV/color, all valid normals, material association, source hashes, all 96 texture identities, chunk bounds/census and support/capsule clearance from the emitted geometry. `check-results.json` records the result. Source camera far-distance formula gives approximately **4887.54m** for this map; a host retaining 2500m may clip distant scenery.

Native mesh cooking, render appearance, frame rate, collision entry and headset traversal remain unverified. No Godot/Unity/Blender/render/build/device job was run. Original raw static geometry/material overrides are ported; prepared dynamic destructibles, fixtures, art repairs and runtime scripts are not separately baked. This payload should enter the next coordinated native map smoke before any runnable-build claim.

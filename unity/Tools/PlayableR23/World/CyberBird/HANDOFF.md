# CYBER BIRD — genuine frozen R6 map

Published `StreamingAssets/PlayableR23/Maps/cyber_bird`: **159 v2 chunks, 111 materials, 1,374,156 render triangles, 899,568 collision triangles**. Includes the actual 144 buildings, 825 storeys/stair runs, 18 bridges, 114,367 source boxes (74,689 solid), 1,650 one-sided foot ramps, and 876 source signs. New payload is approximately 225MB. Existing 107 payloads were not changed. No engine/GPU/device/export job ran.

Baseline is immutable R6 `23951bcc4c358a38a0fd1ad59fa24cef0171f5df6c6c2cc2e38dca5a095f5520`. All consumed frozen files are verified against `SOURCE-MANIFEST.json` SHA `b62578d4…`; hashes are in `provenance.json`. This is the actual `scripts/cyber_bird.gd::build`, selected by `cyber_bird_registration.gd`, not a reconstruction from the historical title.

## Root integration

- Catalog: ID `cyber_bird`, title `CYBER BIRD`, folder `Maps/cyber_bird`, genuine source previews `preview.png` and `preview-top.png`.
- Geometry is world-baked. Source map origin `(0,0,2400)` is already included and Z reflected once. Authored Unity spawn is **`(-9,.16,-2765)`**. Do not add another origin offset. Bounds are `(-400,-4,-2800)` to `(400,422,-2000)`.
- `map.json` is v2: 18 floats/vertex, including original unreflected primitive position/normal. Godot's actual BoxMesh 24-vertex/36-index topology and 3×2 UV atlas are derived from the previous qualified 4.7.2 primitive capture; exact unit coordinates/axis normals restore only floating size/packed-normal quantization. No procedural shape is guessed.
- `chunkSourceLayers[]`: world=1, render-only=0, **foot probe only=16**. Preserve source16 as a dedicated layer included by foot support and excluded by body/head/grip/bullet/projectile queries. Ramps are exact six-vertex one-sided surfaces, not filled wedges.
- **Bind `chunkCastShadows[]`.** The giant canopy explicitly has shadows off, as do neon/signs. Casting a canopy shadow would incorrectly darken the whole map. `chunkVisibilityRanges[]` retains source detail170m/shell950m/sign300m; current native batching groups multiple original MultiMesh owners, so identical per-building culling is not claimed.
- Six material rows use `profile="cyberWeathered"`: ordinary `_Tint/_Metallic/_Roughness` plus `bounceColor`→`_BounceColor`, `floorFinish`→`_FloorFinish`. Copy the staged `Tools/PlayableR23/World/CyberBird/R23CyberWeathered.shader` into the root-owned Resources and select it for that profile. Exact source world-position grit/seams/streak/floor-grout/roughness/bounce formula is retained. The staged URP scaffold adapts source-color linear math once for the current Gamma project; actual shader compilation/lighting/tone parity is unrun.
- `cyberEnvironmentFile="environment.json"` is deliberate. Standard `environmentFile` stays empty to avoid the existing Valley-only HDR parser. This source map has a color background, no HDR sky. The JSON contains exact source moon/fog/ambient/glow values, four street lamps with128 sites, and six nearest interior/neon lamp descriptors. Root owns applying them.
- `city.json` retains buildings, real opening descriptors, routes, loot points, foot ramps, signs, source chunk identities, source fall reset threshold `-12`, and deployment height. CPU-only box receipts stay in Tools (`source-boxes.npy`) rather than allocating a 35MB JSON array at runtime.

The source front menu initially selects Cyber Bird (`front_menu1000.gd:53`) outside scope-playtest mode; root owns any default-selection adaptation.

## Working checks and limits

`python unity/Tools/PlayableR23/World/CyberBird/check.py` checks the stage; append the published directory to check the live bytes. Checks passed: source census, every native binary range/normal/role/winding, source material box counts, 33 actual open-window segments, real authored ground plus full1.9288m standing capsule, exact genuine previews, and36 weathered-source formula cases. No invented support geometry is added.

The geometry tool executes a deliberately restricted, manifest-bound subset of the actual GDScript functions; unsupported syntax fails. It preserves source integer division, integer-array loops, dictionary fields and float32 Vector/Basis math. `executed_geometry.py.txt` is the exact translated code, not a second hand-authored city layout.

Signs use genuine Godot default OpenSans SemiBold glyphs, source96px/4px outline/colors/pixel sizes, rasterized on CPU into99 unique RGBA PNGs and positioned with source transforms. Exact native Godot Label3D baseline/raster/line spacing and glow/tone output remain unverified. Static occluder nodes, pooled-light behavior, traffic,384 live weapon instances, ammo/consumables and source runtime rules are separate integration work; they are not faked with scenery or grants.

## One finite native follow-up

After root binds the profile/environment/layer/shadow fields: load `cyber_bird`, wait for native collision readiness, verify ground and standing clearance at the authored spawn, and capture the terminal view looking Unity+Z. Probe a source foot-ramp midpoint with the dedicated foot mask while proving weapon/grip/head masks exclude its layer. Probe one `city.windows[]` facade segment on both sides to verify an actual opening. Capture one interior, one sky bridge and the source top preview view; confirm the canopy renderer has shadow casting off. These are proposed checks, not executed results or HMD acceptance.

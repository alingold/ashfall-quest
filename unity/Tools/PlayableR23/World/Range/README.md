# Actual R23 SANDBOX RANGE payload

`prepare.py` translates the four original boxes from `quest-r24/source/scripts/sandbox_map_091.gd:5–13` and the actual `field_item_models.gd:244–263` rounded-box algorithm. It reads the actual dimensions, colors, material values, origin and spawn from the preserved script. This is a direct CPU recipe port, not a Godot export. It never starts an engine or changes the source tree.

Run from the repository root:

```powershell
python unity/Tools/PlayableR23/World/Range/prepare.py
python unity/Tools/PlayableR23/World/Range/check.py
```

Output is `unity/Assets/StreamingAssets/PlayableR23/Maps/range/map.json` and `geometry.bytes`, with metadata for those two files. The existing map v1 binary layout is unchanged: magic `0x52323357`, four chunk records, 12 float32 attributes per vertex, Int32 indices. Four render-only chunks contain 224 vertices and 432 triangles (16,024 binary bytes). Each has collision=false and visible=true. No render-mesh collider should be created.

Root must consume the four optional `boxes` descriptors and construct native BoxColliders. Their world centers and sizes are:

| Source part | Unity center | Size |
|---|---|---|
| Floor | (0, -.2, -600) | (90, .4, 100) |
| Rear wall | (0, 8, -562) | (90, 16, .3) |
| Left wall | (-45, 4, -600) | (.3, 8, 100) |
| Right wall | (45, 4, -600) | (.3, 8, 100) |

Godot's map origin is (0,0,600), and its world spawn is (0,.2,608). Positions/normals reflect Z once, and triangle indices swap once for Unity clockwise fronts. Published spawn is (0,.2,-608), with no inferred settling or replacement spawn. Bounds are (-45.15,-.4,-650) through (45.15,16,-550). Local source specs, original-coordinate origin/spawn/bounds and the SHA256 of both original scripts are retained in `map.json.source`. Source bodies are all climbable; the floor also belongs to construction_terrain, recorded in source metadata for the runtime owner.

The source .01 m bevel is preserved, including source float32 Vector3 operations, smooth outward normals and indexed attributes. The source authors no UVs, so the required payload UV slots are zero. Raw tint is baked into vertex RGBA: floor `48545b` (metallic .35, roughness .65), walls `26343a` (.4, .62). Material texture fields are empty; there are no texture or painted-terrain substitutes.

`check.py` independently reads the binary, compares collision descriptors/materials with the source recipe, validates bounds, index ranges, nondegenerate triangles, unit normals, clockwise winding, five spawn-footprint floor supports, rendered floor beneath spawn and wall clearance. `checks.json` records the focused CPU result and payload hash.

The source range inherits the existing Valley environment, which is not ported here. This payload also does not implement source inventory relocation, training, dynamic activities or map catalog registration. Those remain runtime-owned. No engine, native collider, lighting/render, performance or headset claim is made.

Root integration now registers `range`, creates its four actual native BoxColliders from the optional manifest boxes, and provides an empty authored-pad set. Original and R24 copies of both scripts were byte-identical at integration; the generator now reads the preserved original directly. Geometry hash stayed unchanged. These root changes are API-compiled, not engine-run or included in215235.

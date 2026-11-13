# Actual Valley v2 conversion — ready, full publication blocked by lost source poses

`prepare.py` converts the completed real source export into the accepted v2 binary/material/collision contract. Default production conversion **rejects the1124lost MultiMesh poses before creating/writing any map payload**. The headless export has16empty buffers and identity transforms for all1124instances. Root/WORLD require a genuine corrected observer capture; this converter neither invents poses nor infers material/pose associations from collision order.

Current working partial is deliberately noncatalog at `Tools/PlayableR23/World/Valley/diagnostic-direct/`:

- 239 actual visible direct instances / 581,636 render triangles; 24 hidden mesh instances excluded.
- Every enabled source static shape: 1,138 exact oriented boxes, 114 concave occurrences / 451,394 exact source triangles, nine independent convex clouds / 300 hull triangles, and 11 independent cylinder prisms / 2,772 triangles. Total: 1,272 shape occurrences, 454,466 mesh collision triangles, 144 chunks.
- All 112 actual source materials plus one invisible collision-only default; 36 byte-identical exported PNG views. There are 13 color images, 11 normal, ten roughness, nine metallic, zero AO and one emission image; packed roughness/metallic images share files. No filename crosses sRGB color/emission and linear data roles.

```powershell
# Current valid CPU diagnostic, writes Tools only:
python unity/Tools/PlayableR23/World/Valley/prepare.py --diagnostic-direct
python unity/Tools/PlayableR23/World/Valley/check.py
# Full conversion after genuine corrected export replaces/fixes MM instance poses:
python unity/Tools/PlayableR23/World/Valley/prepare.py --source <corrected-export-valley-folder>
```

The latter publishes only map.json/geometry.bytes/source material PNGs/provenance into StreamingAssets/PlayableR23/Maps/valley; no C#, shader, sky, environment, original source, export logs or qualification files are modified. `environmentFile` is `environment.json`, owned by the separate environment task. Current diagnostic map ID is `diagnostic:valley-direct`, `diagnosticOnly=true`, `completePlayableMap=false`; it must not be cataloged.

Binary: magic0x52323357 /chunk count, then the unchanged20byte chunk header (`material,collisionFlags,visible,vertexCount,indexCount`). Each vertex18float32: Unity world position3/normal3/UV2/color4 followed by ORIGINAL GODOT LOCAL position3/normal3. Indices int32. Vertex positions reflect Z once; normals use actual affine inverse transpose and normalize; raw source-local fields remain unchanged. Godot source clockwise faces already change handedness with reflection; no unconditional glTF index swap. Negative-determinant source bases receive the matching single winding compensation. UVV flips once; the runtime shader must not repeat it.

Collision flags0render-only /1concave /3convex. `chunkSourceLayers` and `chunkBackfaceCollision` align one-to-one with chunks. Source bits1/4/8/9 are preserved across chunks and boxes. Convex owners are never combined: each real source hull/cylinder cooks independently, avoiding an enclosing union hull. Boxes use `{name,center,size,rotation:[x,y,z,w],sourceLayer}`; all1138have source-equivalent corner geometry. Concave35backface-true/79false shape occurrences retain their flags. Original convex point clouds, complete shape properties/transforms and margins remain in provenance.

Each CylinderShape3D uses an explicitly approximate64sided inscribed convex prism:128vertices /252triangles, source height/radius/transform intact. Maximum world radial sagitta is under.002m. This is not a capsule or fake analytic-cylinder claim. No procedural terrain approximation occurs.

Materials: `name,profile,tint[4],color,normal,roughnessMap,metallicMap,occlusionMap,emissionMap,metallic,roughness,normalScale,roughnessChannel,metallicChannel,occlusionChannel,emissionColor[4],emissionEnergy,emissionEnabled,emissionOperator,uvScale[3],uvOffset[3],uvTriplanar,uvWorldTriplanar,uvTriplanarSharpness,vertexColor,vertexColorSrgb,transparency,alpha,alphaCutoff,cull,unshaded,snowHeight`. Channels0R/1G/2B/3A/4grayscale. Transparency0disabled/1alpha/2scissor/3hash/4alpha-depth-prepass. Cull0back/1front/2disabled. Emission operator0add/1multiply; the actual mapped emissive uses1. Known source alpine ShaderMaterial maps to `profile=alpine`, source `snow_height`, roughness1 and cull2.

Spawn is actual `(0,1,-8)`. `markers` contains reflected Unity positions; `sourceMarkers` retains the untouched tagged Godot data. Bounds derive from converted visible vertices. Original qualification and SHA256 are retained in provenance:1633typed-null optional Object-property errors remain qualified. Six source Label3D recipes still lack exported glyph geometry. The original qualification's broad completeness language does not override the newly discovered missing1124poses.

Focused CPU checks pass1642assertions across real data: exact triangle/shape/image census, full binary spans, all1138OBB corner transforms,20separate closed manifold hulls, unchanged local massif arrays, actual world reflection, independent inverse-transpose normal fixture, material fields, source evidence identities and full-publication refusal. No engine/GPU/device/Blender run, original-source edit, C#/shader edit, source-export rerun or catalog/StreamingAssets map publication occurred.

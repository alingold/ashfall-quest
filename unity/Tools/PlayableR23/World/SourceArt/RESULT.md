Source art helper is released for Root loader integration. No existing map manifest/geometry, loader, scene, collision, architecture, road, trailer, wheel-family or destructible file was edited.

The actual matched source/Unity freight captures were viewed side by side: teal corrugated container sides were flat blue, and source vehicle paint/glass were absent. ALFA architectural captures also show source wood trim where Unity is flat tan. Original captures stay untouched. New helper transfers genuine prepared R10/R14/R17/R19 rendering treatments onto the same existing Unity triangles.

Read-only inputs bind frozen R6 content `23951bcc4c358a38a0fd1ad59fa24cef0171f5df6c6c2cc2e38dca5a095f5520`. R10 resources are read directly from the original `ASHFALL-Map-Adapters-R10.pck`, checked against frozen archive_adapters_r10. Classic `a687f6...` is the **prepared adapter** hash; raw GLB is `017987...`. SourceArt/source.json retains the stage/resource/input bindings.

The important stage correction: Godot `Mesh.get_faces()` hashes TriangleMesh positions snapped to0.0001. The step is float32, Math.snapped computes double, and Vector3 receives float32. Hashing unsnapped surface positions was incorrect. Across all nine staged maps, all210 source R17/R19 stage digests now match exactly; R14 vertex/index counts match prepared R10. All775 comparable final Main base arrays match byte for byte after exact staged erasure. Surface positions are not snapped/replaced in Unity.

Public API, namespace `Ashfall.Integration.Playable.World.SourceArt`:

```csharp
var art = new R24SourceArtMaterials();
yield return art.Load(mapId, originalChunkCount);
if (art.Failure != null) /* report/retain original rendering */;
if (art.Plan != null) art.Plan.VerifyGeometry(originalGeometryStream);
// Before compacting indices. Owner subset may be static, tower fixture, or panel.
art.Plan.Select(chunk, fullOriginalChunkIndices, existingOwnerRenderSubset,
                out int[] keep, out R24SourceArtPart[] details);
// Renderer uses keep; collider retains the ORIGINAL owner subset.
foreach (var part in details) {
    Mesh mesh = R24SourceArtMaterials.BuildDetail(part, originalPositions,
                                                 originalNormals, originalColors);
    Material material = art.ForKind(part.Kind);
    // Attach renderer to SAME live existing owner; withdraw with that owner.
    // Register with any owner that stores an explicit renderer list.
    // The loader owns/destroys each returned Mesh.
}
// Dispose materials/textures on unload only after withdrawing their renderers.
art.Dispose();
```

`Plan.VerifyChunk(chunk,stream,vertexStart,indexStart,end,nv,ni)` optionally checks exact source chunk spans. `PatchCount`, `MapId`, `Ready`, `Failure` provide diagnostics. Unknown maps load Ready with null Plan. Load uses Android-compatible UnityWebRequest and yields each resource; no engine calls in tools. Runtime never creates colliders or writes tracked poses. Original colors are copied into details, with fresh per-corner projected UV3. R10 details additionally carry the actual source-generated per-corner normals, reflected once to Unity; later stages retain raw normals; base architecture classification is not modified. Root must keep rendering and original collision meshes separate.

All nine maps are now LIVE. Root personally confirmed the corrected two-map container/van/tire rendering in the 18:55:52 native capture; the additional seven have CPU/API checks only. Runtime API is unchanged; `R24SourceArtPart.SourceNormals` is optional and BuildDetail consumes it automatically.

The concrete missed-container cause was generated node names: Godot renamed repeated extracted owners to `@MeshInstance3D@N`. Selection now uses the actual ShaderMaterial's exact container/van shader resource identity, and tires use the actual shared R10 tire material reference. Classic includes all14 container and13 van owners. R10 SurfaceTool deliberately omits COLOR and generates smoothed normals; it is incorrect to demand raw blue==source white or raw hard normals==source smooth normals. Its bindings instead require the exact material identity plus oriented, whole source triangle correspondence. Source normals are preserved in rendering only. Later R14/R17/R19 correspondence additionally requires all three original RGB and oriented vertex normals.

| Treatment | Classic source=Unity triangles | ALFA source=Unity triangles |
|---|---:|---:|
| R10 containers |1476|624|
| R10 vans |5194|5292|
| R10 tires |2400|1300|
| R14 container ends |48|636|
| R14 wood |28|28|
| R17 wood |4510|5171|
| R17 van |98|1594|
| R17 truck |1014|624|
| R17 tire |1744|1248|
| R19 banner |20|26|
| R19 sign |16|32|

Two maps:33123 render triangles /58 chunks. Exact corrected Classic witnesses265/22077,267/852,267/2434 all map to container_r10, within1.28mm of source corners. All source-kind triangle census counts equal transferred counts. Unique selected-source counters also include coincident equivalent duplicates; they do not indicate lost/new geometry.

Correspondence uses oriented cyclic whole triangles within5mm; R10 uses oriented geometric faces, later stages also require per-corner RGB<.005 and normal dot>.9995. Conflicting coincident style/UV/source normals remain unchanged (zero conflicts in these maps). No radius paint, planar fill, source geometry replacement or collider modification occurs. Root's architecture partition/coplanar work remains separate, as do R24 junction/trailer/wheel and other R19 deck/rail/steel/wheel treatments.

The seven published maps add115948 source-art triangles in172 chunks. All9 total149071 triangles,230 chunks and460 independent owner-subset cases. Exact resource hashes and immutable R6 source channels are checked for each family map. SourceArt.Load now supports all nine exact IDs. The sixteen previously published two-map payload/resource files remained byte-identical; only the supported-ID list changed in existing runtime.

Source textures come from actual R19/R21 **embedded ETC2 Image resources**, decoded on CPU to RGBA rather than substituting the cleaner original PNG. Base mip payload, decoded pixel and output hashes are retained in images.json, plus original attribution files. Unity regenerates mip levels from that source base; exact embedded lower-mip parity is not claimed. Tiny MIT texture2ddecoder1.0.6 wheel is isolated under this tool's vendor directory; no environment/global/source install.

Shaders preserve source procedural rib/window/lamp/seam/grain/paint expressions, roughness/metallic, R19 projected V and side-dependent artwork U. Only texture sampling V is reflected for Unity LoadImage. Source lighting versus URP illumination/transparent overlap and banner front/back appearance still require matched native visual confirmation. No shader compilation, engine/GPU/Blender/device/build was run by this worker.

Checks PASS:58 live-map chunk partitions /33123 triangles /116 owner subsets;230 all-nine published chunks /149071 triangles /460 owner subsets; every R10 corner retains a normalized actual source normal. Original collision/index arrays remain unchanged. Source image/resource pixels and installed Unity runtime/editor/build-smoke compilation pass. Tools: prepare.py stages; check.py checks live-two, check.py --all checks staged nine; check.py --all --published checks live nine; publish.py --existing-only publishes two, plain publish.py publishes nine. Only disjoint World/SourceArt runtime/resources and StreamingAssets/PlayableR23/SourceArt payloads are copied. No engine, renderer, device, historical capture/APK/source or existing map geometry/manifest write was performed. Root performs native visual comparison.

Godot snap behavior reference: [Mesh::get_faces](https://github.com/godotengine/godot/blob/4.5/scene/resources/mesh.cpp#L477), [TriangleMesh::create](https://github.com/godotengine/godot/blob/4.5/core/math/triangle_mesh.cpp#L127), [Math::snapped double signature](https://github.com/godotengine/godot/blob/4.5/core/math/math_funcs.h#L630). Exact CPU agreement with all45 serialized target digests is the target-version verification.


Targeted Classic panes are LIVE after the 19:17:45 release. Actual prepared panel433 maps to chunk185 triangles351/352; panel616 maps to chunk186 triangles284/285. New clear_glass source-art parts use the already-owned R23ArchiveGlass shader (.07+edge*.14 alpha). Their four opaque attachments per panel stay unchanged, as do raw266/6978 and270/18228; raw266/4977 does not correspond to these panes and remains opaque. No missing frame geometry or1192-panel gameplay coverage is claimed. Four-pane publication retained every prior art part and all29 other payload/resource files byte-identically. Published9 CPU checks:232 chunks,149075 detail triangles,464 owner subsets; all3 API targets pass. Native window visual confirmation remains pending.

The additive pane converter is windows.py (Tools staging only), followed by check.py --window-candidate and publish_windows.py after Assets release. Re-run windows.py after a general prepare.py regeneration to retain this additive four-pane patch. The ordinary staged runtime/payload now also contain the released pane additions. No RootMap integration change is required.


Detached R17/R6 junction treatment is now LIVE. The omission was kind `junction` being erased during exact source stage replay but excluded from SourceArt output. ActualR17 plans contribute686 triangles: ALFA114,BigbabyClassic114,ClassicBW114,ContCLER115,ContCL115,StatmasterClassic114. Classic/Evolving/StatmasterEvolving have zero detached R17 junctions and receive no fabricated subset. All previous149075 art triangles, four targeted clear-glass panes and opaque attachments are unchanged.

New Resources/R24SourceJunction.shader directly translates frozen alpha_junction_r24.gdshader's seven position-bounded projection expressions, repaired asphalt/crosswalk ranges and originalUV3 fallback; it does not use a generic road or fill geometry. Godot source map-local positions reflect Unity localZ once; root-world translation is not part of the projection. No Root architecture binding/texture/API change is needed. The source R6 family controller guards seven actual R17 parts on the five family variants; ALFA uses its pinned alpha_junctions_r24 guard. Existing approximate SourceArt URP lighting remains an explicit visual limit. New shader engine compilation/rendering is pending.

Actual captured Bigbaby junction centerpixel(600,400) at1200x800 has nearest CPU ray hit chunk253/triangle9213,32.3843m from the exact recipe camera; that face now receives `junction`. junction-checks.json retains the source shader hash/census/witness. Published checks pass232 chunks,149761 total art triangles,464 existing-owner subsets and all3 installed Unity API targets. Geometry, collision, RootMap and architecture core are untouched. Tools: junction_shader.py emits the exact fragment, prepare.py includes actual junction parts, then windows.py reapplies additive pane staging before publish.

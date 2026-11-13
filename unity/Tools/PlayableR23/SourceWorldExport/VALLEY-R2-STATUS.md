# Valley integration

The first executed export is preserved under `runs/prepared-r23-valley-range`.
`qualification-correction.json` withdraws its complete-map qualification: the
headless renderer returned empty buffers and identity poses for all 1,124
MultiMesh instances. Direct meshes, collision recipes and images remain useful.
The converter still rejects that historical capture.

Revision 2 guards null Object properties and observes the original builder's
`_flush_batches()` inputs before its normal flush clears them. Original source
files are unchanged. The frozen new exporter and fresh output plan are in
`runs/valley-source-batches-r2`. This revision ran once in the coordinated
06:09–06:14 UTC slot, finishing at 06:10:12. It recovered all 1,124 nonidentity
batch poses; no null-Object script errors remain. Both lanes were released at
06:10:13. The original 5,586-file reference was verified and reused without a
second import/copy. No Range, Unity build or device operation ran in that slot.

CPU integration now connects the v2 map loader to source material maps, the
translated alpine shader, HDR panorama/environment, rotated boxes and separate
physical/ballistic collision roles. Original local vertex/normal channels are
preserved for the terrain formula. Installed Unity API compilation passes.
Full CPU conversion is now in StreamingAssets/PlayableR23/Maps/valley: 595,124
visible triangles, 1,272 source collision-shape occurrences, 36 material PNGs
and the exact HDR sky. Source/converted triangle census and binary layout
match. The Valley map card and its empty source pad list are wired in source.
The existing v1 maps remain supported; the released 222943 executable is
unchanged. Initial HUD fill widths are also fixed in source.

Next native action, only after the human player exits and WORLD releases a
slot: `run_released_slot.py --valley-only --release-until <concrete UTC deadline>`.
This invokes the staged XR-disabled R23ValleySmoke: actual map/environment,
shader errors, room-scale spawn/settling, movement, pickup/fire, damage/restart,
lobby environment restoration and four rendered views. Then build a separate
PCVR candidate. The fixture is API-compiled, not executed yet.

The source UI now uses Godot's embedded Open Sans SemiBold, with all glyphs and
metrics preserved during WOFF2 decoding. A startup-only XR retry component is
also staged after the human launch exposed FORM_FACTOR_UNAVAILABLE before
Quest Link connected. It retries only when there is no active loader and never
tears down an initialized session. Both changes still need native verification.

Rendering limits pending native comparison: URP/Godot filmic and light falloff,
normal-map tangent reconstruction, transparent depth-prepass/hash behavior and
six Label3D signs. The standard material UV/triplanar and emission rules were
checked against the [Godot 4.7.2 shader generator](https://github.com/godotengine/godot/blob/4.7.2-stable/scene/resources/material.cpp).
No shader compilation, rendered comparison or headset acceptance is claimed.

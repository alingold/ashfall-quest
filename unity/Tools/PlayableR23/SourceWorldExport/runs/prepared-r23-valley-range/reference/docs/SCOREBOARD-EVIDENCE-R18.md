# Source scoreboard evidence and recovery — R18

## Confirmed behavior

MellyPop uses the four original measured Megatron faces from the prepared source adapter. The face resource identities, positions and UVs are unchanged. Native `native-score-depth-frame-r18` shows the reference-inspired navy display clearly on all four orientations. The unnecessary ASHFALL / SCOREBOARD label is removed. White header/divider, cyan names, compact ranks and white right-aligned scores are original UI styling inspired by the user reference; the screenshot is never loaded as game art. No dedicated current-skin 2D thumbnail exists in the available UI path, so explicit initial badges are used instead of fabricated portraits.

The original blank-board report was not reproduced at the measured front view: the earlier canvas was crisp, but a source forcefield overlaid it. Exact segment/triangle evidence identifies `ArchiveGeometry/Draw_world_pb8a19331_-1_-1`, triangle1156, at local(-34.23862,216.25082,76.50146),8.06348m ahead of the board. Its existing `force_field_r9.gdshader` uses transparent blending and depth_draw_never. That shader and source FX geometry are unchanged. Only the board material enters a late alpha1 pass with priority100, ordinary depth testing enabled and depth draw always. Native opaque BoxMesh proof completely hides the board; sampled luma0.137255?0.733333. No global through-wall display or geometry move is used.

Presentation reads real authority: team mode uses configured/current-roster team IDs; FFA uses peer-ID scores and player names. Player ranks are stable, spectators excluded, and zero time/cap render ASCII UNLIMITED. Pure layout tests retain all eight teams plus all eight active players inside the1280px canvas. Real local listen-server death/score tests show score1, cap25 and countdown119 on MellyPop and Paintball. No host rules, score mechanics or HUD files are changed.

## Paintball: original frame, generated display face

Actual high-lobby spawn: local(22.64996,73.07250,14.51465), world origin(0,0,4800).

The original frame is directly LEFT of the actual WELCOME TO BATTLEFIELD PAINTBALL sign, not at either lower title-letter cluster. Frame center: local(-3.960614,78.74280,26.23660). Complete source assembly:64triangles in four connected-component parts (20+12tile triangles and16+16trim triangles), represented by two prepared mesh chunks:

- Draw_roof_p8ad59c41_-1_-1, SHA256 `9b4138d3de7aaf8d21a432284c4ecd3b50bdcc57b511dadd15d11121ef70fb17`.
- Draw_roof_pd959969c_-1_-1, SHA256 `78a91327c2793239bb6b31a86312569253327858154e021aa4440e62bd8c6758`.

Native original/context images show an empty aperture beside the Welcome lettering. No exported face spans it. The generated two-triangle display is constrained to the original aperture: x=-3.960614, y74.847298..82.638298, z22.694317..29.778891, normal toward lobby(+X). It occupies the frame depth; no new floating board/frame is placed. Native probe fits cleanly inside it. This is recovery of an authored screen position from surviving frame geometry plus native/context/reference evidence, not reconstruction of an unavailable original dynamic widget plane or script.

Production helper/data: `archive_score_surfaces_r18.gd` / `world_score_surfaces_r18.json`. The helper checks normalized data SHA256, exact cache fingerprint, both source frame chunk hashes and transforms. It creates display geometry only and inherits render layers/groups; original meshes, collision and registrations remain intact. It stores no static generated-resource or viewport references. One local-array hook in archive_gameplay_r8 merges recovered rows with existing score_faces_r9 without mutating cached details. Actual READY and hosted activation tests pass, including weakrefs becoming null after unload and original pack details remaining empty for Paintball.

## All-map coverage boundary

The complete four-part64triangle frame signature was searched across all105map source geometry, all materials and world/roof layers. Only Paintball matches completely. Four dimension candidates were rejected because they do not match the full point/part signature:

- classic-bigbox-vr, evolving-map-bigbox, statmaster-evolving-soulsmight: vc near(181.56580,44.95995,543.79486).
- zelda-kokiri-forest-solos-ilikepies1211: t51 near(-91.18935,60.65603,0.27532).

No generic door/window/rectangle or neutral-color fallback is enabled. Scaled/tilted variants, differently fragmented exports, other widget families and omitted widgets without a surviving authored placement remain unsupported. The independent MellyPop solid-screen profile scan also found only MellyPop. Current positively supported score coverage is MellyPop plus Paintball; this does not claim all105maps had recoverable boards.

## Available original evidence inventory

Coordinator artifacts reviewed: evidence/coordinator-score-metadata-review.json (730JSON files across105maps, paths/hashes/topkeys, zero exported script files and zero searched scoreboard/leaderboard/Welcome terms); coordinator-score-glb-schema-review.json (105GLB JSON headers, zero extras objects/scoreboard terms). The source GLB geometry search and native images supply placement evidence; term absence alone never proves that an authored native widget did not exist.

Available per-map evidence includes source/map.json (map/rules/template/spawns/materials), source/provenance.json (source/license/retrieval/material/texture provenance), source/geometry.json (geometry hash/accessor expansion), converted/manifest.json (cache/hash/bounds/collision/draw/source metadata), download.json and binary/image receipts. No source.json file exists in the supplied local_archive tree. No original js/ts/gd/cs/lua/py gameplay export exists in the reviewed map directories. Map.json and GLB headers do not contain native widget transforms or scripts. The imported Paintball aperture is visibly empty despite its original frame surviving; this bounds the omission claim to the available export, not the original native game.

## Reproduction and evidence

- Native baseline preserved in evidence/before-score-style-1757/.
- Native current: R18-score-hosted.png, score-hosted-face-{1,2,3}.png, score-opaque-occluder.png, paintball-welcome-frame-{original,context,display-probe}.png, score-{practice,hosted}-canvas.png; R18-score-art-capture.json, native-score-depth-frame-r18.txt.
- Headless: tests/score_presentation_r18.gd; score_integration_r18.gd; score_authority_integration_r18.gd; paintball_frame_probe_r18.gd; paintball_score_integration_r18.gd.
- Audits: tools/audit_score_field_overlap_r18.gd; audit_paintball_lobby_r18.gd; audit_paintball_frame_r18.gd; audit_score_frame_profiles_r18.cjs.
- Merge: workspace work/PARENT-SCORE-HOOK.patch and PARENT-INSERTION-HOOK.txt. Original gameplay file UTF8/no-BOM/LF conventions retained, one hunk,4insertions/2deletions.

All captures are offscreen native PC, XR off, Dummy audio. They are not physical Quest acceptance or GPU performance measurements. Original archive reuse remains local-use/unknown where recorded; no source pack publication is authorized.

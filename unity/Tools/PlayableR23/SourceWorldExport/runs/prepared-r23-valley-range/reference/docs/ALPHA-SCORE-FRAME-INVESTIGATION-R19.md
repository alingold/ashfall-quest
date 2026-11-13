# Current Alpha source-frame investigation

Historical investigation record followed by completed recovery. Final coordinator-approved state: ART-SCORE-R19-HANDOFF.md. Diagnostic phase preserved the first art cohort unchanged.

Actual READY origins are source warmup (57.63441,46.0832,82.864) and source game spawn (-512.912,58.2731,64.641), with world map origin (0,0,4800). The search also includes street-level recorded town spawn (60.59,25.52,82.91). It scans every source material and world/roof component within 65m horizontally and 40m vertically of these neighborhoods. Neighborhood candidate filtering is diagnostic and not an absence proof.

All available Alpha source files were inventoried: map.json, provenance.json, geometry.json, map.glb and its receipt, geometry.glb, thumb.jpg and its receipt. map.json has scoring rules/events but no dynamic widget placement record. Both GLBs have 2692 nodes; neither has score/widget names or extras. Geometry.json identifies the expanded geometry derivative. These metadata facts do not decide whether surviving frames are displays.

## Strong building clue

Three complete source-authored frames sit on the brick town building:

| Center (source metres) | Outer size | Interior aperture | Source parts |
| --- | --- | --- | --- |
| (36.24113,27,27.88192) | 6.4885 x 11m | about 6.0100 x 10.3358m | 20 + 12 + 16 + 16 triangles |
| (42.78363,27,27.88192) | 6.4885 x 11m | about 6.0100 x 10.3358m | 20 + 12 + 16 + 16 triangles |
| (49.22964,27,27.88192) | 6.4885 x 11m | about 6.0100 x 10.3358m | 20 + 12 + 16 + 16 triangles |

All are the complete Paintball four-part hollow-frame point-cloud family under independent horizontal/vertical scaling and rotation: normalized maximum point error about 0.000001. The fixed-dimension R18 test could not find these scaled variants. Each retains both black tile and black trim source components. Their aperture centers have no spanning source display face: a source ray from the street reaches s14 brick wall approximately 0.5619m behind the frame center. A second s9 surface is about 1.0119m behind. The extracted original s14 texture is brick; no glass/window texture was inferred from diagnostic gray rendering. Their complete geometry and mounted façade context make these viable score-display recovery candidates; subsequent recovery and hosted native approval are recorded below.

Actual READY confirms all three, plus these nearby pillar-mounted frames:

- Portrait at (85.50193,30.25,79.11807), outer 4 x 8.5m. Full four-part family match, normalized error about 0.000002. Aperture center ray reaches black s3 pillar surface 2.048m behind the frame.
- Two stacked landscape frames at (42.38192,28.69290,82.50241) and (42.38192,30.89763,82.50246), outer approximately 5 x 2.2047m and 5.1181 x 2.2047m. Complete four-part family matches. Aperture rays reach s15 metal about 0.1643m behind. Their role may be signage/control panels; no scoreboard role assigned.

Current live frame chunks shared by these six candidates:

- Draw_world_pd959969c_0_-1: SHA256 388c944238b2229751de92c9c6ef2a7693d1f46747004e161435def451321a58.
- Draw_world_p8ad59c41_0_-1: SHA256 0d8d78b9645873fe3e0b08d2766e159beef332f78c2e11face3b5c63d6da2042.

All have identity transforms and render layer 1. Exact live frame aperture AABBs, map fingerprint and guard data are saved in evidence/alpha-live-score-frame-candidates-r19.json. tests/alpha_score_frames_r19.gd asserts all six complete frame assemblies survive actual READY and reports production_scoreboards=0. No cached map resource is mutated by this probe.

## Other spawn and limits

The source game-spawn neighborhood search includes all materials, not just vc/t52. Candidate panels and door/window-sized assemblies are recorded in evidence/alpha-score-neighborhoods-r19.json. A first s47 candidate near (-506.56,50.81,38.29) uses an opaque orange metal texture and has an intervening source structure; it is not approved as a scoreboard. No conclusion of scoreboard absence is made there.

Evidence tools: alpha_score_neighborhoods_r19.cjs (neighborhood inventory), alpha_frame_affine_probe_r19.cjs (full four-part independently scaled point comparison), alpha_frame_rays_r19.cjs (all-material source ray intersections), render_score_context_r19.py (CPU context only), alpha_frame_materials_r19.gd (read-only original material extraction). CPU images use diagnostic gray for textured surfaces and cannot establish final native appearance or exact pixel occlusion.

## Recovery candidate after the native art checkpoint

The native art AFTER batch completed and its van image shows the triple empty brick-façade frames in the actual current scene. A dedicated generic helper/data extension now supplies three two-triangle score faces within those measured apertures, inset 3mm from the inner source edges, at the original frame mid-depth. Existing archive_world_score_r9.gd canvas/style, actual authority and ordinary world depth are reused unchanged. The one-line archive_gameplay_r8.gd hook appends R19 rows after R18 rows and does not mutate cached details.

The exact full-sized town frame signature was then scanned across all 105 maps, every source material and world/roof layer. It yields precisely these three Alpha matches; no other map matches this complete sized point/part signature. Evidence: town-score-frame-family-audit-r19.json. This does not exclude other sizes, tilted or differently fragmented frames or different display families elsewhere; those remain diagnostic-only. No inferred extra scoreboard is enabled from a rectangle/color heuristic.

Data is normalized-SHA256 guarded; map fingerprint, both complete source chunk SHA256 values and identity transforms are mandatory. Meshes inherit original source layers/groups and have no static generated-resource references. The source geometry/collision remains unchanged. tests/alpha_score_guards_r19.gd verifies three bounded +Z faces, rightward U orientation, repeatability, source mesh identity, and bad-fingerprint/mutated-chunk rejection. tests/alpha_score_integration_r19.gd proves actual READY produces one board with three faces, hosted team score1/cap25/countdown119, and unload releases generated mesh/viewport references. All three generated displays subsequently passed independently viewed hosted native fit/readability and opaque-world-depth proof at the final checkpoint. This remains PC evidence, not physical Quest acceptance. Other maps remain diagnostic-only until context and exact guarded data are supported.

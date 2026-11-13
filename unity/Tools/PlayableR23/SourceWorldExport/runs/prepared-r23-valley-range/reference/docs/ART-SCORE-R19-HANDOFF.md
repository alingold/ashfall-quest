# R19 final approved art and Alpha score handoff

Coordinator independently viewed and approved corrected sign head-on/context, all three score close views, and opaque depth proof. Production art: f3b0ce6 + 5a4ef2d + cad33eb. Score recovery: 9ba92e1 + 57d4d4e. Source checkpoint before this final documentation commit: 66c7398. Final documentation commit SHA is reported separately by the assistant; no history rewrite.

## Approved result and proof

- Original NORTHLINE landscape sign artwork reads correctly and fits authored frames. Native cyan stripe gate passes: left2409/right0. Head-on and oblique source views were inspected. Alpha's supported landscape family has no orthogonal orientation; none was fabricated.
- All three Alpha brick-façade score faces fit original complete source frames. Actual hosted score1–0, cap25, countdown01:59 and player rows are readable. A red opaque owned BoxMesh fully hides the center board. Existing navy presentation, authority, ordinary world depth and priority100 behavior are retained.
- Foreground truck steel deck/rails and van rubber/rims visibly improve. This does not mean all trucks or mechanical surfaces are repaired.
- Actual Alpha art counts:16 landscape plates,6 deck selectors,10 rails,10 steel parts,3 van wheel components covering4 wheels;11 added draw batches; guard_skips0. Inherited R14/R17 runtime counters and guards remain intact.

Owned native process exited0. GPU release: **2026-11-02T04:17:57.8391415Z**, recorded in work/GPU-SLOT-RELEASED.txt. No more implementation or native execution. Parent source, original assets, old workspaces, export and network manifests were not edited.

## Ordered commits

Full local chain from clean a6aa9614446b548a2616284e06d25f6d302bf3d3, in order:

1. 0cf5da6 — actual Alpha READY baseline diagnostic.
2. f3b0ce6 — original landscape/vehicle cohort, selectors/resources/tests.
3. 5a4ef2d — one-line surface art hook.
4. 3943646 — scaled source-frame investigation and capture basis diagnostics.
5. cad33eb — approved exterior text UV correction and200plate orientation test.
6. 9ba92e1 — approved three-aperture score helper/data/tests.
7. 57d4d4e — approved one-line score hook.
8. 3d1cc8d — defect-preserving capture prefix and independent cyan stripe gate.
9. 66c7398 — hosted native fixture and final reviewed proof documentation.

The final documentation-only commit follows this list and is reported in the final assistant response. Parent chooses cherry-picks; production approvals are explicitly identified above. Diagnostic commits can matter when replaying modifications to test files. No commit was amended or rewritten.

## Files and tiny hooks

Production surface cohort: source/scripts/map_art_r19.gd; source/assets/materials/assembly_r19.gdshader; source/assets/data/art_selectors_r19.json; source/assets/data/art_index_r19.json; eleven resources in source/assets/data/art_r19/; source/assets/art_r19/freight_texture.res, freight.png and ATTRIBUTION.json.

Production scores: source/scripts/archive_score_surfaces_r19.gd; source/assets/data/world_score_surfaces_r19.json. Three generated two-triangle faces are inset3mm inside measured source apertures at x36.24/42.78/49.23,y27,z27.88. Full normalized data SHA256, map fingerprint, both complete live source chunk SHA256 values and identity transforms guard recovery. No cached pack details are mutated.

Hooks are one line each: source/scripts/archive_map1003.gd after R14/R17 art; source/scripts/archive_gameplay_r8.gd after R18 score rows. Exact separate diffs: work/ART-R19-PARENT-HOOK.patch and work/SCORE-R19-PARENT-HOOK.patch. Parent owns final Alpha LOD rebake, export and content/network manifests. Include assets/art_r19/*.json in the parent's include_filter so attribution ships; export_filter=all_resources includes the embedded Texture2D resource. No export preset was changed here.

Native fixture: source/tests/capture_hosted_art_scores_r19.gd. Other tests: art_invariants_r19.gd, sign_orientation_r19.gd, art_resource_paths_r19.gd, alpha_score_guards_r19.gd, alpha_score_integration_r19.gd. Tools and further source evidence are described in source/docs/ART-SCORE-R19-FINAL-REVIEW.md and ALPHA-SCORE-FRAME-INVESTIGATION-R19.md.

## Coverage and preservation

All105maps were scanned for the supported exact landscape/vehicle source profiles. Eleven matching maps: alfa-aglae-souza; bigbabyjesuz-classic-kiki-80; classic-bigbox-vr; classic-bw-187norvis; cont-cl-er-ssd-cjcoops; cont-cl-ssd-cjcoops; evolving-map-bigbox; ga-fenix-ga-hikesa; phoenix-bigbox-vr; statmaster-classic-soulsmight; statmaster-evolving-soulsmight. Baked totals:200 landscape plates,36 decks,60 rails,81 steel components,30 wheel components. These are selector/component counts, not complete repaired-vehicle counts.

The complete exact-sized town frame signature was independently scanned across105maps, all materials and world/roof layers: only the three enabled Alpha frames match. Other frame sizes, tilted/scaled/differently fragmented or omitted widget/display families remain unsupported; metadata absence is not an absence proof. R18 accepted Paintball/MellyPop behavior remains intact.

All11art maps pass exact ordered-face preservation, original collision resource identity/layers/masks, source render transforms/layers/groups/shadows, fingerprint/mutation rejection and idempotence. Remainders preserve encoded attributes through the accepted R18 subset helper. Selected surfaces decode/re-encode normals: tested vector error<0.0002; byte-identical selected normals/attributes are NOT claimed. Intentional generated UVs replace only matched surface UVs. Source geometry, floors and original model/image bytes stay intact.

Headless actual Alpha READY, hosted score authority and unload tests pass. Isolated PCK resource loading passes without raw PNG or import cache: raw_png_present=false, embedded ETC2 image with10mip levels. No raw PNG read occurs in production material loading.

## Exact remaining work

The rear truck behind cab source(-359.4039,36.3051,280.6427) retains an entire white flatbed/chassis; the improved foreground cab is(-368.38675,36.3048,303.1438). Remaining bed center is approximately(-359.69061,35.31114,267.58936), yaw1.942degrees. Its raw deck and rails already appear in first-cohort selectors, so do not equate the white cover with a missing deck selector alone.

Raw vc upper box fragments:2+4triangles, centers(-359.93904,35.45060,267.58936) and(-359.69061,35.45060,267.58936). Lower chassis fragments:3+7triangles, centers(-359.88634,34.92807,267.61842) and(-359.49687,34.92807,267.61842). First raw steel profiles expect complete6/10triangle components. Confirm current live groups and upper occluding surfaces, then exact fragment-union/cab-context selectors and all-map bake/guards before enabling. No unfinished rear-truck selector was added. Evidence: evidence/rear-truck-components-r19.json.

Tall portrait roadside plates remain pink and outside the landscape cohort. Give them original portrait-proportioned art later; do not stretch landscape posters or recolor pink walls/neon. Other vehicle variants and other-map display families remain partial/unknown.

## Evidence and acceptance limits

Viewed final native evidence: evidence/hosted-native-uv2-scores-r19.log/.txt; R19-AFTER-UV2-sign-head-on.png and .stripe.json; sign-oblique.png; score-context.png; score-close-0/1/2.png; score-opaque-depth.png. Earlier successful truck/van views: R19-AFTER-cab-b.png and R19-AFTER-van.png. Every BEFORE image remains intact. Defective mirrored AFTER images/logs are preserved both at original paths and under evidence/defect-sign-mirror/.

Headless evidence: invariants-art-r19-final.txt; isolated-packed-resource-r19.txt; sign-orientation-exterior-r19.txt; alpha-score-integration-r19.txt; alpha-score-guards-r19-final.txt. Scans: town-score-frame-family-audit-r19.json; alpha-affine-frame-probe-r19.json; alpha-live-score-frame-candidates-r19.json; alpha-score-neighborhoods-r19.json; alpha-frame-rays-r19.json.

New artwork/UV/code are original replacements, not reconstructed unavailable original art. Original credits and archive UNKNOWN REUSE LICENSE / LOCAL ONLY restrictions remain. Model/image originals were not mutated; no publication/push. Attribute manifest is source/assets/art_r19/ATTRIBUTION.json. Arial was locally rasterized; no font binary is embedded.

Desktop Vulkan warns ETC2_RGB8 unsupported and converts the shared texture to RGBA8; rendering is correct on this PC. Final APK resource inclusion, physical Quest rendering/performance/compression/acceptance and WAN behavior remain unverified and parent-owned. PC native/headless proof is not Quest acceptance.

## Short history

Initial actual-source probe found zero R17 hash skips, ruling out the hypothesized marker-before-art failure for this workspace. First native AFTER showed successful deck/rim changes but mirrored signs. The one-expression UV correction passed exterior tests and then independent native cyan-left/readability proof. Source frame investigation found scaled complete64triangle families missed by the old fixed-dimension search; three Alpha brick-façade apertures were recovered and independently native-approved. These final results supersede all earlier pending/failure language in historical evidence.

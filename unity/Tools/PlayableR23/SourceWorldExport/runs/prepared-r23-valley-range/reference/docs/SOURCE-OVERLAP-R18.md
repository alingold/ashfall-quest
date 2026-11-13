# SOURCE OVERLAP - bounded read-only handoff

Completed 2026-11-01 before 19:40 MST. Source checkpoint remains bbbd55183579fe288d6c7b31902d07890bec451f, clean and unchanged. No code/catalog/pack/build/asset changes, commits, Godot/GPU/device/VR/ADB execution, delegates or queued work. Only Node/Python parsing and read-only Git were used. Outputs are confined to the two authorized source-overlap-audit directories.

## Finding at supported strength

Large, spatially co-located coordinate-geometry families already exist in the RAW SOURCE exports. Melis shares 2,031,446 exact unique triangles with Banana and 1,934,383 with Bamboo. Banana/Bamboo and the three-way intersection share only 32,776, exactly equal to the same-template GSpot control baseline. Thus the larger Melis families are not explained by the reported shared glyph count alone. This establishes raw-export geometry provenance and co-location, NOT author intent, an unintended second arena, or a safe deletion selector. No source-author blame is justified. ASHFALL importer, map unload and runtime/device behavior were NOT tested by this audit.

NO authoritative SAFE separation/removal selector was proved. There is no per-arena/object membership, visibility or role metadata selecting one of these large families. Whole-node/material/layer/template deletion would lose other source content.

## Per-map inventory

Raw triangle totals include all source mesh groups. World/roof counts are actual GLB triangle counts; map.json layer summaries have mixed units (world_triangles versus object-count fields) and must not be mistaken for per-triangle membership.

| Map slug | Source template | All raw triangles | World triangles | Roof triangles | Nodes |
| --- | --- | ---: | ---: | ---: | ---: |
| banana-land-gods-pitdevil | MapEditorSnow | 3,582,511 | 3,570,659 | 11,084 | 3360 |
| bamboo-gods-pitdevil | MapEditorSnow | 2,686,257 | 2,626,615 | 57,762 | 1698 |
| beast-mode-gods-pitdevil | MapEditorSnow | 2,143,614 | 2,126,732 | 12,226 | 2281 |
| chem-x-template | MapEditorDefault | 516,266 | 513,902 | 268 | 1064 |
| cold-hearted-gods-pitdevil | MapEditorSnow | 2,563,529 | 2,542,833 | 16,296 | 2958 |
| dark-carnival-gods-pitdevil | MapEditorSnow | 2,712,974 | 2,688,230 | 20,484 | 3219 |
| melis-cafe-gods-pitdevil | MapEditorSnow | 4,694,812 | 4,681,096 | 12,988 | 3314 |
| g-spot-gods-pitdevil (control) | MapEditorSnow | 650,662 | 223,135 | 427,065 | 1140 |
| battlefield-paintball-gods-pitdevil (control) | MapEditorDefault | 453,011 | 447,897 | 5,086 | 570 |

Exact full SHA256 hashes and byte counts for source/map.glb, source/map.json, source/provenance.json, converted/manifest.json, derived source/geometry.glb and converted/map.scn are in evidence/source-overlap-audit/source-hashes.csv and each per-map JSON. Layer/material counts and AABBs: layer-material-counts.csv. Actual GLB material-index/name groups are separately recorded in supplementary-material-volume-inventory.json; source node material suffixes and actual GLB material references are not conflated.

## Exact sharing and controls

Full nine-map matrix: comparison-matrix.csv / comparison-matrix.json. Seven reported maps plus GSpot (unreported/known-good Snow control) and Battlefield Paintball (unreported overlap / same Default template control).

- Snow reported pairs generally share a 32,776-triangle baseline, also present with GSpot. Chem X / Paintball share 32,776 under MapEditorDefault. Cross-template pairs commonly share only 8.
- Additional substantial exact pairs: Bamboo/Melis 1,934,383; Beast Mode/Coldhearted 1,252,279; Beast Mode/Dark Carnival and Coldhearted/Dark Carnival 566,955 each.
- Banana/Melis: 2,031,446 shared unique nondegenerate triangles (2,031,550 minimum multiset occurrences), 56.77% of Banana and 43.28% of Melis unique geometry. Shared AABB approx [-593.177,-76.856,-605.281] to [608.042,422.329,596.609]. 1,951,631 common triangles have centroid y in [50,100).

Three-way exact accounting inside Melis:
- Banana-only family relative to Bamboo: 1,998,670 unique triangles.
- Bamboo-only family relative to Banana: 1,901,607.
- Shared by all three: 32,776, exactly the Snow/GSpot control baseline.
- Union of Banana/Bamboo families in Melis: 3,933,053.
- Melis unique geometry in neither: 760,378 (Melis total 4,693,431).

Banana-only family AABB approx [-593.177,-30.437,-605.281] to [608.042,422.329,596.609]; 1,951,492 centroid-y triangles in [50,100). Bamboo-only family AABB approx [-593.258,17.361,-604.501] to [606.743,610.604,595.501]; 1,866,908 in [50,100). Their broad AABBs overlap by approximately 1199.92 x 404.97 x 1200.00 source-coordinate units. This is AABB co-location, not a triangle-intersection/BVH result or intent proof. Full common/unique AABBs, vertical bins, layer distributions and area totals are in three-way-banana-bamboo-melis.json and detailed pair JSONs.

## Chunk mixing makes broad selectors unsafe

Banana/Melis have shared and map-unique triangles blended inside 586 Banana and 713 Melis primitive instances. Central concrete example in Banana: world_-1_1_p2a61c272 has 40,114 common + 47,975 Banana-unique occurrences in the SAME primitive (88,089 total). Its AABB is approximately [-60.767,42.463,-120.611] to [0.760,91.055,-59.738]. Deleting that whole node/material chunk would remove both categories.

Melis has 645 primitive instances containing BOTH distinct Banana-only and Bamboo-only families. Its world_-1_1_p2a61c272 contains 40,114 Banana-family + 33,032 Bamboo-family + 9,494 neither-family occurrences in one primitive. Membership is not encoded as separate scene objects or author roles. Exact triangle fingerprints can support a future deliberate investigation, but are not themselves authority to remove content.

## Scene, layer and volume boundaries

All nine raw GLBs have one selected/default scene (index 0), all nodes reachable as scene roots, zero child edges, no node transforms, no mesh nodes outside the selected scene, and no inspected extras/visibility/source-role grouping fields separating arenas. Therefore no alternate/default-scene or hidden-subtree selector was found. Source group names world, roof, false, trigger, killzone, boundary and playfield are recorded. A node literally named false and a numeric map.json layers.false count are NOT an explicit visible=false field or an arena ownership selector.

There are ZERO exact orientation-insensitive nondegenerate world-versus-roof duplicate triangles in every audited map. Both layers contain substantial unique geometry. Banana/Melis roof counts are 11,084/12,988; their pair contains both common and unique roof content. GSpot control has 427,065 roof triangles, more than its 223,135 world triangles. This demonstrates that removing all roof geometry is not supported; source roles alone do not establish which features are legitimate or unwanted. Zero exact duplication does not rule out near-duplicates, offsets or intersecting surfaces.

Boundary/playfield meshes are inventoried with their AABBs and repeated-coordinate counts, but do not authorize clipping/deletion. For example Banana/Melis share a narrow playfield AABB approx [-41.491,39.999,-58.462] to [54.200,50.975,48.893], while recorded spawns span Banana [-208.63,58.01,-321.65] to [184.64,83.61,247.06] and Melis [-284.91,58.19,-321.65] to [323.91,91.96,271.41]. Much recorded spawn/source content is outside that playfield AABB. Do not infer invalid scenery from this. Repeated boundary/playfield coordinate triangles are also present in controls (including orientation-insensitive boundary duplicates), so multiplicity alone does not identify unintended arenas.

Raw map.glb and derived geometry.glb have separately recorded hashes and the same per-source-group triangle counts in all nine cases. Derived geometry is not relabeled as original source. Converted map.scn was hashed and its manifest inspected only; it was not loaded or executed. This audit cannot establish runtime importer/unload acceptance.

## Method and reproducibility

Scripts (only authorized work directory): audit.py, supplement.py, structure.py. Run with C:/Users/aling/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe -B and the corresponding script path. They read the fixed original local_archive root and write only the fixed authorized evidence root. -B avoids bytecode cache writes. No semantic_scan main CLI was invoked.

Comparisons use selected-scene world/roof TRIANGLES with area > 1e-10. Exact decoded float32 coordinates, no node transforms found, signed zero canonicalized. Each triangle's three vertices are numerically sorted x/y/z, ignoring winding, material and layer. Full 36-byte coordinate keys are compared, NOT probabilistic hashes; no coordinate quantization/tolerance. Multiset intersection uses minimum occurrence counts. Spatial bins use unique-triangle centroid y. Different triangulation, shifted geometry, UV/color/material differences and near-duplicates are outside equality claims. Orientation-insensitive boundary duplicates can be intentional two-sided geometry. No raw vertex/signature arrays were saved.

Evidence entry points: summary.csv/json, source-hashes.csv, layer-material-counts.csv, comparison-matrix.csv/json, three-way-banana-bamboo-melis.json, detailed Banana/Melis and Bamboo/Melis pair JSONs, per-map inventories, raw-versus-derived-structure.json, supplementary-material-volume-inventory.json and method-and-source-status.json. No massive unfiltered vertex dumps.

## Outcome

The raw exports already contain the measured large shared/co-located families, and those families are blended with other content inside source chunks. Common original template scenery/props are not themselves defects. Original intent remains unknown, and no metadata-backed safe separation selector is proved. A future repair needs an explicit desired-content decision and bounded validation; no removals were implemented or proposed as safe here.

Source Git status verified unchanged/clean at bbbd55183579fe288d6c7b31902d07890bec451f. No queued work. All handoff prose is ASCII-safe UTF-8 without BOM.

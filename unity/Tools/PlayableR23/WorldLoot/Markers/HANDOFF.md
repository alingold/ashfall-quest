Staged repair: recovered red-cache authoring symbols, 2026-11-05.

Ready outputs are `StagedPayload/<slug>/geometry.bytes` and `map.json` for
`battlefield-paintball-gods-pitdevil` and `alfa-aglae-souza`. No live Assets,
original source, Loot/caches.json, textures, or runtime files were changed.
`prepare.py` refuses different live geometry hashes and never overwrites an
existing staged directory. Use `--inspect` for source analysis without payload
writes, or `--output <new-child-directory>` for a fresh stage.

| Map | Render before → after | Collision before → after | Changed chunks |
| --- | ---: | ---: | ---: |
| Paintball | 452,983 → 97,975 | 452,481 → 97,473 | 10 of110 |
| ALFA | 1,107,952 → 760,168 | 1,037,013 → 689,229 | 25 of2,144 |

Every changed chunk contains only confirmed `p2a61c272` authoring symbols.
Both render and collision flags become0. Encoded vertex/index data remains
inert: the existing loader requires at least three indices, so no degenerate
placeholder or chunk reindexing is introduced. The full map.json files and
all material, vertex, normal, UV, color and index bytes remain identical.
All2,219 nonmarker chunks are wholly byte-identical; ALFA's930 destructible
definitions and every chunk-panel/glass membership retain their original IDs.

Original `archive_semantics_r8.gd` classifies336 Paintball/331 ALFA rings and
4,127/4,038 question components. Exact fingerprint-bound recovered r17 rows
cover the remaining19/27 prepared components. Raw recovery matches all128/99
selector rows by original count/center and bounds. The two ALFA R18 supplemented
rows use their explicit69+35 and61+27 source members; no broad bounds-only
removal is used. Union removal is355,008/347,784 triangles in each active role.

`marker-removal.json` records original/staged hashes, immutable source script
and data hashes, cache fingerprints, exact selector matches, source assembly
summary, triangle-key hash, changed chunk numbers and per-chunk before/after
hashes. Source math is read from original `archive_semantics_r8.gd:46-66`,
`map_semantics_r14.gd:35-68,91-100`, and `map_caches_r17.json` (data, not a
standalone script). Original `recover_caches_r17.cjs` explains split-row recovery.

`python unity/Tools/PlayableR23/WorldLoot/Markers/check.py` passed. It independently
reads the original hash-receipted prepared geometry via the cache exporter's
read-only helper, classifies every component, compares the complete reflected
source triangle-key set, and reads every staged chunk. It verifies the exact
active triangle census, all unchanged bytes/attributes/mappings, loader ranges,
and unchanged live hashes. No engine, GPU, render or device execution occurred.

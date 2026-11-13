# R18 source surface coverage

Local-use archive restrictions and existing credits remain. Original source model/image/collision bytes are unchanged. New vehicle/concrete detail and scoreboard UI are original procedural work; unavailable original artwork/scripts are not claimed reconstructed. PC/headless evidence does not establish physical Quest acceptance.

## Source score displays

| Map/family | Enabled recovery | Evidence/limits |
| --- | --- | --- |
| MellyPop central Megatron | Four existing exact source faces/UVs/positions; reference-inspired navy rows and board-local late alpha1 draw with ordinary depth testing | All four native orientations are clear; source FX remains outside display. Opaque BoxMesh completely hides board. Real server score/cap/countdown and mode/layout tests pass. |
| Battlefield Paintball high Welcome lobby | Original complete 64-triangle frame at (-3.960614,78.74280,26.23660); one generated two-triangle display inside its exact original aperture | Native before/after proves fit beside actual Welcome sign. Data SHA, map fingerprint and both frame-chunk hashes guard recovery. READY/host/start/unload tests pass; cached pack details unchanged. |
| Other maps | No new guessed screen surfaces | Full 105-map, all-material/world/roof scan finds only Paintball's complete measured frame signature. Four dimension candidates fail full shape/part signature. Other widget families, scaled/tilted/fragmented frames or omitted placements remain unsupported. |

FFA reads peer-ID scores and player names; team mode covers configured/current-roster teams. Stable ranks, cyan names, white scores, ASCII UNLIMITED for zero caps/time, and all eight teams plus eight active players fit the 1280px canvas. Initial badges are explicit fallbacks because no current-skin 2D thumbnail is available in the UI path. No rocket-only scoring is invented. Reference screenshot is not game art.

See SCOREBOARD-EVIDENCE-R18.md for exact source locations, source material/chunk identifiers, metadata/script inventory, rejected candidates, renderer/depth evidence and merge hook.

## Enabled R18 prop additions

| Source map slug | New cab assemblies | Complete concrete roadblock assemblies | Roadblock draw chunks |
| --- | ---: | ---: | ---: |
| alfa-aglae-souza | 1 | 23 | 4 |
| bigbabyjesuz-classic-kiki-80 | 1 | 23 | 4 |
| classic-bw-187norvis | 1 | 23 | 4 |
| cont-cl-er-ssd-cjcoops | 1 | 23 | 4 |
| cont-cl-ssd-cjcoops | 1 | 23 | 4 |
| statmaster-classic-soulsmight | 1 | 23 | 4 |
| classic-bigbox-vr | 0 | 35 | 5 |
| evolving-map-bigbox | 0 | 35 | 5 |
| statmaster-evolving-soulsmight | 0 | 35 | 5 |

Six cabs were delivered in 8db00b8/cda85c3 and independently reviewed by coordinator. Each map now has eight repaired truck assemblies. The native after view confirms fitted windows/paint on the formerly blank 44-source/78-converted-triangle cab.

The complete 26-triangle/16-vertex barrier profile matches 243 assemblies across nine maps. Source color remains the base; shared restrained aggregate adds no symbols, lights, alpha or unique textures. Encoded source buffer subsets preserve exact normals/colors/UVs without re-encoding. All ordered triangles, collision identities, layers, masks, groups, hash rejection and idempotence pass. Quantization bounds remain conservative; supported helper format is tested Godot 4.7 static single-surface ArrayMesh. Native after view confirms shader compilation and intact barrier geometry.

Existing R17 wood/vehicle/tire/junction coverage remains. No broad neutral/pink/white selector is used. Full candidate inventory is evidence only, not a missing-art defect count.

## Actionable unresolved prop families

| Family | Source fixture | Current evidence/reason | Next action |
| --- | --- | --- | --- |
| Roadblock fragments | vc body20 at (-541.43,49.14236,78.46051), cap6 at (-539.46872,49.14236,78.83842) | Body has complete barrier's 16-point shape within 0.00006m pairwise error; cap vertices are exact subset of body's end vertices. Separate converted components persist. | Uncommitted exact fragment profile work paused for scoreboard priority; finish bake/invariants before enabling. |
| Stop/octagon signs | vc (-51.6442,27.89994,104.98082),6 triangles/8 vertices | CPU context proves industrial pipe end; regular octagons also include wheel ends. | Rejected for traffic-sign painting. Need actual plate/post context. |
| Cylinder/barrel | vc (50.404,23.20443,237.60599),36 triangles, by red barn | CPU/native show plain cylinder atop crates; barrel-versus-sack/material intent is not fully proved. | No hoops/labels applied; identity audit remains. |
| Lamps/poles | vc (-14.78533,31.47199,214.93022),4 triangles/6 vertices | CPU shows structural rod near building; native camera was occluded by wall. | No lamp head identified and no lighting replacement enabled. |
| Fences | Existing R17 exact wood selectors and Alpha breakable wood coverage | No newly proved missing family in this pass. | Keep inherited coverage; identify precise remaining variants. |
| Other vehicles | Existing R17 profiles plus six proven cabs | Remaining neutral trailer/mechanical surfaces are not sufficient material/missing-art proof. | No speculative profile enabled. |

## Reproduction

Use bundled PowerShell7 with own work/run.ps1, headless/XR-off/Dummy except fresh coordinator-granted native slots. Score tests: score_presentation_r18.gd, score_integration_r18.gd, score_authority_integration_r18.gd, paintball_frame_probe_r18.gd, paintball_score_integration_r18.gd. Prop tests: roadblock_invariants_r18.gd, art_guards_r18.gd, cab_uv_r18.gd. CPU context tools: prop_context_r18.cjs and render_prop_context_r18.py (textured source surfaces use diagnostic gray and are not missing-art proof).

GPU release and current native evidence are documented in workspace work/GPU-SLOT-RELEASED.txt and NATIVE-SCORE-RESULTS.txt. No further native execution without a fresh grant.

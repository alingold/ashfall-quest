# R19 LOD sensitivity checkpoint — HEAD a262d80

Recommendation: KEEP production rootLOD6 for this build. Threshold8 is only a possible future controlled Quest trial; do not promote10 from these static PC captures. No production policy/cache/geometry/collision changes in this checkpoint. Bounds prototype remains OFF. All earlier commits8b827e9,4c5dd15,b606334,5090c0a,849f480 intact; a262d80 adds benchmark/probes only. Working tree clean. No parent edits/push/device operations. Exploration stopped; parent integrating/building.

Native authorized slot:2026-11-02T04:12..04:17Z; all owned children finished. Coordinator reports final native end04:16:06Z and release04:16:16Z; Sol observed successful completion/reconfirmed release04:16:27Z. No further native authorized or launched. Unrelated C skins_pipeline renderer was never touched; GPU timings remain confounded and are not a benefit claim.

Controlled all-eight-heading comparison (same authored lobby eye(57.63441,47.7832,82.86377),1600x1000 Mobile Vulkan,MSAA4x,FOV85,far4887.2412m,XRoff/Dummy,postcut accepted30/rejected0,bounds0;32warm+32measurement frames per heading):

| Root threshold | Summed visible primitives | Summed draws | Reduction vs repeated6 |
| --- | ---: | ---: | ---: |
|6 repeat|3325818|3284|reference|
|8|3257914|3284|2.042%|
|10|3191947|3284|4.025%|

Sums compare eight views, not one-frame budgets. The6repeat exactly reproduces prior production6 all eight views. INITIAL new6 run is retained:3360304 primitives/3365draws; extra draws at135/180/225/270 were495/383/226/198 instead of465/344/217/195. Exclude its full-sum difference from threshold attribution. Rootgame PROCESS_MODE_DISABLED does not stop already queued coroutines/always nodes. Its before-override viewport list included temporary Archive overview1024, which later disappeared. Do not call that an optic change. Matching-draw rows0/45/90/315 were stable even on the first pass; final all-eight comparison uses repeat. Read-only four optical viewports in final snapshots retainLOD1/updateDISABLED(0); other menu/map viewports are also listed. No scope setting is written.

Qualitative static image assessment: broad lobby images show foreground roof/major skyline stable at reviewed headings. Coordinator specifically reviewed6repeat vs10yaw90 and observed tree crowns/background foliage and tower white bracing visibly simplify. This is expected detail loss, not a failed invariant test or evidence of no LOD change. No temporal popping, binocular/sniping or Quest performance acceptance. Savings2.04%/4.03% do not justify a production change for this build.

CPU distance references from exact scene base-face intersections at the same eye/FOV85/keep_aspect1 and1600x1000 projection:
- Roof region yaw90 pixel(800,750):4.3695m,Draw_world_s15_0_-1,surface0,triangle1643.
- Tree region yaw225 pixel(880,500):75.7460m,Draw_world_s41_0_-1,surface0,triangle2144.
- Tower wall region yaw45 pixel(400,250):195.5306m,Draw_world_s13_-1_-1,surface0,triangle121.
- Distant hillside region yaw225 pixel(700,450):447.2359m,Draw_world_s0_0_-1,surface0,triangle28.
- Wind-tower intended ray yaw90pixel(330,350):MISS retained; no distance invented.
Labels describe selected screenshot regions, not authoritative semantic mesh names. Ray hits are nearest base faces in map._geometry; ignore alpha texels/backface rules/selected LOD/shader displacement/non-map fixtures. First CPU attempt mistakenly used dummy1:1viewport; rejected and replaced by explicit1.6aspect math. Corrected report R19-LOBBY-DISTANCE-PROBES.json preserves full coordinates and limitations.

Benchmark API: tests/render_cost_r19.gd accepts only --r19-lod-threshold=6/8/10(default6), applies solely root.mesh_lod_threshold AFTER map load. PNG/JSON tags include root threshold and lobby origin. --r19-rerun preserves first-pass outputs with separate tag. Output stores before/final SubViewportLOD/update modes and cacheaccepted/rejected counts. Root-only counters exclude independent scope render counters. Headless argument/scope-isolation test passed; valid native benchmark refuses headless(exit2), invalid threshold refuses before renderer gate(exit3). PowerShell prepared-script syntax check passed. Native captures parsed/ran all requested threshold paths. No additional tests/exploration required.

Evidence: own evidence/R19-COST-alfa-aglae-souza-postcut-lod{6,8,10}-lobby.json/PNG; controlled repeat R19-COST-alfa-aglae-souza-postcut-rerun-lod6-lobby.json/PNG. First-pass6 files retained. Counters CSV R19-LOD-SENSITIVITY-COUNTERS.csv includes first-pass rows (do not silently treat6 as repeat). Scope settings and per-heading counters remain in rawJSON. There are32 sensitivity PNGs including repeat.

Prepared reproduction command (DO NOT run without a NEW explicit grant; prior slot expired):
```powershell
& 'C:/Users/aling/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe' -NoProfile -File 'D:/Codex/ASHFALL-R19-Quest/work/sol-render/native-lod-sensitivity.ps1' -GpuGranted -SlotEndsUtc '<new coordinator-granted UTC deadline>'
```
Script validates deadline<=5min, requires>=50seconds before each child, runs6/8/10 sequentially with45second childtimeouts. Expected~2minutes, maximum~135seconds of children plus small overhead, within fresh<=5minute slot. Uses postcut enabled, explicit lobby,PNG,root controls; no bounds flag. No request for another slot.

Integration remains as reviewed in RENDERING-R19-HANDOFF.md and RENDERING-R19-PARENT-HOOK.md. Parent MUST rebake only Alpha after final ART using --no-postcut-lods-r19 and explicit finalProject path, then check actual accepted/rejected coverage. Diagnostics runtime API unchanged: tick(delta,viewport,interface,map_slug,stage,scope_active,gpu_ms=-1,lod_state={}); actual supportedGodot4.7.2 getters, no scene scans/IDs/audio/PII,rollingCAP128~128seconds at~1Hz,separate user://logs/render-diagnostics-r19.json. Approved parent main insertion remains separate and untouched. Cache hash/schema/engine/source/topology guards and exact base/collider checks remain intact.

Final SHA256:
- tests/render_cost_r19.gd:6BA82F7F01C12FD42C60508B6E089805296DC5036B497E6C38800B70918ACBB3
- unchanged scripts/quest_render_r16.gd:318D81F1C21D98953FD2C0A38062989A00DB490A9278A459D7769DD9AF8C1E33
- unchanged scripts/postcut_lods_r19.gd:79E98428D136DF312CF60B13F21D6B3333608CA0CDFD1F11DAB36338ECA79112
- unchanged scripts/render_diagnostics_r19.gd:E6E226EC2EBA1E2E5649C25A76F0EBB5FB9CD5727AF7003AE243F824D845E7BF

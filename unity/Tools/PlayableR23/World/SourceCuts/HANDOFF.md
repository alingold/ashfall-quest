# Classic v003 source cuts — published; native behavior passed

Root published the guarded nine-file change. Native `floor-20261105-180826/classic-source-cuts.json` passes all718 break/reset,381 same-plane aperture checks and16 authored floor cases. Original337 and geometry/art data remain unchanged. Raw preview teardown fault was retained and fixed separately; the later `floor-20261105-181935` run has no production/shader/teardown errors, with the known Editor Search startup fault still present. Headset, full rendered parity and performance remain unverified. The implementation handoff below records the earlier staging/CPU state.

All source/payload candidates are staged and HELD under this directory. Live Assets and original geometry are untouched by this worker. Root may run:

```
python unity/Tools/PlayableR23/World/SourceCuts/publish.py
python unity/Tools/PlayableR23/World/SourceCuts/publish.py --apply
```

Default verifies only. The guarded9-file plan refuses any stale existing source/data hash or occupied new target; original map manifest is replaced last. Existing metas are untouched; new helper/blob metas supplied. `publication-plan.json` contains exact before/after hashes. Do not rerun prepare/stage after publication: baseline guards deliberately refuse silent rebase.

Implemented: real bounded binary parser/assembler `Destructibles/R24SourceCuts.cs`, map load/registry integration, barycentric SourceArt sampler, optional SourceArt normal-frame binding, and opt-in R24Architecture source-frame compensation. One final existing R23ArchiveDestructibles registry owns all718. Existing definitions0?336 and their geometry paths remain unchanged; new definitions337?717 are appended. No parallel damage owner, whole-larger-triangle claim or replacement geometry.

The2.495MB derivative retains supplied v003 barycentrics/source bindings and exact baseline map/tower/full geometry/architecture/SourceArt hashes. All27 input chunk vertex/index/header guards and10,907 raw-face references are checked. Each selected raw face is removed ONLY from the old static subset, with original337 owner theft refused. Static and dynamic emitted vertices share the exact same interpolation for render/collision. Old whole/static unrelated faces remain on their admitted paths.

Static render remainder retains original SourceArt classification (500 real clear-glass fragments) and R6 selectors. The TWO intersecting existing R6 mask partitions are clipped in the original barycentric domain; source mask tolerances/47 deferrals are unchanged. New VC owner fragments use original VC treatment (selector255), not R6 architecture paint. Source clear-glass material is retained via existing SourceArt. No source image/material data changed.

Precision: regular parts batch by source owner/chunk; compatible precision fragments batch within exact original source planes. Analytic plane coordinates avoid creating fake thickness from floating cancellation. All fragments are retained. The CPU checks use the same explicit Float32 quaternion supplied to Unity Transform, rather than merely double basis vectors. Per-renderer source-offset/local-to-map properties restore original coordinates BEFORE Z reflection; noncut default path remains zero/disabled. No shared material mutation. Cut collider cleaning/welding is disabled so CPU-positive local slivers are not intentionally discarded by those options.

Final pure assembly: 4,016 PASS checks;27 chunks,381 owners,29,289 emitted triangles (13,692 dynamic /15,597 static),1,698 mesh/collider parts,1,044 oriented batches. All10,907 raw areas conserve in source barycentric space; source input381 coverage preserved,492 unported/partial passages remain static. Existing47 v003 deferrals remain exactly deferred.

Actual assembled Float32/quaternion CPU checks: all triangle windings positive; source-world position residual <=0.00003242m, normal residual <=2.39e-7, UV/color <=2.23e-16. Per-original-face area residual <=1.08e-5 relative; maximum fragment absolute area error <=0.00006140m?. Extremely tiny fragments can have large relative area errors (max516.9) while contributing negligible absolute area; weights/source partitions are unchanged. World-coordinate retransformation can merge sub-ULP endpoints even when local collider vertices remain distinct. Native collider cooking, aperture queries and rendering are UNRUN and must qualify this; no native/HMD claim.

`check.py` exercises the ACTUAL parser/assembler and SourceArt plan on unchanged live source geometry, guards,718 append identity and failure controls. `check_interpolation.py` independently reconstructs source attributes/areas with the emitted Float32 quaternion. `check_api.py` compiles staged replacements against installed Unity6000.6 APIs, warnings as errors: PASS. No engine/shader compiler/GPU/device run.

Actual map read-only seams for Root's separate718 native route:

- SourceCutApplied (only after actual IsLoaded)
- SourceCutOwnerCount (381 added)
- SourceCutMeshCount / SourceCutColliderCount
- SourceCutOrientedMeshCount / SourceCutTriangleCount

Counts describe assembled runtime objects, not native success. Existing337-only route must remain distinct from opt-in718 candidate.

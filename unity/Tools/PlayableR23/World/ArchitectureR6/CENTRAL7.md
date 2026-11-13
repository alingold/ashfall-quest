The remaining seven central maps now load their genuine R6 architecture treatments through the existing runtime/shader. ALFA and Classic's finished coplanar payloads are preserved byte-for-byte.

| Map | Transferred faces | Patched chunks |
|---|---:|---:|
| bigbabyjesuz-classic-kiki-80 |24,198|23|
| classic-bw-187norvis |24,427|23|
| cont-cl-er-ssd-cjcoops |24,314|23|
| cont-cl-ssd-cjcoops |24,104|23|
| evolving-map-bigbox |43,620|32|
| statmaster-classic-soulsmight |24,427|23|
| statmaster-evolving-soulsmight |43,355|32|

Total208,445 source-bound faces across179chunks. Source brick/concrete/corrugated metal/wood/stone, original tint/roughness/vertex-color semantics and family road/trailer/wheel parameters are carried into each architecture.json. Five ALFA-like variants match actual junction7, trailer32..34 and wheel48..51 kinds. Evolving and Statmaster Evolving's frozen flags authorize junction7/trailer32 and no33/34/48+; no additional selectors were invented for the other parameter entries. Unmatched/ambiguous/different-tessellation faces beyond the bounded coverage rule retain supplied rendering; these counts are useful partial coverage, not full map-art parity.

Source binding is immutable R6 architecture config SHA `1cb3f22b3267a0d825eadbd3cc2b132f17740fdd0061a74ed5ff44e6e133644d`. Only WORLD's pure `exports()` function is read/executed to resolve the published folders; module setup/main and mutable source propagation are never run. Every plan path, transform and all four actual Main channel hashes match the frozen config. Prepared R10 adapter hashes match frozen archive_adapters_r10. The existing oriented three-corner5mm/RGB/normal matcher and bounded exact-union fallback remain in use.

Publication creates only each new map's architecture.json, independent local architecture/{brick,concrete,corrugated,planks,statue-stone,freight}.png copies/metas, and its architectureFile map.json link. The link is enabled last, after all resources exist. Atomic replacement preserves any prior hardlinked map.json snapshot; source/raw/historical texture hardlinks are never edited. All42 copied textures are exact source bytes and have a single local link. All seven original geometry/collision hashes and every other map.json field are unchanged. Existing ALFA/Classic architecture.json/map.json protected hashes remain identical.

Checks pass:86 actual immutable source-kind triangle witnesses across seven maps, all three corresponding RGBs/normals/corners/materials and original source last-corner classification; complete patch ranges/counts/geometry bindings; family parameter limits;42 exact independent texture copies; retained12 farm/union/window-hole/overlap controls. Receipts/counts/witnesses: central7-checks.json and per-map transfer files. No runtime or shader change, engine/GPU/device/build/Blender job, or native visual claim.

Reproduce with installed Python:

```text
prepare.py --remaining-central
check_central7.py
check_footprint.py
check_central7.py --publish
```

`--map <one of9exactcentralIDs>` remains available for bounded staging. `--remaining-central` excludes ALFA/Classic and checks their protected hashes. Root may now compare native source/Unity captures; sky/ambient, source R10/R17/R19 art outside architecture flags and other map families remain separate work.

# R6 archive map payloads

**Completed: all 102 additional archive maps published; zero refusals.** All 102 live binaries passed the final independent check. Together with existing Paintball/ALFA/Classic, all 105 source archives have payloads. The new genuine previews are 5 original converted PNGs and 97 original JPG thumbnails. New logical payload is approximately 3.24GB; C17.16GiB/D23.70GiB remained free after publication. No native/engine/build/device run was performed.

`catalog-delta.json` lists **published, independently CPU-qualified maps only**. Read `maps[].id/title/creator/relativeFolder/preview`; the folder is relative to `StreamingAssets/PlayableR23`. `preview.jpg` is the original archive thumbnail (the source runtime's fallback when `converted/preview.png` is absent). `nativeValidated:false` is intentional.

Baseline: frozen R6 content `23951bcc4c358a38a0fd1ad59fa24cef0171f5df6c6c2cc2e38dca5a095f5520`. Every consumed frozen script/data file is verified against the exact `SOURCE-MANIFEST.json` (`b62578d4…`). Each original GLB must also match the independently recorded R14/R9 semantic, conversion-manifest and provenance hashes; cache fingerprints must agree. Original files are read-only.

The seven central-family additions are `classic-bw-187norvis`, `cont-cl-er-ssd-cjcoops`, `cont-cl-ssd-cjcoops`, `statmaster-classic-soulsmight`, `bigbabyjesuz-classic-kiki-80`, `evolving-map-bigbox`, and `statmaster-evolving-soulsmight`. The existing Paintball/ALFA/Classic/Range/Valley directories cannot be targeted by this converter.

## Working behavior

- Real original glTF geometry, vertex colors, UVs, transformed normals, genuine albedo/normal textures and source StandardMaterial overrides. Reflect Z once; retain reflected clockwise winding. Repair only zero source normals geometrically.
- World/roof remain solid; boundary remains invisible and solid; false/walk-through remains visible and noncolliding. Playfield metadata is never a wall. Source killzone/trigger descriptors are preserved in `source-semantics.json`, not silently made solids.
- Source R8 red rings/question symbols and R17 cache symbols are retired by exact connected-component qualification. R14 local spawn/pad marker visuals remain visible **without collision**, matching `map_semantics_r14.gd:154–159/70–76`; decorative-cross collision is removed while retaining its art. No AABB blanket deletion. Source foliage material classification removes leaf collision.
- Source native marker or recorded spawn candidates require genuine floor, four footprint offsets, and a 1.9288m standing capsule (radius .3m). The source 19×19 floor grid is used when recorded candidates fail. No support plane or replacement platform is created. Both published spawns are checked again against the emitted bytes.
- New texture outputs share exact content via hardlinks **only between this batch's output files**; original source files and historical outputs are never hardlinked or changed. Each map retains local texture filenames for the current loader. NTFS's 1024-link cap falls back to a new exact copy. Treat these shared texture contents as immutable; replace a file when changing its pixels rather than modifying the shared file in place.

## Explicit remaining limits

These are usable **raw archive** payloads, not a claim of complete prepared-Godot-adapter parity. Source dynamic panel assembly/aperture repair, reconstructed authored waiting-deck colliders, fixtures, hazards, gameplay rules, distant foliage LOD and map-specific art repairs remain separate integration work. Real raw panels are retained statically, not deleted. R6 architecture flags are root-owned; `source-chunks.json` retains batch/node/primitive identities for that integration. Current v1 renderer retains its existing alpha-cutout/lighting approximation and double-sided collider policy. No native cooking/render/headset result is claimed by these CPU assets.

`source-semantics.json` also includes original template/center/spawn/bounds metadata and exact source environment descriptors (including Moon darkness). They are preserved data for root's environment integration; the current loader is not claimed to apply them. Valid source waiting spawns can be beyond combat X/Z bounds; preserve their separate lobby/game role rather than expanding the arena bounds.

The archive catalog has 105 maps; existing Paintball/ALFA/Classic cover three. This batch targets the other 102. Procedural `cyber_bird` is outside the raw-GLB archive batch; existing Range/Valley remain unchanged. `refused` contains exact errors rather than fabricated success if any map has no valid support, missing inputs, or invalid geometry.

## Commands

`python unity/Tools/PlayableR23/World/Catalog/prepare.py --central --publish` processes the first seven; `--all --publish` continues all remaining archives, one GLB/process. Already published directories are never overwritten. `--all` without `--publish` stages on D only.

`STOP` pauses publication between maps for root's capture windows. After the root explicitly releases the hold, remove that owned file and run `publish_staged.py --release-assets` to publish already qualified stages. `check.py` reads every published binary independently, verifies range/count/normal/role/attribute-census/texture/thumbnail integrity and real spawn clearance. Per-map provenance retains all source hashes and concrete limits.

No engine, GPU, Blender, APK build or device execution was used. Native follow-up should first load one central archive with authored lobby/game spawns and one floor-grid fallback archive, checking both entry positions and genuine preview selection. Root owns catalog/UI/loader changes.

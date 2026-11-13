Added genuine source catalog screenshots to the existing map cards:

- `archive:battlefield-paintball-gods-pitdevil` → `R23MapThumbnails/battlefield-paintball-gods-pitdevil` (BATTLEFIELD PAINTBALL, GODS_PITDEVIL).
- `archive:alfa-aglae-souza` → `R23MapThumbnails/alfa-aglae-souza` (ALFA, aglae_souza).
- `archive:classic-bigbox-vr` → `R23MapThumbnails/classic-bigbox-vr` (Classic, BigBox VR).

All three are original800×450 JPEG bytes copied unchanged from the existing archive, matching both the original installed PCK selected-payload MD5 and original thumbnail download receipt SHA256. The exact catalog resolver is preserved Opus R23 `scripts/map_catalog1000.gd:16–20` → `archive_catalog1003.gd:44–50`: use `converted/preview.png`, otherwise `source/thumb.jpg`. These three have no converted preview in the archive pack, so the source JPEG is the genuine active-source fallback. No screenshot was invented or rendered. Full paths, source script hashes, catalog IDs/creators/fingerprints, payload hashes and receipts are in `provenance.json`.

`R23MapOption` now supplies these resources automatically for the canonical archive IDs. Existing `R23VrUI.MapThumbnail` already draws option resources, so no Root or VRUI method change is necessary. Existing immutable API, explicit resource overrides, source card dimensions/crop, Range/Valley/Cyber Bird PNGs and all menu transitions are preserved. Root's new canonical Classic catalog choice picks up its image automatically through `new R23MapOption(id,label)`. Unknown maps retain no image rather than receiving an invented substitute.

New importer metadata preserves full RGB, exact800×450 non-power-of-two dimensions and RGBA32 uncompressed Standalone/Android formats; root's existing MapThumbnails import preparation also covers the new files. Only `R23UiState.cs`, three new thumbnail JPEGs/metas, and this disjoint tooling directory changed. No edits to Root/World/Editor; no installed APK change.

`python unity/Tools/PlayableR23/UI/ArchiveThumbnails/check.py`:9 map-option/selection cases plus all three source JPEG bytes/receipts/resource/import metadata pass. `python unity/Tools/PlayableR23/check_integration.py`: installed Unity Runtime/EditorRuntime/BuildAndSmoke API compilation passes. Actual Unity import/render/device remains unrun; no engine/GPU/build job was started.

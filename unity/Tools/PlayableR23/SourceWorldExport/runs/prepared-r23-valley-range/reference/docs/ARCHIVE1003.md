# Complete public map archive — local adapter

This isolated branch extends preserved 1002/1001. Imported maps, textures, metadata and derived caches are local-only under `local_archive/`, ignored by Git/Godot editor scanning and explicitly excluded from exports. No reuse/redistribution grant is established. The source-code license does not license imported map assets. Do not put those assets in GitHub, releases, distribution bundles or Library. The user separately authorized a private exploration video/report.

## Catalog and loading

The downloader enumerates the current public `https://maps.deliciouswines.org/maps/index.json`; counts are data-driven. Every entry is represented with its title, supplied creator attribution, preview and status. Official/community labels are source metadata, not an independent ownership finding. The selector reuses four cards with Previous/Next paging, keeping only their thumbnails in memory. Failed/incomplete entries remain visible and disabled with diagnostics. Geometry loads only when a supported map is selected.

Map IDs are `archive:<slug>`. One converted scene is active at a time. Existing Valley, Range and Cyber Bird remain separate choices. Launch from the owner folder's `Launch-Desktop-Archive.cmd`, or `Launch-VR-Archive.cmd` only when the user chooses to start VR. Automated work uses XR off.

## Conversion and integrity

The original tools in `tools/archive_pipeline/` inventory public file sizes and disk capacity, use two polite download workers, resume partial files, verify hashes/lengths and deduplicate shared textures. A 50 GiB free-space reserve is enforced. Thumbnail URLs come from each index entry; missing previews are rendered locally from the actual converted map. No downloaded scripts execute.

GLB normalized integer vertex attributes are expanded to ordinary floats before Godot import. In the captured archive, positions/UVs are floats, normals are normalized signed bytes, and some vertex colours are normalized unsigned bytes. Native glTF right-handed Y-up metre coordinates are preserved; the Babylon viewer's renderer-side mirror is not applied. Materials are rebuilt from external colour/normal texture references. Shared derived texture/material resources avoid embedding duplicate copies in every scene.

Render meshes merge by material and 240 m cell; world/roof collision remains in 60 m tiles. Source roofs/world geometry stay solid, with back-face collision for thin surfaces. Boundary barriers stay physical/invisible and are filtered out of the player's climbing query and floor-placement rays. Walk-through decoration is non-solid. Playfield is metadata, not a solid volume. Connected kill-zone components become convex, query-only safe-recovery sensors; trigger components are query-only without original scripted effects. This is a technical ASHFALL adapter, not a recreation of arbitrary original map scripts, native pickups, win rules, bot navigation or campaign logic. Concave/overlapping authored sensor behavior may need manual refinement.

Each map has a deterministic semantic identity from original geometry, canonical metadata and source texture contents. This is separate from Godot's binary serialization, which can differ across equivalent conversions. A local cache graph digest verifies actual scene/material/texture bytes before loading. Multiplayer additionally checks the selected-map identity on the loading acknowledgement and rejects mismatches. Matching library snapshots and source revisions are required on peers.

## Spawn, loot and lifecycle

Recorded spawns are snapped to real floors, checked for player headroom and hazards, and rejected outside recoverable bounds. Missing valid spawns fall back to a deterministic standable grid near the map center. Loot sampling uses the intersection of solid geometry and play-boundary footprints; invisible boundary geometry never becomes a floor. This prevents background/skybox floors from receiving unreachable loot. Validated points feed both ordinary placement and authoritative multiplayer spawn selection.

Existing ASHFALL weapon, five-ammo-family, build-supply and heal providers populate supported maps. Decorative source-map guns/items do not become functional pickups. Placement/reset/teardown reuse existing item logic and retain inventory/reload state handling. Outside/fallen players recover safely; network recovery is host controlled. Loot routes remain a heuristic, not an automatically generated navigation mesh.

## Validation

`tests/archive_validate1003.gd` loads the actual game, verifies cache integrity, floor/headroom/hazard/bounds-safe spawns and loot, provider counts/reset, removal on transition to Range and repeated map loading. `matrix1003.py N --reuse-conversion` runs a resumable per-map matrix through absolute index N. It reuses only a previously successful, hash-verified conversion when the converter is unchanged. Each owned child has a timeout and an 8 GiB working-set cap; use short batches. A work-root `STOP-MATRIX` file checkpoints after the active map.

Separate fixtures check all paginated selector actions, no geometry preloading, synthetic cache-byte corruption rejection, actual player grip-query behavior, and small/large/unusual full-game network loads/rematches. Network input and mismatch claims are explicitly scripted. `tests/archive_tour1003.gd` records real rendered views with scripted camera motion and rendering counters.

The final owner report and per-map manifest distinguish completed checks, blocked entries, preliminary results and genuine reproduced failures. MovieWriter's fixed 30 FPS is not achieved FPS. Local loopback is not Internet/multiple-headset acceptance, and offline collision queries are not physical tracked-hand VR proof. No automatic navigation, native script equivalence or headset performance is promised.

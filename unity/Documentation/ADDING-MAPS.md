# Adding a map to ASHFALL (standard procedure)

Written in r19 (MAPKIT) from the r6 catalog import, the r12-r18 per-map fixes and the r18 MAPSITE import. A map is
not finished when it loads. Every catalog map also gets the **treatments** in section 4, and each one is detected from
the map's own geometry, so a new map needs them re-derived, not copied. Do all of section 4 and attach its evidence.

Conventions used below: `REPO` = `~/ashfall/unity`; map folder = `REPO/Assets/StreamingAssets/PlayableR23/Maps/<slug>/`;
`<id>` = `archive:<slug>`. Coordinates in map payloads are **Unity-local (Z already reflected once)**; `source-semantics.json`,
the recovery rows, the weapon-board rows and the pod-policy rows are **source-local Godot (Z reflected)**. Mixing them
up is the most common data error (see section 8). Never use `rm -r`/`rm -f` (agent rules); claim every file first.

Note: `Tools/` is listed in `.gitignore`, so the scripts under `Tools/PlayableR26/Maps/` exist in the shared checkout but are
not tracked by git unless the lead force-adds them. Keep a copy of the folder when you hand a branch over.

## 0. Checklist

| # | Step | Tool / file | Evidence to keep |
| --- | --- | --- | --- |
| 1 | Choose and download the capture | `Tools/PlayableR26/Maps/import/R26FetchCandidates.py` | HTTP receipts, GLB + `map.json` hashes |
| 2 | Convert with the R6 converter | `import/R26ArchiveCandidate.py` -> `Tools/PlayableR23/World/Catalog/prepare.py` | `provenance.json`, `spawn-proof.json` |
| 3 | Audit native chunks, spawns, kill zones; look at renders | `import/R26AuditArchive.py`, `R26ArchiveVisual.py`, `R26ReviewBatch.py` | audit JSON, overview + cutaway images |
| 4 | Thumbnail, catalog row, launch/recovery rows, license | `import/R26BuildThumbs.py`, `R26PublishMaps.py`, `R26UpdateMapsiteLicenses.py` | the rows, `SOURCE-LICENSE.txt` |
| 5 | Bind empty gameplay rows, prune enclosed spawns | `import/R26BindImportedGameplayData.py`, `R26PruneOccludedSpawns.py` | pruned list |
| 6 | **Treatments** (section 4) | `Tools/PlayableR26/Maps/r26_map_treatments.py`, `r26_map_data.py` | `audit` matrix: found vs applied per map |
| 7 | Verify | section 6 | compile PASS, `cpu_checks.sh all` PASS, rig captures |
| 8 | Report | `~/ashfall/campaign/reports/<WS>.md` | per-map treatment table |

The `import/` scripts are the r18 batch scripts, kept as templates (they read `R26SelectedSlugs.json`, a staging
folder and a `TMP` next to them; adjust paths). They no longer edit C# (r19 removed every whole-file hash pin and fixed
row count, see section 7).

## 1. Source, download, conversion, import

- Source: the public archive <https://maps.deliciouswines.org/> (Population: ONE community maps; index + per-map
  `map.json` + `map.glb`). Fetch serially with a gap, cache every response, fetch the big GLB only after the texture
  dependencies pass the texture-kit check (`R26FetchCandidates.py`). Use the **creator and slug exactly as the archive
  lists them**; some slugs carry a UUID suffix (Warehouse).
- A capture is only importable when every texture it uses has an ASHFALL replacement in the texture kit
  (`/media/al/Windows/Users/aling/Documents/ashfall/authoring/textures-v1`); original game textures are never shipped.
  Material names in the payload are the texture hashes (`p054b1b60` white, `p2a61c272` red, `p52c49b5d` dark,
  `pee49fa59` jump-pad rim, `pb8a19331` forcefield lattice, `vc` = vertex colour).
- Conversion reuses the frozen R6 converter bound to a fresh source receipt (`R26ArchiveCandidate.py`). It emits
  `map.json`, `geometry.bytes` (R23 v1 chunks: material, collision flags, visible, vertices 48 B, indices), texture
  slots, `provenance.json`, `spawn-proof.json`, `source-chunks.json`, `source-semantics.json`,
  `solid-volumes-r25.json` and `SOURCE-LICENSE.txt`. The converter removes recognizable authoring symbols **only when
  the source semantics list them** (`provenance.json` -> `sourceMarkerTrianglesRemoved`); for a fresh capture that list is
  empty, so the symbols are still in the geometry. Section 4 handles them at load time.
- Spawn proof: the converter keeps only match/warm-up sites with floor support, standing clearance and enclosure checks.
  Maps with no recorded spawns get collision-derived sites near the documented arena height (Dark Void: play floor
  Y ~ 100 m, not the roof at ~ 325 m).

### Catalog, thumbnails, notices

- `World/Resources/R24ArchiveCatalog.json`: `{id, title, relativeFolder:"Maps/<slug>", creator}` (sorted by id).
  `TeamLaunch/launch-maps.json` and `TeamLaunch/ArchiveRecovery/maps.json` get the staged `*-launch.json` / `*-recovery.json`
  rows (source hash, manifest hash, cache fingerprint, bounds, recorded/deathmatch/warm-up sites).
  `Legacy/manifest.json` lists retired captures; **remove the Legacy row when a restored map replaces it** and keep one
  active row per id.
- Thumbnail: `UI/Resources/R24MapThumbnails/<slug>.jpg` (+ `.meta`), generated from the imported mesh (`R26BuildThumbs.py`).
- Creator and license: `Maps/<slug>/SOURCE-LICENSE.txt` names the creator, the archive page and the payload URL and
  says the creator and other rights holders keep their rights (the repo MIT/CC0 does not cover the map). Menu rows show
  the creator spelled as the archive does (MENUS search matches name and creator). Add the map to
  `THIRD-PARTY-NOTICES.md` per `R26UpdateMapsiteLicenses.py`.
- `World/R23MapCatalog.cs` has an `Excluded` set (a map can be removed from the picker without deleting its payload).

## 2. What the data files hold

| File | Rows | Bound to |
| --- | --- | --- |
| `Loot/caches.json` | `sites[{sourceIndex, position}]` = weapon cache sites, Unity coords | source hash, cache fingerprint |
| `Loot/weapon-boards-r18.json` | `boards[{id, min, size, normal, hasFloorHint, floorHint}]` WEAPONS sign racks, source coords | source hash + fingerprint (checked against the launch row) |
| `JumpPads/pads.json` (+`provenance.json`) | `pads[{center, normal, radius, speed, tilted, origin}]`, Unity coords | source hash |
| `HealthStations/stations.json` | `stations[{center, normal, radius, healthPerSecond}]`, Unity coords | source hash + fingerprint |
| `TeamLaunch/ArchiveRecovery/maps.json` | `recorded`, `deathmatch`, `warmup` hint points (source coords), bounds, panels, hazards | source/manifest hash |
| `World/Resources/R26MapAuthoringMarkers.json` | `parts[{chunk, material, triangles, ranges, collisionOnly}]` | source hash; material + triangle count per chunk |
| `World/Resources/R26MapPodPolicy.json` | `verdict` OPEN/BOXED/UNSURE, `pods`, `spawns` (interior hints), reviewed defaults | source hash |
| `World/Resources/R26MapScoreboards.json` | scoreboard / billboard rectangles | source + geometry hash |
| `Styles/styles.json` | `preset`, `saturation`, `fogDensity`, `shadeStrength`, `groundTexture` | map id only |
| `Maps/<slug>/solid-volumes-r25.json` | solid-volume definitions | geometry hash |

A row whose source hash differs from the loaded map is **inert** (the runtime ignores or refuses it). When a capture is
replaced, every row for that id must be regenerated; stale rows are not an error, they silently do nothing.

## 3. Detecting features in a new map (what the author means)

The author's list: "red hexagons are the weapon spawns, as well as the signs that say weapons ... we look for jump pads,
we look for revive things." Every item below is a baked authoring assembly in the map geometry. The detectors in
`r26_map_treatments.py` are ports of the Godot recovery (`reference/tools/r14/semantic_catalog.cjs`,
`recover_caches_r17.cjs`, `archive_semantics_r8.gd`) and are **validated against the stored data of the 105 original
maps** (`r26_map_treatments.py validate`: warm-up, deathmatch, jump and health match exactly on 52 of 54 maps; Badlands and
Light vs Dark lose 1-2 jump pads; WEAPONS boards match with 0 missing on every map except Fortress). Component = triangles
connected by vertices welded at 1 mm, joined across chunks of the same material.

| Feature | Signature (Unity payload) | Gameplay |
| --- | --- | --- |
| Red hexagon | material `p2a61c272`, one component of 150-220 triangles, flat (< 0.08 m), 2.8-3.6 m across; plus question-mark glyphs (107/112 triangles 0.39 m high, or 28 triangles 0.10 m) and small parts within 2 m | weapon cache site at the ring centre |
| WEAPONS sign | backing board (12-triangle box, 0.5 m thick, wider than the letters) + 7 letter components W E A P O N S of 48/44/44/104/176/36/236 triangles in one material, same height, evenly spaced along X or Z | wall weapon rack (22 weapons) |
| Jump pad | two gold (`vc` colour 1, .776, 0) 52-triangle chevron components (15 welded vertices) 0.35-0.85 m apart, parallel, plus `pee49fa59` rim parts within 2 m; centre/normal from the thin hexagonal rim, else three supports, else the chevron plane | launch pad, 45 m/s |
| Health (revive) station | one 6-triangle, 7-vertex green face (`p6da8d940`, 1-2.7 m) + a 108/126-triangle black base within 0.35 m + two gold 12-triangle cross bars above it (one long vertical, one long horizontal) | healing pad, 10 hp/s radius 1.12 |
| Warm-up marker | material `p054b1b60`: 104-triangle annulus 1-1.5 m wide + one 6-triangle arrow 0.4-1.1 m from its centre | warm-up spawn hint |
| Deathmatch marker | material `p52c49b5d`: same annulus + arrow + a 26-triangle humanoid head 1.3-1.95 m above | arena spawn hint |
| Glass / door panels | materials whose **source** name contains `glass` (listed in `Maps/<slug>/provenance.json`/the GLB), components 0.6-8 m wide, 0.9-5 m high, <= 48 triangles; doors on `vc` | breakable windows (not applied to new maps, section 4 T14) |

Hidden-marker rule: red-hexagon material triangles that belong to a ring, a question glyph or a small part near a ring;
every warm-up/deathmatch ring, arrow and figure; all of them are authoring symbols, not scenery.

## 4. Treatments, where they live, how to apply and prove each

Run the whole set for map ids with `python3 -I Tools/PlayableR26/Maps/r26_map_treatments.py <command> --ids slug,slug`
(or `--r18` for the 21 maps of r18). Commands: `scan` (what is in the geometry), `validate` (detectors vs the original
maps), `apply` (caches, boards, stations, recovery hints, hidden markers, source-semantics sidecars), `pods`, `styles`,
`audit` (found vs applied, per map). Then run `python3 -I Tools/PlayableR26/Maps/r26_map_data.py REPO archive:<slug> ...`
for the jump-pad rows (it needs the sidecars `apply` wrote). Always run `apply` before `r26_map_data.py`, and re-run both
if the geometry changes.

| # | Treatment | Holds it | New-map detection and procedure | Evidence |
| --- | --- | --- | --- | --- |
| T1 | Red hexagon = weapon cache site | `Loot/caches.json` (`R23WorldLoot.cs`, `R23SparseLoot.cs`) | rings from section 3 become `sites`; the runtime validates floor, bounds and hazards per site and tops up sparse maps; a map with no rings keeps its audited floor-grid seeds | `audit`: found == applied; rig `weapon` item count; a rig frame of a ring site |
| T2 | WEAPONS sign = wall rack | `Loot/weapon-boards-r18.json` (`R24MountedWeaponRacks.cs`, `R24WeaponRackPlan.cs`) | `apply` writes a board per sign (min/size from the backing box, normal horizontal, away from the board toward the letters) | rig frame facing a sign shows guns mounted on it |
| T3 | Jump pads | `JumpPads/pads.json`, `R23JumpPads.cs`, `R23PadContacts.cs` | detector rows -> `r26_map_data.py`; **a pad whose contact ray is blocked by collision is dead** (feet -> centre ray, `ClearContact`) | rig: walk the bot onto the pad and read `route-position` y (it must rise); compare with an old pad as control |
| T4 | Revive / health stations | `HealthStations/stations.json`, `R23HealthStations.cs`, MP via `R26LanSession.RescueStations.cs` | detector rows (`apply`); the shared cooldown and the host-validated heal are automatic | `audit` counts; rig frame at a station |
| T5 | Hidden authoring markers | `R26MapAuthoringMarkers.json`, `R26PlayableMap.Markers.cs` (called from `R23PlayableMap.BuildChunk`) | `apply` writes `parts` with triangle `ranges`; `collisionOnly:true` parts are jump chevrons and station cross bars (visible, but no collision, as the Godot `decorative_collision_parts`) | rig A/B: old build shows the red ring, new build does not |
| T6 | Authored warm-up / deathmatch hints | `ArchiveRecovery/maps.json` `warmup`/`deathmatch` (`R24ArchiveRecoveryState.cs`) | `apply` keeps only markers with a floor 6 m below to 1 m above and 1.9 m of headroom; if none qualifies the audited synthetic sites stay | `apply` log `authored_warmup: n viable of m` |
| T7 | Pod policy: BOXED / OPEN / UNSURE | `R26MapPodPolicy.json` (`R26MapPodPolicy.cs`), `R24ArchiveRoundPolicy.cs` for the legacy forcefield lists | `pods` probes the roof over the verified match sites: >= 90 % covered = BOXED (needs interior `spawns` hints and a visual review; use `Tools/PlayableR26/Maps/boxes/`), <= 25 % = OPEN, else UNSURE (pods keep their old decision). BOXED always disables pods | row `evidence` text; overview + cutaway render |
| T8 | Forcefield lattice | material `pb8a19331` (R25 forcefield shader, automatic); legacy `R24ArchiveRoundPolicy.ForcefieldInterior` table keyed by id for 22 old maps | nothing to add for a new boxed map: its pod row and interior hints replace the table | a render of the lattice |
| T9 | Scoreboard frames and billboards | `R26MapScoreboards.json` (`R26WorldScoreboardFrames.cs`, `R24WorldScoreboards.cs`) | scan the capture with the shop frame templates (`Tools/PlayableR26/Maps/scoreboards/`, `audit/R26ImportBoards.py` + `R26ImportBoardDecisions.py`), look at both sides, approve rectangles | per-map rectangles; both-side collision test |
| T10 | Billboards and sign plates are solid | generated from T9 rows (630 panels on 79 maps in r18) and `audit/R26ArtBoards.py` for source artwork | every display rectangle gets an oriented solid box; sign/banner plates need explicit solids if the source has them in render-only batches | rig hit test from both sides (Trashed 16/16, Alfa 32/32, Doom 6/6 in r18) |
| T11 | Spawn audit | `spawn-proof.json`, recovery rows, `R26MapsSpawnShell.cs`, `audit/R26MapAudit.py` | match + warm-up sites must have support, standing clearance and no enclosing solid; remove enclosed ones from both recovery rows and cache seeds | audit JSON: every group clear |
| T12 | Loot density | `R26MapsLootDensity.cs` (automatic) | a 96 x 36 x 96 m neighbourhood with >= 24 sites keeps one site per 32 x 12 x 32 m cell | Robotics 2 worst 2 s 41.75 -> 67.92 FPS (r18); check `audit/R26CacheAudit.py` output |
| T13 | Map style (sky, light, ground) | `Styles/styles.json` | `styles` adds a row per map from `STYLE_DEFAULTS` in the tool; every catalog map must have one | `audit` column `style` |
| T14 | Windows (glass panels, doors) | recovery `panels`, `chunkPanelIds`, `World/Destructibles` | glass materials are listed above; **extracting panels needs the converter re-run with the panel pass (archive_semantics_r9)**, which the r18 adapter does not do | count glass candidates; mark not applied |
| T15 | Solid volumes (optional local exclusion hulls) | `Maps/<slug>/solid-volumes-r25.json`, `R25MapSolids.cs` | 57 original maps have one; **no generator is in the repository** (produced before the baseline), so new maps ship without it and work. If you change the geometry of a map that has one, move the file out (it is bound to the geometry hash) | file absent or hash matches `geometry.bytes` |
| T16 | Tower gate grates, Magnum rings | `map_fixtures_r18` (9 original variants), `R24ArchiveMapPolicy.MagnumOnly` | not applicable to new captures unless the signature of those variants is present | n/a |

Other automatic behaviours that depend only on `map.json` bounds: far-view culling (`R26PlayableMap.Farview.cs`),
fall failsafe 100 m below the lowest geometry, tactical map, firearm reach 3,700 m.

## 5. Code that keys on map ids

Data-driven since r19: the hash pin and fixed row counts in `R24ArchiveRecoveryCatalog.cs` (123 rows),
`R24MountedWeaponRacks.cs` (37 maps / 1,344 boards / file hash) and `R23WorldLoot.cs` (123 rows); marker hiding takes
`parts` rows. Still keyed on ids: `R24ArchiveRoundPolicy` (legacy forcefield/ground-team lists and
`ForcefieldInterior` polygons for 22 old maps; new maps use pod-policy rows), `R24ArchiveMapPolicy`/`R24PlayableMatchRules`
(Magnum), `Session.cs`, `PreparedLifeProjection.cs`, `Flight/Capsules.cs` (Paintball maps are "indoor": no capsules),
`R24WeaponRackMath` (`chill` rack side), `R23UiState` thumbnail map for three built-ins (all archive maps use
`R24MapThumbnails/<slug>`). None of them needs a row for a new map.

## 6. Verification

```bash
cd ~/ashfall/unity
python3 -I Tools/PlayableR26/Maps/r26_map_treatments.py validate             # detectors still match the 105 originals
python3 -I Tools/PlayableR26/Maps/r26_map_treatments.py audit --ids <slug>   # found vs applied, one line per map
~/ashfall/tools/compile_check.sh                                             # expect PASS, 164 assemblies, 0 failed
~/ashfall/tools/cpu_checks.sh all --output ~/ashfall/cpu-checks/<label>      # expect all four suites PASS
flock ~/ashfall/campaign/rig-build.lock ~/ashfall/tools/mp-rig/build_player.sh --out ~/ashfall/builds/r19-<ws>-<n>
~/ashfall/tools/mp-rig/mp_rig.py run --mode solo --map archive:<slug> --build <build> --minutes 1 --fps 72 --roam none \
  --out ~/ashfall/mp-rig-runs/<run> --host-args "-ashfallNetBotRoute x,y,z;x,y,z -ashfallNetBotSeated 1.06"
```

Rig notes: the route is map-local Unity coordinates (a setup teleport to the first point, then walk to the next;
the `solo-sample-0.png` frame is taken a few seconds in, so face the feature from the second point). Use
`-ashfallNetBotRouteSpeed 1` to walk slowly onto a pad. `data` files in `StreamingAssets` are read at run time
(symlinked), so only C# changes need a new build. Compare against an older build (`r18-linux-20261118-222128`, which
keeps its own data copy) for a before/after frame. Expected evidence per treatment is in the last column of section 4.

## 7. Release locks that used to break on every import (removed in r19)

`R24ArchiveRecoveryCatalog.cs` pinned the SHA-256 of `maps.json` and `Length!=123`; `R24MountedWeaponRacks.cs` pinned the SHA-256
of `weapon-boards-r18.json`, `Length!=37` and `total!=1344`; `R23WorldLoot.cs` required `maps.Length==123`. They are replaced by
non-empty checks; the per-row binding to the loaded map (source hash, manifest hash, cache fingerprint) is unchanged and
is what actually protects against a mismatched capture. If you add a new pin, add its update to the importer.

## 8. Pitfalls (each one happened)

- **Two maps glued together.** Old captures of Banana Land, G0DTHAM and Cold Hearted contained a second arena or
  intersecting rocks inside the real one. Check the overview and a cutaway render against the previous capture; the live
  capture can differ from the listing date. Keep one active row per id and move the old capture to Legacy.
- **Spawns inside geometry.** Candidate sites must be rejected when a capsule is entirely inside a mesh. The v1 loader
  doubles every triangle with reversed winding, so a ray can return a walkable normal from the **underside** of a mountain.
  Use the shell test (`R26MapsSpawnShell.cs`, `audit/R26MapAudit.py`) on stored sites; remove enclosed candidates from
  the recovery rows and the cache seeds (eight on Meliworld/Cold Hearted in r18).
- **Billboards without collision.** The display code only adds a renderer across the aperture; a player walks through
  it. Every approved rectangle must create a solid box, independent of scoreboard visibility.
- **Loot marker density.** Robotics 2 has 2,635 cache markers; 136 in one neighbourhood cost 110 ms script updates. The
  density rule is automatic, but hexagon-derived sites can cluster too: check `R26MapsLootDensity` counts after T1.
- **Decorative collision.** Jump chevrons and station cross bars in a fresh capture have collision and block the pad's
  contact ray; the original maps had it removed at import. Without `collisionOnly` parts the CPU contact-ray check blocks
  13 of the 19 usable pads on Rush, Dark Void, Cold Hearted and Fall Guys (rig: Dark Void pad 1 launched the bot from
  Y 122 to 197 m after the fix). A pad under a walkable cover plate (all 5 on Ice and Fire, 0.45-0.74 m above the rim)
  stays dead: the bot walked onto two of them and never launched, while an old-map control (Chill) launched.
- **Red rings are colliding decals** in a fresh capture and the hexagon geometry is shared across chunks, so hide by
  triangle ranges, not by chunk. The `parts` entry must match the chunk's material and triangle count.
- **Z reflection.** Payload coordinates are reflected once relative to the Godot-local data (`src_pt` in the tool does
  it). Forgetting it puts caches and warm-up hints on the mirror image of the map.
- **Legacy copies.** `r26_map_data.py` iterates `Legacy/Maps` too; a restored map's old capture would become a second pad
  row (the repo copy skips it). Search every id-keyed list for duplicates.
- **Formats.** `caches.json`, `weapon-boards-r18.json`, `launch-maps.json` and the recovery file are compact one-line JSON
  with CRLF; `pads.json` and `stations.json` are indent-2 CRLF; the pod-policy file is one row per line. Use the tool's
  writers and check `git diff --stat` is limited to your maps.
- **Hash-bound derived files.** `solid-volumes-r25.json`, scoreboards and spawn proofs bind to the geometry hash; never
  edit `geometry.bytes` by hand (r19 hides markers at load time for exactly this reason).
- **Shared checkout.** Claim files in `~/ashfall/campaign/claims.txt`, no git write commands, no `rm -r`/`rm -f`, temp
  files in `~/ashfall/campaign/tmp/<WS>/`, builds only through the rig-build flock.

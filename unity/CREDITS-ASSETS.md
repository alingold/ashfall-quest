# ASHFALL — asset credits and provenance (non-weapon assets)

Weapon model credits are in [CREDITS.md](CREDITS.md). This file covers everything replaced or
added in the November 2026 expansion. Paths are relative to the ASHFALL folder.

## Sounds

All third-party recordings are CC0 1.0 (https://creativecommons.org/publicdomain/zero/1.0/).
Courtesy credits; each was edited, trimmed, equalised and level-conformed. Per-cue sources,
licence evidence and hashes: `authoring/audio-cc0-v1/manifest.json` and `third_party/LEDGER.json`.

- **The Free Firearm Sound Library** (prepared SFX library) — Ben Jaszczak, Brian Nelson,
  Kevin Heras, Matthew Nanney; uploaded to OpenGameArt by bart.
  https://opengameart.org/content/the-free-firearm-sound-library
- **Gun reload sounds** — springyspringo. https://opengameart.org/content/gun-reload-sounds
- **Dynamite sound effect** — listener. https://opengameart.org/content/dynamite-sound-effect
- **20 Sword Sound Effects (Attacks and Clashes)** — starninjas.
  https://opengameart.org/content/20-sword-sound-effects-attacks-and-clashes
- **Muffled Distant Explosion** — nenadsimic. https://opengameart.org/content/muffled-distant-explosion
- **Fantozzi's Footsteps (Grass/Sand & Stone)** — Fantozzi; OpenGameArt upload by qubodup.
  https://opengameart.org/content/fantozzis-footsteps-grasssand-stone
- **Metal footsteps on concrete** — thimras. https://opengameart.org/content/metal-footsteps-on-concrete

Synthesized cues (27 replacement fallbacks for foley/consumables, and every sound under
`StreamingAssets/PlayableR23/Expansion/Audio/` and `Branding/startup-sting.wav`) are original
project synthesis (`authoring/audio-v1`), dedicated CC0.

Revision 3 (2026-11-10, after the first headset playtest; `authoring/audio-cc0-v1/v3`): eleven
automatic-weapon shots (AKM, CX4, MK18, MP5, MP9, P90, UMP, dual Uzis, M60, RFB, TEC9) were
rebuilt from Free Firearm Sound Library takes with their natural recorded tails; four melee
swings were shortened; the menu click and hover became dry ticks; six melee impacts were
weighted. Per-cue takes are listed in `authoring/audio-cc0-v1/v3/manifest.json`.

Revision 4 (2026-11-10; `authoring/audio-cc0-v1/v4`): five cues for the tank, helicopter and
grapple pull, layered from CC0 recordings: **Helicopter SFX** — WuxiaScrub
(https://opengameart.org/content/helicopter-sfx); a diesel idle — Mihacappy
(https://freesound.org/people/Mihacappy/sounds/819133/); and takes from the packs listed above.
Full list and evidence: `authoring/audio-cc0-v1/v4/third_party/LEDGER.json`.

The 97 recordings these replaced were extracted from another commercial game. They are no longer
in the build; copies are kept outside it in `retired/20261110-copied-audio/`.

## Map textures, ground themes and thumbnails

- All 311 distinct map textures used by the shipped community maps are original procedural
  textures (`authoring/textures-v1`, generator `src/`), dedicated to the project.
- Exception, revision 3 (2026-11-10; `authoring/textures-v1/v3`): the seven rock slots (15, 106,
  107, 108, 109, 110, 142, colour and normal) and the `pale-dune` ground theme are processed
  from **Poly Haven** photo scans, CC0 1.0 (https://polyhaven.com): `dry_river_pebbles`,
  `rock_face_04`, `rock_surface`, `cliff_side`, `rock_boulder_cracked`, `rock_face`,
  `rocky_gravel`, `damp_sand`. Evidence and hashes: `authoring/textures-v1/v3/third_party/LEDGER.json`.
- The seven detail maps in `StreamingAssets/PlayableR23/Styles/detail/` are original or derived
  from the same CC0 scans (`authoring/textures-v1/v3/detail`).
- The 11 ground themes in `StreamingAssets/PlayableR23/Styles/ground/` are original
  (`authoring/textures-v1/ground-v1`), except `pale-dune` as noted above.
- Map thumbnails are offline renders of each map with these textures (`authoring/mapviz-v1`).
- The copied originals (another game's map-editor texture library and screenshots) are kept
  outside the build in `retired/20261109-copied-map-textures/` and
  `retired/20261109-copied-map-thumbnails/`.

## Community maps

Community maps retain their creator credit and archive source URL in each map's
`SOURCE-LICENSE.txt`. This includes optional Legacy community maps and Battlefield
Paintball, the default map. The map layouts and archive-derived material are outside
the ASHFALL code license and original-asset dedication. See
[Third-party notices](THIRD-PARTY-NOTICES.md).

## Player skins

The 24 "ASHFALL Originals" skins are original procedural characters (`authoring/skins-v1`).
The shared hand/glove mesh and UV layout is Godot XR Tools "Hand_Glove_low" by DigitalN8m4r3
(Miodrag Sejic), CC0-1.0 (assets) / MIT (repository),
https://github.com/GodotVR/godot-xr-tools.
PBAN (`pban`), OBSIDIAN (`obsidian`) and WITCH DOCTOR (`claude`, Claude Opus) are
recreations of another game's characters, kept at the owner's request after the 24 originals.
Builds containing these three recreations are for private use. Archival copies of all 115
earlier recreations remain in `retired/20261110-recreated-skins/`; the other 112 stay retired.

## Identity

- Logo, wordmark, app icon, splash and hero art: original (`authoring/branding-v1`). No trademark
  search has been done.
- UI font: Open Sans SemiBold, Â© 2020 The Open Sans Project Authors, SIL Open Font License 1.1.

## Bots

Kestrel bot model: Irondust "Sci-fi Soldier", CC0 (see `StreamingAssets/PlayableR23/Bots/kestrel-PROVENANCE.txt`).

## Legacy pack

The optional Legacy pack contains material copied from another game. Its settings are off by default. A build containing it must not be distributed. Run `python maintenance/20261110-playtest/tools/legacy_pack.py --remove` before building anything that will be shared.

# ASHFALL R8 isolated source

Base: completed R7 `1d2e2d8b8acc42b30aed54128ce41f2eded367f2`. Branch: `standalone/r8-imported-map-gameplay`. Godot 4.7.2 Windows Desktop.

- SKINS is available beside LOADOUT and in the field menu. Four new Witch Doctor variants are labeled GPT-6 Astra, GPT-6.1 Sol, Gemini 4 Argon and Claude Opus. Selection persists and replicates to peers; the three existing styles remain available. Original donor files were not edited.

- Imported map spawn candidates are grounded and checked for headroom, boundaries and hazards. Zone-enabled matches retain valid arena sites; warmup uses a separate supported location. Atrocious and Paintball transitions are covered by exported runtime checks and a two-peer Atrocious match/respawn/rematch.

- Recognized red weapon hexagons/question glyphs become nearby caches of six actual random ASHFALL weapons. Classic receives distributed cache sites. Loot activates near players with bounded allocation, existing ammo/build/heal systems and authoritative ownership. Held rarity glow stays off.

- Prepared geometry adapters for all 105 imported maps remove recognized weapon symbols and collisions without decoding meshes during play. Glass and recognized thin entry panels shatter on approach or gunfire, remove collision, play an original break sound and synchronize broken state. Structural walls remain solid.

- Classic has an actual north-up tactical map and 12 newly placed functional launch pads with sound/cooldowns. Live world scoreboards show session totals near warmup and distant arena spawn areas. Original scoreboard/pad metadata was not supplied; these are ASHFALL implementations rather than exact source-object restoration.

- HUD surfaces render over nearby geometry, obstruction blackout is removed while head collision remains, and menu pointer ownership suppresses the duplicate beam. Imported map textures use mipmaps, anisotropic filtering and foliage alpha scissor. Headset comfort and tracked controller acceptance remain to be tested.

- Restored accepted R6/0992/0993 audio after finding R7/1004 changed PCM under the same filenames. Exported audio routing and decoded samples pass 604 checks. CX4 sight/rarity alignment passes 6391 checks. Weapon handling and statistics are preserved.

- Bamboo and Bananaland overlapping scenery is present in original Delicious mesh data. Independent raw-geometry views reproduced it without ASHFALL or prior maps; it remains a source issue. All 105 maps passed READY entry and weak-reference unload checks.

- Separate R8 branch, executable, userdata and unchanged R7 settings seed. Keep both adjacent map PCKs with the executable. Private imported map data remains local; creator credits retained. No VR or other running game was launched/stopped.

Source play needs the supplied private map files: pass `--local-maps-pack=<absolute path>/ASHFALL-Local-Maps.pck` after `--`; the adjacent R8 adapter pack mounts automatically. The adapters are hash-bound to `assets/data/archive_adapters_r8.json`. Rebuild prepared resources using `tests/bake_adapters_r8.gd`; completed resources and packing helper are in the isolated workspace work/r8 area. Original private archives and prepared meshes are deliberately outside source Git.

See FEEDBACK-REGISTER-R8.md and the sibling ASHFALL-R8-Evidence directory for exact scopes and remaining headset/source issues.

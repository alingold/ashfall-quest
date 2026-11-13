# ASHFALL R10 isolated source

Base: completed R9 `008b01f819dc2e9e4fbedd7c2e7466dbe6709c45`. Branch: `standalone/r10-runtime-corrections`. Godot 4.7.2 Windows Desktop, version 1.0.0.5-r10.

- Firearm rarity colors corrected to white, blue, purple, gold, red. Existing type/star selector labels are retained; held weapons have no rarity glow. Blades and grenades remain fixed form.

- Paired weapons relay the actual emitting hand to the host. Coincident/crossed hands no longer charge the wrong magazine. Accepted firearm stats, recoil, reloads, cadence and handling are unchanged.

- Launch pads enter a sustained ascending state that floor support and arms-out glide intent cannot cancel. Successful activation launches at 45 m/s; existing gravity gives about 62 m of open-air height. Walking off a ledge and ordinary flying/climbing retain their accepted behavior.

- Shared window recognition now includes custom maps using official-template glass. Recognized panes are transparent and own their pane/frame attachments and collision. Precise aperture cuts remove leftover welded frame pieces when broken. Decorative glass-named letters/posts are excluded from pane recognition.

- Doors use a new original wooden-board fracture sound; panes retain glass break audio. Every existing accepted WAV remains unchanged.

- Initial hosted match spawns prefer grounded, clear sites separated from the warmup gathering point, with a safe fallback on maps lacking sufficient space. Warmup and match keep the same single loaded map. The field menu no longer offers skin selection; active-round skin changes are rejected by authority. Main-menu selection remains available.

- Shared original procedural detail replaces confirmed low-detail gray van bodies/wheel parts and full-size plain container boxes where shapes match. Existing textured props, all source files, triangle counts and collision are preserved. This does not recover missing original UVs or complete every bespoke sign/vehicle texture.

- All 105 archive adapters rebuilt and hash-bound to this R10 executable. Separate settings/calibration profile is seeded byte-for-byte from current R9 settings; Initialize remains a manual action. Earlier source overlap/object-metadata limits remain explicitly open.

The private original map PCK and matching R10 adapter PCK must sit beside the executable. Source play: pass `--local-maps-pack=<absolute original PCK>` after `--`. Rebuild adapters with tests/bake_adapters_r10.gd (private destination last user argument), then tools/r10/index_adapters.cjs and tools/r10/pack_adapters.gd. These require the original private PCK; no donor files are modified. The adapter resources are private derived geometry kept outside Git. New prop detail is original procedural shading, not recovered donor art.

See FEEDBACK-REGISTER-R10.md, PORTING-MANIFEST-R10.json and sibling evidence. Offline simulation, synthetic controller input and loopback transport do not constitute headset or Internet acceptance. Prior feedback registers remain in this source copy.

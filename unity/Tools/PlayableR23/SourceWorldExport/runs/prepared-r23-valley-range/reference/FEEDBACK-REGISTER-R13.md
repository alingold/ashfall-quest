# R13: team-only moving ghosts and death handling

Base: completed R12, e3a9cec0625caf7509ea20bf9ad5ebcd9e4a76ee, handoff/validation/executable verified. New no-hardlinks source clone, branch standalone/r13-team-ghosts, candidate ASHFALL-R13-Ghosts and independent userdata. Previous sources/deliveries and original donor assets were not edited.

| User comment | Implemented result |
| --- | --- |
| Proper death handling and a little notice above the HUD. | One authoritative death path sets zero health/shield, clears healing and counts one death/kill. Falling also enters the death feed. Compact ghost/respawn notice remains visible above the HUD, with large legacy death text suppressed. Repeated damage cannot score another death. |
| Opponents cannot see ghosts; teammates can. | Downed and eliminated bodies render only to allies. Opponents see no body, name, bars, shadow or held gun. Ghost colliders do not stop bullets/grenades. Free-for-all treats every other player as an opponent. |
| Translucent black-and-white version of current skin. | Original current head/body/glove meshes and albedo textures retained. A per-avatar 42% alpha grayscale material replaces rendering only; original materials/overlays/shadows are restored exactly on revival/respawn. All seven styles and both model tiers checked. No character asset files changed. |
| Name and health bars remain above friendly ghosts. | Friendly tag stays visible for both downed and eliminated players, with red dead-player name, zero-pool readouts and empty bar tracks. Existing through-wall friendly tag behavior retained. |
| Move around. | Normal walking/climbing/flying remain available with world collision. Ghosts pass through player colliders. Local and authoritative weapon, pickup, building, consumable and paddle permissions remain blocked until alive. Safe arena relocation handles a ghost falling out of bounds while retaining death state, timers and counters. |
| Ghosts are visible through walls. | Ghost shader ignores geometry depth and fog; only teammates render it. Native opaque-wall control shows the alive body occluded, allied ghost visible, opposing ghost absent. |
| Preserve prior accepted systems. | Existing revive charge/reach/team validation, expiry and deathmatch respawn timing retained. Calibration/settings and manual Initialize retained. All 276 existing WAV files and 430 character/weapon model/texture files unchanged; map packs byte-identical. R12 deployment and finite dual-trigger checks pass. |

Validation uses actual Main/avatars/HUD/physics, controlled rosters, native Mobile Vulkan renders and three actual loopback peers. Synthetic movement and controlled ready-charge setup are explicitly labeled; no physical controllers, headset comfort, microphone acceptance or Internet reachability are claimed. Prior map-export/artwork gaps remain in the retained R8/R9/R10/R12 registers.

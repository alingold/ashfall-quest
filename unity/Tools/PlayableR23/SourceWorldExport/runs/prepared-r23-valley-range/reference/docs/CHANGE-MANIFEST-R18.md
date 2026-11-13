# R18 change manifest

Isolated base: completedR17 `13c19ea4ed551aef3c81f938ea1b5c6db49a1f23`; branch `quest3/r18-stations-flight` in `D:/Codex/ASHFALL-R18-Quest/source`. No original workspace, prior delivery, Skyrim or Gemini asset was edited. No VR app was launched/stopped.

- Calibration: restore the canonical origin after temporary ceiling correction, sweep upward recovery safely, and use calibrated standing clearance for validated spawns. Controller/camera tracked local poses and manual Initialize ownership are retained.
- Health octagons: instant100health+100shield with an authoritative15s shared recharge, visible cross ring/countdown, late-join/reset state and new original restore sound. Full players and ghosts do not consume a charge.15s is the user's proposed ASHFALL setting, not verified wiki parity.
- Flight/pods: spreading arms cancels pad ascent after face clearance. Pods use clearance/cruise/ground legs, retain landed body, use validated rear exits, lower their controls and add four original pod sounds.12-pod structures and existing launch directions remain.
- Loading/lifetime: show progress before work, single-worker threaded map resources on rendered clients, cancel/reap stale requests, guard deferred retired maps and release pickups. Headless Dummy rendering uses a tested staged main-thread compatibility path.
- Rendering: correct foliage alpha-to-coverage without thinning source leaves, cap normal-map contrast, retain weapon mip sharpness, and enlarge exact shared panel batches. Original source collision/triangles remain. Supply boxes now use3shared opaque surfaces rather than7/10separate body surfaces; item awards remain unchanged.
- Fixtures: complete upper central-tower grate assemblies break by contact/shots with a distinct metal sound in9verified variants. All20Afterlife lobby rings/arrows are hidden in hosted play while READY inspection is retained.
- World: four patterned translucent zone planes reach sky/pod height;267unintended Aftermath ground hazards become safe support while separate fall rules remain. Sparse maps get up to160additional validated cache sites, using the existing six-pickup authority system.
- Residual marker weld: source-proven69+35and61+27annulus fragments are recognized as their104/88converted assemblies in Alpha and Chaotic. Exact source bounds/count/fingerprint selectors remove192remaining visual/collision triangles per map, preserving floors and cache positions.
- Weapons: red-rarity real guns use original rails/hooks at1,344source-proven WEAPONS sign candidates in37maps, upright and tangent to the wall, with physics validation of every gun approach/grip and near-player activation. Native MellyPop accepts30complete racks/660guns and rejects one unsafe sign; unvisited signs are not claimed physically validated. Accepted gun/optic code, damage/recoil/reloads and existing292audio files are preserved byte-for-byte from completedR17.
- Scoreboards: original measured MellyPop faces plus Paintball's recovered frame beside Welcome. Reference-inspired navy/white/cyan UI, real FFA/team/player scores, unlimited-cap handling, stable ranks and ordinary opaque-world occlusion. The user's reference image is not shipped as game art.
- Missing art: fitted original detail for a proven blank semi cab across6sourcevariants; restrained concrete barrier detail across9variants, with exact triangle/collider/hash/UV guards. Unmatched missing textures remain open.
- Network: finite jitter-tolerant per-hand shooting budgets prevent predicted-ammo refunds while preserving accepted weapon cadence; kicked peers lose input/voice immediately and disconnect after notification, with locked-room token rejection.
- Defaults: all maps start at250%walk/300%fling, editable by user/host. All other authored rules, settings, skins, controls and saved initialization remain preserved. Version1.0.0.13-r18-quest3 / Android code19.

## Port surface

Small insertions: map fixture pass in archive_map1003; local scoreboard-row merge in archive_gameplay_r8; existing selectors call MapLoadingR18; station hook in semantics/net events; accepted player calibration/pad hooks. Dedicated R18 helpers/data/shaders/tests carry most additions. Use the isolated commit diff from the R17 base; this does not perform a Skyrim/Skyra port.

## Evidence boundary

Offline protocol, mock-XR input, native PC rendering, APK audit, USB installation and physical Quest playtesting are separate. See FEEDBACK-R18.md, R18-NETWORK-CHECKLIST.md and PERSISTENT-BACKLOG-R18.md. Remaining source overlap, unmatched art/widgets, physical Quest comfort/audio/foliage and WAN/live microphones are not claimed solved.

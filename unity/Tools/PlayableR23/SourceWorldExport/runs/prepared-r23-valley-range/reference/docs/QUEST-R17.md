# R17 Quest iteration — Alpha

Base: completed R16 d6f74d0aac75d6d3e7719b3b092fd40f8541aa72. Own D-drive source copy and branch quest3/r17-alpha; no C workspace, delivered checkpoint, Skyrim or Gemini original changes.

Changes: remove all eight campaign speech clips and their playback/text paths; retain microphone chat and all268 other WAVs (including current weapon/revive sounds). Keep initialization manual; guard unvalidated map staging from player physics, restore the calibrated origin translation at map/session relocations, retain physical crouch and runtime-owned tracking poses. Log calibration and health-pad visits for diagnosing the next physical test.

Rendering: all105 archive maps have mipmapped GPU-native compressed texture derivatives, shared by source pixels (821 bindings,519 distinct artifacts, max1024). Existing 415 compressed model/material imports and105 mesh LOD resources remain. Root viewport uses6px mesh detail; scope viewports are unchanged. Group nearby breakable panels by material and48x24x48m tiles. Keep exact source triangles, UVs, normals and collision; rebuild only a changed tile when glass/doors break/reset. No map visibility cutoff or center resolution reduction was added. Resources have per-map lifetime. Zone marking skips the replaced zero-layer originals and uses the live batch geometry.

Alpha native PC overview: R16 had3218draw calls /719546LOD triangles (832540beforeLOD). R17 final has1120draw calls /705201triangles at the same view, including additional surface repairs. This is65.2% fewer draw calls than R16. PC counters do not establish Quest frame rate or perceived headset resolution.

Gameplay/UI: walk cap500% in personal slider, save loading and host authority; defaults/other multipliers unchanged. Infinite reserve shows a dash below the real loaded count; reserve/reload mechanics unchanged. Remove sprint percentage, retain bar/icon. Alpha gets four invisible solid perimeter walls and keeps vertical fall recovery; other maps keep their own boundary/fall rules. Recover cross-chunk red cache rings in all105 sources, deduplicate validated locations and supply six existing real weapon drops. Host symbols stay hidden; READY retains inspection symbols.

Surface detail: hash-bound vehicle, tire, wood and road-junction selectors prepared for17 matching maps. Alpha adds7truck cabs,5previously blank van bodies,32tire/axle pieces,530wood pieces and7junction patches, plus162wood breakable assemblies. Original procedural detail and original collision; no claim that every exported missing-art prop in every map is repaired. All source material/model/image bytes stay unchanged. Existing archive licensing/provenance restrictions remain.

Health pads: tested the actual initialized actor standing on native pads and real ENet authority restoring HP at10/s, with no ghost revival or healing another storey. The R16 headset report is not dismissed: physical acceptance remains pending. These restore green HP; the outstanding question is whether the observed damage was HP or shield. Visit diagnostics now record both values and calibrated feet. No shield-healing behavior was invented.

## Quest validation

- Launch ASHFALL R17 Quest manually. No spoken story on startup or map entry. Initialize once in the menu, enter Alpha, return and enter again: eye height and10cm physical crouch stay consistent.

- Face outward and inward at the same Alpha lobby position; compare turning, clarity and frame stability. Look at distant roofs through AWP/SPR: no new map cutoff; check detail transitions and scoped aim.

- Set host starting health to50, zone off, start a match, and stand on a green/black pad with a yellow cross: green HP should rise toward100 at10/s. Repeat in READY after green HP damage. Blue shield is a separate pool. Report mode/map if a pad fails; the app now records contact evidence.

- Move WALK SPEED to500% in settings and host controls. Check normal saved values still load correctly; weapon handling/recoil/reload remains the accepted version.

- Inspect sprint icon/bar without a percent. Infinite reserve displays “-”; finite reserve and loaded ammunition show numbers and decrement normally.

- Run/fly into Alpha's perimeter: stop against a solid wall without horizontal teleport. Check Magnum's20m fall recovery still works.

- In hosted warmup and match, approach remaining red-hex locations: six actual grabbable weapons, authoring symbols hidden. In READY, symbols may remain for inspection. Pick up a gun and verify hand glow stays off.

- Break doors/windows from both sides; geometry and collision disappear together. Reset the round and repeat. Check zone lines do not redraw broken panes.

- Look at the repaired vans, semi cabs, wheels, wood and road junctions. Note any still-blank prop's nearby landmark for the remaining art audit.

- Return to menu and re-enter Alpha twice. Check memory/responsiveness, AWP scope, pad chains, building and separate lobby/game spawn choices.

Offline validation, installation and human headset acceptance are recorded separately. No tool starts or stops VR. The shared app package/signing identity preserves preferences; initialization remains a manual session action.

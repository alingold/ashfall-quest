# R18 feedback and acceptance register

Base: completed R17 13c19ea4ed551aef3c81f938ea1b5c6db49a1f23; own source/branch quest3/r18-stations-flight. Retain original C workspace, R17 delivery, Skyrim and Gemini originals. No VR launch/stop or change to a running game.

- [ ] R18-01 Manual vertical combat initialization persists through hosted warmup -> match -> return, pod transit/eject and map transitions. Test actual gaze flow, tracked-pose ownership, standing/crouching and collision offset recovery.
- [ ] R18-02 Spreading arms to glide cancels pad ascent after its short face-clearance interval; retain launch-normal direction and pad chains.
- [ ] R18-03 Healing octagons instantly restore both green health and blue shield to100; never revive ghosts. Shared station recharge, visible circular meter around yellow cross and seconds remaining. Research exact timer; use user-suggested15s unless verified otherwise. Authority owns cooldown, late joins and resets.
- [ ] R18-04 Tune long-distance stability, especially foliage, using mip/filter/alpha coverage and measured render cost. Preserve whole-map visibility, sniping and native center resolution.
- [ ] R18-05 Native central tower grates on cardinal sides break via shots and contact, remove complete render/collision assembly and use a distinct metal sound. Apply to matching original-map variants.
- [ ] R18-06 Remove recovered red hexagon symbols from hosted views; retain useful authoring inspection in READY.
- [ ] R18-07 Visible loading indicator before READY/host map work, with progress/state until the map is ready; no automatic initialization.
- [ ] R18-08 Improve pod launch/engine/eject/landing sounds; carry pods down to actual ground unless player ejects. Move control sign/button below front sightline.
- [ ] R18-09 Restore a translucent red patterned zone wall up to sky limit; retain ground markings/audio and efficient rendering.
- [ ] R18-10 Hide all native lobby spawn markers during hosted play, including Afterlife. Keep READY inspection.
- [ ] R18-11 Audit sparse-loot maps including Afterlife. Add useful, distributed floor/headroom-validated weapon caches using existing real item systems.
- [ ] R18-12 Weapon-board signs get one of each gun at red rarity, mounted to match wall orientation, with correct grab/ground transition. Recover recognizable boards across maps.
- [ ] R18-13 Remove the redundant EXPLORE / CHOOSE A SPAWN heading while retaining lobby/game buttons.
- [ ] R18-14 User preference supersedes earlier per-map movement defaults:250% walk and300% fling on every map, READY and host, editable by user/host. Preserve all other authored rules.
- [ ] R18-15 Audit Aftermath light-vs-dark beige winding ground paths causing respawn; fix unintended collision/hazard while preserving intentional map fall rules.

Research: no indexed wiki or official specification found for the octagon's station cooldown after targeted searches. BigBox advanced-rules documentation supports configurable health/shield actions but does not establish this pad timer.15s will be an explicit ASHFALL default, not represented as verified Population One parity. Godot official antialiasing docs recommend alpha-to-coverage for scissored foliage; Mobile has MSAA but no TAA. Sources and measured outcomes will be retained in the handoff.

## Offline status at18:50MST, November1

The checklist above remains unchecked for physical headset acceptance. Current offline evidence:

- Actual gaze initialization / READY / host / match / pod / rematch and ceiling recovery:58checks passed.
- Actual Alpha hosted doors/windows/building, pad glide cancel, current-position zone and instant station behavior:62checks passed; station sharing/late join/reset:34checks across3real peers.
- Native map lifetime:44checks across6Alpha/Afterlife/Paintball cycles;0orphan nodes and0retired pickups, menu nodes10167, static bytes settled461MB. PC measurements only.
- Native grate/station/zone/racks/Aftermath integration:28checks passed. Fifteen-second station timer is the user-proposed ASHFALL default, not verified wiki parity.
- Latest strict-engine real-peer flow207,combat164,Uzi35 checks passed. The agent's broader583-check network checkpoint also passed; physical microphones/WAN remain pending.
- Current AWP38,hand proxy179,Quest carryover78,audio/overlay506,supply rendering45 and map-default/integration254 checks passed. Current weapon/optic source and all292accepted audio files remain byte-identical to completedR17; six new original effects added.
- Source-positioned Paintball scoreboard READY/host/score/unload and FFA/multiteam presentation passed. Native rendering evidence follows the supplied scoreboard reference, preserves opaque-wall occlusion and uses the real Paintball frame.
- Render tuning and exact panel batching passed177+264checks; PC data show10.4% fewer summed panel draw calls over8Alpha headings, no broad silhouette removal. Physical Quest center-map resolution and foliage stability still require playtesting.

WEAPONS source recovery now covers1,344signs in37maps with79catalog/dedup/source-preservation checks. Native MellyPop validates30of31complete racks /660realredguns; one unsafe approach is skipped. Other maps validate floors/each gun access near activation, not blanket physical acceptance. Residual source markers outside the completed cuts remain open. The current outstanding list is [PERSISTENT-BACKLOG-R18.md](PERSISTENT-BACKLOG-R18.md).

Prior feedback and receipts remain in the inherited registers. Offline checks, native PC review, device installation and physical headset acceptance must remain distinct.

## Final build19 continuation

Version1.0.0.13-r18-quest3 / code19; runtime checkpointfcff2e47732dcb301be554ecf018ea66b91103f9. In-place Quest installation and APK/map-pack hashes verified; app left closed. Final APK gate255passed. Narrow source-bound104/88triangle weld recovery removes Alpha's remaining red hex and the matching pair in Chaotic; floor/cache preservation verified. Other Chaotic red components remain open. Native compiled exported payload world-feature28checks passed. The updated outstanding list separates implemented/offline-tested work, unsupported source families and physical acceptance.

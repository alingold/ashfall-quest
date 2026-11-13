# R14 map systems feedback

Verified base R13: 1122846d2b14d55736f4b972c95c84e56193c56a. Isolated branch standalone/r14-map-systems. Earlier feedback registers remain retained. No VR/game launch or stop; no original donor/active standalone edits.

| ID | Request | Status |
| --- | --- | --- |
| HEAL-01 | Recognize green/black octagon with cross; health-regeneration pads functional throughout all maps. | Implemented; offline validation recorded; headset acceptance pending |
| PAD-01 | Retain sustained jump-pad flight; tilted pads launch along face normal and support directional chains, including Alpha. | Implemented; offline validation recorded; headset acceptance pending |
| AUDIO-01 | Repair revive-paddle sound playback; preserve latest accepted weapon/audio assets. | Implemented; offline validation recorded; headset acceptance pending |
| ART-01 | Audit all map material bindings and native/source appearance; repair broken Alpha signs, road intersections, large trucks, remaining vans, tower fences, barrels, stop signs, lamp posts, crossing barriers, wooden fences and dumpsters. Distinguish original missing artwork from runtime faults. | Audit complete; guarded container/wood repairs included; unavailable original signs/vehicle/fence artwork remains unresolved |
| MAP-UI-01 | Remove tactical-map headers, Local Archive subtitle and bottom-right help/legend; remove duplicate health/shield panel, retain useful top-left player readout. | Implemented; offline validation recorded; headset acceptance pending |
| RARITY-01 | Tint weapon pictures, selector heading text and stars in selector/backpack using white, blue, purple, gold, red rarity order. Held weapon meshes do not glow. | Implemented; offline validation recorded; headset acceptance pending |
| RULES-01 | Apply documented source-map defaults to hosted sessions as for solo READY; host can deliberately override. | Implemented; offline validation recorded; headset acceptance pending |
| SPAWN-01 | White ring with arrow is a warmup/lobby spawn. Black ring/person marker is a deathmatch spawn. Scan all maps, recover floor/direction, and hide markers during hosted play. Team Revive retains deployment decks. | Implemented; offline validation recorded; headset acceptance pending |
| POD-01 | Add open side windows, remove pilot names/personalization, allow any unused own-team pod. Move decks to true map corners/perimeter and orient along opposite map sides. Initial travel must go forward into map; no backward launch. | Implemented; offline validation recorded; headset acceptance pending |
| POSE-01 | Fix stale authoritative/player tactical-map location causing wrong zone damage after moving/ejecting; preserve movement speed for ghosts and living players. | Implemented; offline validation recorded; headset acceptance pending |
| MOVE-01 | Add dive after approximately one second of falling when pushing forward on left joystick, increasing downward fall speed. | Implemented; offline validation recorded; headset acceptance pending |
| TRAIL-01 | Long elbow trails while flying, visible to all eligible players; respect team-only ghost visibility. | Implemented; offline validation recorded; headset acceptance pending |
| TRACER-01 | Bullet tracers visible to every player. | Implemented; offline validation recorded; headset acceptance pending |
| PING-01 | Remove arbitrary ping-distance limit while keeping finite/valid geometry positions. | Implemented; offline validation recorded; headset acceptance pending |
| PASSAGE-01 | User clarified doors/windows work in hosted warmup and solo READY, fail in actual match. Validate actual match authority/pose and passage destruction for Alpha and full library. | Implemented; offline validation recorded; headset acceptance pending |
| BUILD-01 | Building works in lobby but not actual match. Fix authority/movement and validate real match construction. | Implemented; offline validation recorded; headset acceptance pending |
| MENU-01 | Remove Release controls to resume text entirely; fix menu black backdrop and interactive laser depth so all render ahead of world geometry. | Implemented; offline validation recorded; headset acceptance pending |
| TRAIL-02 | User confirmed trails already work in lobby and match. Preserve implementation, make trails 2–3 times thicker and ensure remote visibility. | Implemented; offline validation recorded; headset acceptance pending |
| POD-02 | Improve visual fidelity against the spawn-platform reference. Exactly 12 pods per platform, futuristic framework walls and roof; preserve clear hot-drop and pod departure routes. | Implemented; native screenshots and 12-pod collision checks recorded |

## Offline results and open source gaps

The final R14 candidate has actual packaged-runtime receipts for match construction, door/window destruction, fast-fall and zone position, map defaults, UI/audio, deployment and unloading. Native Vulkan screenshots and three-peer loopback checks are separate from physical-headset acceptance.

- Health: 410/410 in-bounds authored face contacts return healing. 370 center-floor walking probes work; 40 are obstructed by original exported geometry/collision and remain listed in `semantic-summary-r14.json`. Healing through roofs/walls was not enabled to mask those source defects.
- Spawns: all 105 maps were scanned; 570 warmup and 79 deathmatch sites pass bounded floor/headroom validation. Maps without usable source sites retain floor-validated fallbacks. Authored off-arena warmup hubs do not expand match spawn bounds.
- Art: all 4,366 exported image bindings decode. Missing original UV/artwork on averaged-color signs, vehicles and other props remains unresolved. Guarded repairs cover 2,056 container ends and nine exact wood panels across 13 maps.
- Rules: 79 source exports expose settings; 26 do not. Unsupported custom source scripts are recorded and not executed. Supported values are normalized and clamped to the existing host limits; explicit host overrides win.
- Existing source composition/overlap issues recorded in earlier registers are not represented as newly fixed by this release. Original map packs and all accepted audio/model files are unchanged.
- Physical headset controls/comfort, audio listening/microphone quality and Internet reachability remain unverified. No VR runtime or another running game was started/stopped.

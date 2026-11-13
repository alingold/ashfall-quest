# Current outstanding ASHFALL issues — R19

Updated November 1, 2026,21:51MST. R19 build20 is installed with APK/PCK hashes verified and left closed; physical acceptance pending. Isolated source: D:/Codex/ASHFALL-R19-Quest/source, branch quest3/r19-support-pose, based on completed R18 documentation checkpoint a6aa9614446b548a2616284e06d25f6d302bf3d3. [All recorded feedback](FEEDBACK-MASTER.md), [this iteration](FEEDBACK-R19.md), [prior detailed backlog](PERSISTENT-BACKLOG-R18.md).

## Highest priorities still open

| Priority | Problem and evidence | Next validation or implementation |
| --- | --- | --- |
| P1 Quest aiming | Native asymmetric-eye projection and AWP's hidden extra support-grip damping are corrected. Twelve paired production-controller replays pass. General CX4/unscoped twisting was not reproduced. | Physically test right/left dominant support grip, offhand influence0, AWP/CX4, center/edge headings. Private R19 captures bounded numeric pose/settings and render diagnostics; no voice, names or positions. Inspect those before claiming the broader bug fixed. |
| P1 Quest rendering | R19 adds exact post-cut geometry LODs, retaining full near geometry, attributes and collisions. Final same-pose native Alpha lobby measurements show3.44% fewer summed visible primitives across eight views, unchanged draws. This is a modest improvement; inward-view pixelation is not solved by evidence yet. | Capture actual Quest inward/outward views, scope on/off, frame and render counters. Profile CPU versus GPU/overdraw. Keep experimental tighter bounds OFF. Preserve whole-map visibility and stable sniper silhouettes. [Research and limits](RENDERING-R19-RESEARCH.md). |
| P1 Missing art | Original landscape sign artwork and truck-deck/rail/chassis/van-wheel details cover11source-matching maps. Alpha has16repaired landscape plates. Tall portrait signs, distinct rear white flatbed/chassis and other vehicle/fence/barrel/road/stop-sign/lamp families remain. | Use exact source fragments and context in [art handoff](ART-SCORE-R19-HANDOFF.md); add original correctly proportioned art, guarded UVs and complete assemblies. Do not recolor every pink/white surface. Native proof is not physical Quest acceptance. |
| P1 Source overlaps | Raw Melis contains large co-located triangle families shared with Banana/Bamboo; independent source proof predates runtime loading. No safe authoritative layer deletion found. | Clean upstream export or reference-guided map-specific curation. [Source audit](SOURCE-OVERLAP-R18.md). Other reported maps remain individually qualified. |
| P1 Scoreboards | Three Alpha facade apertures now recovered from complete measured frame families; live host scores/cap/time, depth and unload verified. Paintball was physically accepted; MellyPop four-face display remains supported. | Other original widget/display families still need source/context recovery. Full105map exact-family scan is not proof that other maps have no scoreboards. No floating fallback. |
| P2 Fixtures | Shared105map recognition, hosted Alpha doors/windows/building, oriented pads, health stations, spawn recovery and grates retained. Bespoke scaled/fragmented families can remain unmatched. | Audit all-map coverage gaps, doors especially. Prove complete render and collision removal in exploration/warmup/match. |
| P2 Loot markers/racks | Alpha audited red markers removed; Chaotic still has11other red components/1010triangles, not all proven markers. Source-bound wall racks validate safe approaches on activation. | Recover remaining complete assemblies without deleting unrelated red shapes; inspect unsupported rack fonts/orientations and sparse maps. |
| P2 Multiplayer | Scripted real-ENet regression evidence exists; physical voice, two-headset effects/ghosts and true WAN remain separate. | [LAN/WAN checklist](R18-NETWORK-CHECKLIST.md). Use matching R19 PC/Quest content IDs; prior R18 clients are intentionally incompatible. |
| P2 Port | Checkpoints, changed-file manifest, helper boundaries and feedback registers retained. | Deliberate Skyra/Skyrim import remains separate. No Skyrim files edited. |

## Physical checks for this candidate

- [ ] App opens: automatic VCS initialization after stable tracking; no forced menu action. Standing/crouching and saved calibration survive READY, host, match, pod/eject, menu and rematch. Manual recalibration still works.
- [ ] LOADING MAP covers original Dam/menu geometry; no progress bar; transition remains cancellable.
- [ ] B/action pings appear in world and held left-trigger map. Far icons retain apparent size, target is5x larger, each owner-to-ping line follows that owner's current position; no enemy-private pings leak.
- [ ] Support grip and AWP projection remain stable in center-heavy and edge views. Scope stays active after firing while grip remains held.
- [ ] Alpha signs/trucks/vans and three source-framed scoreboards look correct on Quest. Check known remaining portrait/rear-truck gaps against the issue list.
- [ ] Repeated Alpha/Afterlife/Paintball entries do not accumulate map nodes/resources or cause escalating lag. Offline six-transition test passes; physical memory/comfort remains open.
- [ ] Other inherited controls, sounds, damage/recoil/reloads, inventory, building, breakables, ghost/team visibility and live microphone behavior remain correct.

The user physically accepted instant health+shield restoration, glide-to-cancel jump pads, and Paintball lobby/game spawns plus its scoreboard in the latest R18 playtest. Those observations are retained; they do not establish all-map acceptance. Health cooldown15s remains the user's suggested ASHFALL default, not verified Population One parity.

Actual T3 Sol tasks this iteration: XR2696134e-c633-4eef-8b48-7745e4682b09; art/score34eb3c4c-bf84-4ef9-8d2d-a8f646cfe8be; renderad1be6fe-e721-4fc1-85a4-e183dc4d3df5. Earlier Opus/Argon windows hit provider limits; no quota bypass. Historical registers are retained with their original tested scopes. Available records cover October17–November1; unavailable earlier months are not claimed reviewed.

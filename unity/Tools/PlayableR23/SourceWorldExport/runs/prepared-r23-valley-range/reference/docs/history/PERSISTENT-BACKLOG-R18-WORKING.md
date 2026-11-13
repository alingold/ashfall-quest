# Persistent ASHFALL work queue â€” November1,2026

Work deadline:20:00America/Denver (MST),2026-11-02T03:00Z. Agent interim checkpoints due19:15MST for integration. Use the existing5-hour provider windows without account switching/rate-limit workarounds. Work is complete when useful tests/evidence support it, not merely when a previous row says PASS. All substantive delegated work uses the authenticated local T3 API in separate source copies. This queue is revisited after each result and before the final handoff.

## Active assignments

| Owner | Actual model / T3 session | Scope |
| --- | --- | --- |
| Rendering | Claude Opus5.5 /c3890d11-dd0c-4709-ba74-a4c8a9201d5f | Distant foliage/edge stability, measured CPU/GPU cost, mip/alpha/LOD tuning; preserve full map and sniping. |
| Map fixtures | Gemini4ArgonHigh /367acc37-d1f1-4ed6-806b-9a424ec7e152 | Tower grates, residual red/white symbols, native weapon-board positions/normals, source-overlap provenance. |
| Network audit | Claude Opus5.5 /24c2a7d7-89b0-4a79-9935-0e23623e8d78 | Scripted host+two-client regression harness, ownership/ammo/ghost/tracer/voice state and real gaze-flow calibration review. |
| Score/art | GPT6.1Sol /a5c87914-bdd5-461b-9051-1761eb2e8d7c | Source-positioned scoreboards and positively matched missing-texture detail. |
| Primary | Current Codex chat | Integrate reviewed work; R18 healing, calibration, flight cancel, pods, loading, preference defaults, Aftermath hazards, supplemental loot, validation/build/install. |

## Current review status —18:56MST, November1

This is a working issue list, not a delivery receipt. Current R18 source is isolated and not yet installed on Quest.

- **Still open / read-only followup:** residual red markers outside the completed fixture cuts. Argon has frozen the reviewed catalog and is auditing remaining symbols/source-overlap provenance without modifying this build.
- **Reviewed agent changes now integrated:** Paintball actual Welcome-frame scoreboard and mode-aware/reference-styled MellyPop boards (READY/host/score/unload checked); Opus foliage alpha coverage and larger exact panel batches; Opus finite jitter-tolerant shooting and enforceable kick/reconnect; Argon8towergrates×9sourcevariants and all20Afterlife lobby symbols; Sol cab detail in6variants and concrete barriers in9variants. Parent render/panel/grate/hash and combined real-ENet flow/combat tests passed. The Opus provider window exhausted after the tested checkpoints; no bypass/retry is authorized before22:00.
- **Additional preservation checks:**179hand-proxy/climb/open-window checks passed;38actual mock-controller AWP checks confirm held-grip scope survives shots and closes on release, with current accepted bolt regrip;78Paintball/consumable/jump/scope/build carryover checks passed. Legacy pre-change aim/reload fixtures have documented stale expectations; weapon/optic source bytes remain identical to completedR17.
- **Implemented; offline checks passed; headset acceptance outstanding:** actual gaze initialization through READY, warmup, match, pod transit/ground landing and rematch (58checks); instant health+shield station with15s shared cooldown (also34checks across realhost/client/latejoin), launch-to-glide cancellation, match doors/windows/building and current-position zone damage (62checks).
- **Implemented; final combined/native review continuing:** visible sky-height red patterned zone wall and cross recharge ring/timer render in nativePC;source-proven WEAPONS recovery across37maps/1,344signs (Argon catalog plus primary adapter79checks). Native all31MellyPop candidates yield30complete floor/access-validated racks with660real red guns; one unsafe rack is deliberately rejected. Normal play activates up to2nearby boards per250ms, not every rack at load. Every gun approach/grip is validated before a rack is created, with floors recovered from physics instead of old hints;267Aftermath path shapes converted to safe support with267valid non-hazard probes. Lower pod controls/new original sounds, redundant heading removal and250%walk/300%fling defaults retained. PhysicalQuest acceptance remains outstanding.
- **Map lifetime:** six actual Alpha/Afterlife/Paintball entry/exit cycles and cancelled background loading passed44checks. All previous map nodes and pickups retire, orphan nodes remain0. The final native Vulkan run kept menu nodes10167, pickups0, and static memory settled461MB after all three map types. Headless baseline was approximately1.049GB; neither is Quest memory evidence. Deferred retired-map refresh guards are included.
- **Art milestone integrated:** one previously blank semi cab in six exact source variants now has fitted original detail; full ordered-triangle/collision/hash guards passed in Sol's copy and parent resource guards passed. Many other missing source art cases remain open.
- **Sparse loot milestone:** Afterlife now209validated cache sites (160supplemental), with real six-item caches activated near players; physical placement/grab audit continues.
- **Still open across the history:** known baked source overlaps; unidentified native scoreboard locations beyond the now-recovered Paintball and MellyPop boards; unmatched bespoke doors/art/spawn markers, unsafe/unrecognized weapon-board access; physical Quest comfort/foliage/audio/calibration acceptance; real WAN/cross-platform/live-voice acceptance; separate Skyrim/Skyra port.

Evidence is under the isolated R18 evidence directory. Later sections retain owners and exact next actions. Checked offline behavior must not be mistaken for physical headset validation.

## Priority queue

| Priority / ID | Problem and status | Required evidence / next action | Owner |
| --- | --- | --- | --- |
| P0 CAL | Confirmed recurring headset report: initialization not retained warmup->match despite R17. | Reproduce real gaze flow; audit origin collision drift, epochs, pod travel/eject and rematch. Test standing/crouch with runtime-owned poses unchanged. | Primary + network |
| P0 LEAVES | Confirmed R17 headset shimmer, especially leaves; center-map cost remains. | Identify actual cutout materials; measure stable alpha coverage/mip detail and native frame cost. Physical Quest acceptance stays separate. | Rendering |
| P0 HEAL | Corrected specification: instantly fill health AND shield, recharge with cross-ring timer. | Shared authority15s default (not wiki-verified); full players do not consume; ghosts cannot heal/revive; state/late join/reset tests. | Primary |
| P1 LOAD | Confirmed freeze-looking map entry; repeated map-exit lag historically. | Show indicator before work, threaded resource load, bounded progress, cancel/round races and node/resource release tests. | Primary + rendering |
| P1 MARKERS | Confirmed residual red hex and hosted Afterlife lobby icon. | Source-bound complete render/collision cuts across imported welding variants; READY inspection remains. | Fixtures |
| P1 GRATES | Confirmed native tower cardinal grates should break but do not. | Recover exact matching assemblies across original-map variants, bullet/contact/reset/network tests, metal sound. | Fixtures |
| P1 SCORES | Persistent incomplete/misplaced source scoreboards. | Detect original boards/planes across maps, render actual live score/timer in correct transforms; no arbitrary floating fallback. | Score/art |
| P1 ART | Confirmed remaining blank vehicles/signs/fences/intersections/barrels/containers/lamps. | Exact source/texture signatures, original/licensed replacements, source preservation, collision/hash checks. | Score/art |
| P1 LOOT | Confirmed maps such as Afterlife feel empty; requested red wall-mounted gun boards. | Audit all sparse maps, add floor/headroom-validated useful layers and paths; real item/authority quantities, wall normal/basis replicated. | Primary + fixtures |
| P1 POD | Confirmed pods vanish aloft and controls obstruct front. | Three-leg flight to actual support, landing/eject authority, lower controls, improved original SFX, ride ends without altering calibration. | Primary |
| P1 ZONE | Confirmed current vertical wall invisible; ground line/audio work. | Restore four translucent patterned sky-height planes, current square/phase sync, collision-independent and reasonable draw cost. | Primary |
| P1 AFTERMATH | Confirmed beige winding ground resets player. | Inspect source kill-layer geometry; remove/replace unintended walkable kill contact only on this map; preserve other-map fall rules. | Primary |
| P2 SPAWNS | Repeated historical authored lobby/match/pod mismatch; some fixed in R16/R17. | Classify native white vs black/red/blue person rings, no pod on small Paintball; repro before new changes. | Fixtures + network |
| P2 OVERLAP | Historical Bananaland/Bamboo/Beast/ChemX/Coldhearted/DarkCarnival/MelisCafe overlap reports. | Render/archive provenance: baked source vs runtime double scene. State ownership honestly; never silently delete authored architecture. | Fixtures |
| P2 HANDS | Repeated reach-through wall, failed climbing, hands stretched behind wall. | Sweep/near-side/fast-motion regression with own poses; headset reach remains manual acceptance. | Network then primary |
| P2 AIM/AMMO | Historical CX4/moving muzzle mismatch, AWP grip hold and infinite dual-Uzi fire; inherited tests pass. | Reproduce current build/ownership under simultaneous triggers and moving poses before changing accepted handling. | Network |
| P2 HUD | Historical right duplicate laser/occluded backdrop+paddles/HUD/blackout. | Verify all menu/HUD geometry uses correct overlay behavior; current new UI must retain it. | Network then primary |
| P2 NET | Solo host start, rematch, ghosts, voice mute/kick, all-player tracers/flight trails. | Two-client scripted ENet proof; WAN/NAT, live microphones and physical cross-platform remain explicitly unverified. | Network |
| P2 AUDIO | Prior repeated old SFX/paddle loop regression. | Preserve current weapon/item samples, identity/hash checks; only new pod/grate/station effects authorized here. | Primary + network |
| P2 PORT | Future Skyra/Skyrim import records. | Small insertion hooks, changed-file manifest, source checkpoint and feedback IDs; do not modify actual Skyrim. | Primary |
| P3 DEFAULTS | User final preference supersedes source movement defaults. | Every map defaults250%walk/300%fling, host/READY; remain editable; other authored settings retained. | Primary |

Previously accepted city identity/interiors/skins/menu placement and controls are preservation requirements, not excuses for speculative rewrites. The retracted star-label request and Codex transcription-indicator complaint remain out of scope. The inherited master has103historical rows; this queue distinguishes current confirmed failures from regression checks rather than claiming every old problem still exists.

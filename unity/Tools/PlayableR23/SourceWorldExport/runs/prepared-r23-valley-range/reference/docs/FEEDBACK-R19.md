# R19 feedback register — November1,2026

Base: completed R18 build19, runtimefcff2e4/documentationa6aa961. New independent source/branch: D:/Codex/ASHFALL-R19-Quest/source, quest3/r19-support-pose. R18 delivery/original assets/active C workspace/Skyrim remain untouched. No VR launch/stop. Latest instructions supersede earlier manual-only initialization.

- [x] Offline implemented; headset acceptance pending: Automatic vertical-combat initialization once on app startup after valid stable tracked height; preserve settings/calibration through all subsequent maps. Keep manual reinitialize available.
- [x] Native/offline verified; headset acceptance pending: Loading transition: simple LOADING MAP, no jerky progressbar, no original dam geometry/background showing through.
- [ ] Critical Quest support-grip bug across guns: twists away even offhand influence0. Separate production XR pose/input/frameorder from AWP compositor, reproduce heading/load dependence, no weapon damage/recoil/reload balance changes.
- [ ] AWP Quest scope bottom-right toward map center/correct toward edge, spins on support grip. Per-eye native projection and latest-view/stale30Hz orientation must be tested separately; PC mocks were insufficient.
- [x] Native/offline verified; peer/headset acceptance pending: B pings appear on both world and held offhand map, constant apparent size at any distance, colored/action icons and lines from each player's location to their own ping. Target stamp5x normal marker scale. No range cap; team privacy retained.
- [ ] Alpha blank pink signs, white semi-trucks and remaining car: identify missing complete families and add original coherent surface art, not blanket color replacement. Native before/after proof and broader matching-map profiles.
- [x] Three exact source apertures recovered; native hosted verified: Alpha scoreboard surfaces near spawn: inspect all source files and surviving native geometry automatically, broaden recovery by exact signatures; no manual per-board user inventory required.
- [x] Primary-source research complete; geometry improvement partial and Quest acceptance pending: Research verified Population One distance rendering using primary sources; distinguish actual documented facts from inference. Improve Quest geometry/draw/overdraw/LOD with full-map/silhouette/collision preservation rather than whole-image pixelation. Read current device telemetry without launching/stopping game.

Confirmed by this playtest: instant health/shield stations work, gliding cancels jump pads, Paintball scoreboard and lobby/game spawn classification work. Rendering/framerate improved but center-heavy views remain pixelated. Semi-truck repair was not accepted in-headset despite earlier native/selector checks; track that discrepancy explicitly.

Actual T3 Sol tasks: XR2696134e-c633-4eef-8b48-7745e4682b09; art/score34eb3c4c-bf84-4ef9-8d2d-a8f646cfe8be; renderingad1be6fe-e721-4fc1-85a4-e183dc4d3df5. Opus/Argon prior windows unavailable; no quota retry/account workaround. Primary owns startup/loading/ping integration and release. Offline checks, actual Quest playtesting and WAN/voice remain separate.

R19 integration result: AWP extra support-grip damping removed; per-eye scope projection corrected. Paired production controller replay passes12cases; broader Quest gun twisting is still unreproduced and requires device diagnostics. Original landscape artwork and vehicle details cover11matching maps; portrait signs/rear white truck parts remain open. Post-cut geometry LODs restore31Alpha meshes after final art merge; full near geometry/collision is preserved. Native comparisons show a modest triangle reduction, not physical Quest performance acceptance.

Additional release gate: real-ENet regression reproduced heartbeat-to-retired-peer errors. R19 now checks connected transport/channel availability before broadcast, retaining healthy-peer heartbeat/state/event/pose delivery. The shared-warmup check had an independent fixture epoch race; canonical host barrier epochs replace racy local snapshots. Final peer validation is retained separately.

T3 network follow-up: Sol thread d0eac5c9-245d-46ed-9b22-e4afb860f08e. Connected-peer guards fixed the reproduced transport error. Canonical delayed root flow207checks passes; delegated delivery/periodic-heartbeat follow-up passes218checks. These remain headless loopback results.

Final compiled exported-payload peer suite218and native world28checks pass. Signed R19/code20 installed via USB, APK/PCK hashes verified; app left closed. Full details: VALIDATION-R19.md.

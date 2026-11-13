# R14 playtest checklist

Use the sibling R14 candidate's `Launch-VR.cmd`. Initialize only when you choose to. Preserve its timestamped log if a check fails. Offline receipts do not establish headset comfort, controller behavior, audio quality or Internet reachability.

## Main menu and in-map UI

- [ ] Current full-size rotating skin stays at its world pedestal when turning/moving your head. Select a map and READY; calibration and personal settings persist.
- [ ] Open each menu near a wall: text, black backdrop and working lasers remain visible together. Closing never leaves a release-controls banner.
- [ ] Hold the left trigger: clean map appears; release: it disappears. Player marker follows ordinary movement, pod travel, eject and dive.
- [ ] Weapon icons, labels and stars match white → blue → purple → gold → red. Knives/swords/grenades keep their fixed forms; held guns do not glow.

## Alpha, hosted warmup and actual match

- [ ] Host Alpha. Source defaults populate without manual setup. Change a host value; other settings and round transitions retain that override.
- [ ] Everyone starts together at a recovered white-ring warmup location. Spawn markers/statues and weapon placeholders are hidden, with usable real loot.
- [ ] In warmup AND actual match, walk through a door/window. Entire breakable geometry and its collision disappear; wood and glass have distinct sounds.
- [ ] Build a supported wall/floor in warmup AND actual match. Other players see it, and charges are correct.
- [ ] Green/black cross pads restore health steadily to 100 while standing on them. They do not heal a different floor or revive ghosts.
- [ ] Flat pads launch high. Tilted Alpha pads launch along their face direction; successive pads work during ascent.

## Team Revive deployment

- [ ] Each platform has twelve pods, a detailed metal deck, angled framework walls and roof. Inspect near and far, including stereo edges and roof glare.
- [ ] Spawn floor, stairs/thresholds, interior headroom and canopy collision feel correct. Both pod side windows are open holes.
- [ ] Walk into any unused own-team pod, including pod twelve. No player name reserves it. Trigger/grip LAUNCH works and travel continues forward across the arena.
- [ ] Early eject and fuel expiry restore free movement without turning or recalibrating the headset. Walking/flying off the platform also works.
- [ ] Opposing platforms sit near opposite perimeter corners and travel along opposing sides. No launch goes outward/backwards.
- [ ] Deathmatch starts at black-ring arena spawn sites instead of the warmup hub or sky platform. Returning to warmup groups players again.

## Movement, combat, death and networking

- [ ] After about one second falling, push left stick forward: dive accelerates descent. Release to glide; land normally. Ghost movement speed matches living movement settings.
- [ ] During a long drop/eject, verify your map marker remains current. Zone damage follows your actual body; entering safety stops new zone damage.
- [ ] A second client sees both thicker elbow trails in glide/dive and sees bullet tracers originate at the shooter. Test distant pings beyond the previous 80/150 m range.
- [ ] Death gives a compact notice. Teammates see the translucent monochrome current skin/name/bars through walls; opponents see no ghost. Ghost can move but cannot fight/build/loot.
- [ ] Charged paddles revive the ghost once, with one discharge cue and no overlapping charge/ready sounds. Releasing/stowing/menu suspension stops charging cues.
- [ ] Verify accepted CX4/AWP alignment, scope retention, recoil, reload and dual-Uzi finite ammunition. Existing weapon/audio assets are unchanged; headset perception still needs checking.

## Library, performance and remaining source gaps

- [ ] Inspect additional maps with recognized pads/spawns, including off-arena warmup hubs. A bounded validated fallback is expected where source markers lack usable floors.
- [ ] Enter/leave Alpha and Cyber Bird repeatedly, then host/start/return. Look for retained geometry, stalls, memory growth or lost settings; save logs with map names and steps.
- [ ] Inspect repaired container ends and wooden panels at distance for shimmer. Source-only missing artwork and overlapping source maps remain listed in the material/feedback registers.
- [ ] Use packaged loopback self-tests without a headset, then test a separate real PC. Loopback success does not prove WAN reachability, voice quality or latency tolerance.

# ASHFALL R12: Team sky deployment

R12 retains the completed R11 Witch Doctor profile update and R10 gameplay/audio fixes. The standalone candidate is the sibling ASHFALL-R12 folder. Run Launch-VR.cmd for PC VR or Launch-Desktop.cmd for desktop debug. Keep its builds folder intact; ASHFALL-Local-Maps.pck and ASHFALL-Map-Adapters-R10.pck are intentionally reused byte-for-byte.

Choose a map, HOST, then enter its shared warmup. Open the left menu, HOST CONTROLS, choose Team Revive with teams enabled, and START MATCH. Teammates start on their team's elevated metal platform. Jump/glide from an open edge, or enter any unused red cabin on that deck. Aim at or touch the cyan console button and press trigger/grip to LAUNCH; release the input before pressing again to EJECT. Desktop debug uses E. Pods climb clear of the cabin row before crossing toward random validated arena sites. Travel ends with automatic release if the pilot stays aboard. Initialization remains a manual menu action.

Deathmatch uses the arena's supported spawn sites with randomized candidate traversal. It never uses the team launch decks. Ending a round returns everyone to the shared in-map warmup.

The implementation is isolated on standalone/r12-team-launch-platforms. Baseline commit 29c5f78f129be9f7317426ff79274f8bf8010c45 reproduces every verified R11 runtime hash over clean completed R10. The source object audit found no authoritative team/pod assignment in the flattened exports; the new decks are explicitly original reconstruction. See FEEDBACK-REGISTER-R12.md, REFERENCES-R12.md and the retained R8/R9/R10 registers.

Porting groups:

- launch_art_r12.gd and team_launch_r12.gd: shared meshes/materials, collision, platform layout, original audio, console controls and clocked carrier visuals.
- net_session1000.gd: authoritative launch/eject, one-use cabins, snapshot state and pose/spawn fences. net_bridge1000.gd: subsystem ownership, initial team spawns and private deathmatch spawn RNG.
- player.gd: temporary carrier state; preserve tracked origin, head height and accepted controls. archive_map1003.gd/community_map1002.gd: authorize only known sky decks and active flight paths within map bounds.
- cyber_bird.gd: records authored geometry height on the CPU while batching, excluding its finished boundary/canopy. This gives native and headless peers the same deployment altitude without changing geometry.
- assets/audio/sfx/r12: three original synthesized cues. Existing sound assets and routing are preserved. Keep the engine WAV's loop import flag; launch/eject are one-shots.

Use tools/r12/content_manifest.cjs after runtime changes and before exporting with Godot 4.7.2. Peers require a matching runtime content ID. Tests under tests/launch_*_r12.gd exercise real Main/physics and loopback transport; test fixtures are excluded from normal export. Candidate Self-Test-Deployment.cmd launches three hidden XR-off peers and writes receipts without starting VR. Self-Test-Network.ps1 -Game retains the glass/pad/skin-lock/finite-dual-weapon transport regression.

Physical headset comfort, controller reach, audio mix and real Internet reachability still need acceptance testing. Existing unrecognized bespoke map bindings, source overlaps and missing original art remain documented; this revision does not claim to repair every inherited map-export limitation. No Skyrim or original donor assets were edited.

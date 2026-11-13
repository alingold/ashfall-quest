# R12: Team Revive deployment

Base: completed R11 character delivery, verified against its handoff, executable SHA256 and all 1,273 runtime hashes. Its six runtime changes were materialized over clean R10 checkpoint 715823c2b84f5b1c50f4acf2e857e84d498e15a0 in baseline commit 29c5f78f129be9f7317426ff79274f8bf8010c45. Isolated branch standalone/r12-team-launch-platforms. R11 completed while deployment work was underway; deployment moved into a new R12 copy. Earlier delivered binaries and source assets are preserved. No VR session launched or stopped.

- Shared warmup gathering location is separate from the main menu and from match deployment. Host starts the round there; end returns everyone to warmup.
- Team Revive: one initial position per teammate on that team's own elevated metal launch platform near a map corner/perimeter. Red pods along the front. Players can walk into a pod and explicitly LAUNCH, eject early, or simply jump/glide off the open edges. Pods are optional.
- Deathmatch: use grounded arena spawn points, randomly selected with the existing safety/enemy-separation checks; never use the Team Revive launch platform.
- Apply the shared flow throughout the map library, retaining verifiable authored bindings when available. Do not pretend flattened source spawn triples contain team/platform tags.
- Preserve R10 fixes, accepted weapon/player settings, height/calibration and manual Initialize. Synchronize team assignments, pod occupancy, destination, travel and eject state through authority. Clean up on round/map/session changes.
- Deliver an isolated source checkpoint, launcher/candidate, manifest, native screenshots and offline checks separated from headset acceptance.

TEAM-01 is implemented as new shared deployment geometry throughout the map selector. R8/R9/R10 registers remain retained, including their open source-artwork and gameplay-binding gaps. The R11 character profile and glove assignment are retained unchanged.

| Comment / edge case | Result |
| --- | --- |
| Platform high in the sky; jump, fly or pod by choice. | Open 33 x 15 m climbable metal decks, 65 m above playable geometry, inward-facing red cabins. Cyberbird measures 236.325 m deck height over its 171.325 m landmark, below the finished canopy at 420 m. Direct downward collision ray reaches the arena. |
| Teams gather separately after host START. | Two to eight team decks, eight cabin lanes each (session limit eight total players). Distinct teammate spawn lanes; teammates together, opponents separated. Shared warmup remains a separate in-map gathering. Round end restores warmup. |
| Enter a red pod and launch to a random arena position. | Choose any unused own-team cabin; aim/touch its console and press trigger/grip, or E in desktop debug. Neutral input required after entering. A short outward climb clears the cabin row, followed by level travel over the arena; explicit early eject or automatic release. Destinations use floor/headroom-validated arena sites. |
| Deathmatch uses random map spawn points. | Retains existing supported-spawn, enemy-separation and warmup-avoidance checks, with private randomized candidate traversal. Deathmatch removes all deployment structures. |
| Preserve initialization and accepted gameplay. | Calibration, height, settings, inventory and health retain their existing values through deployment. No automatic Initialize. All 273 inherited WAV assets byte-identical; three new original pod cues only. Weapon handling/data unchanged. |
| Multiplayer and cleanup. | Server validates occupied own-team cabin and one-use pod, chooses destination and clock, clamps carried root poses to authorized path; preserves hand/HMD offsets. Eject uses spawn-revision fencing. Pod/engine nodes retire on expiry, round/map/session changes. |
| Source authenticity. | All 105 supplied maps inspected through retained source metadata. No authoritative pod/team bindings recovered. Four lootdropship material references are supply-ship artwork. These platforms are a reconstruction, with no claim to have restored missing original tags. |

Offline coverage and exact final artifact hashes are in VALIDATION-R12.json. Synthetic controller inputs are distinguished from physical headset acceptance; loopback transport is distinguished from Internet reachability. Native screenshots use Mobile Vulkan with XR off.

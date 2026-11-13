# Quest R16 performance and playtest fixes

R16 is an isolated continuation of completed R15 checkpoint `6b5544f7f971c9451e2df5d06fb530c961f1bad5`, branch `quest3/r16-performance`. The separate source lives under `D:\Codex\ASHFALL-R16-Quest-Fixes`. R15's source/builds and original assets are preserved. Nothing is pushed to GitHub. See [every new playtest note](FEEDBACK-QUEST-R16.md) and [the prior feedback register](FEEDBACK-MASTER.md).

## Rendering

The captured R15 Quest playtest had a sampled GPU 95th percentile of 24.676 ms, mean process callback interval 20.05 ms and a snapshot near 4 GB graphics residency with substantial swapping. These are observations from that session, not a controlled benchmark of every map.

- 415 model/material image imports now use mobile GPU compression and mipmaps. Their ETC2/EAC artifacts total 598,010,864 bytes including mipmaps, versus 3,541,630,976 bytes for base RGBA-equivalent images. This is an artifact-size comparison; not all textures are necessarily resident together. Original image pixels, GLBs and all 276 WAV files are unchanged.
- Precomputed mesh LOD indices cover all 105 archive maps. The original highest-detail vertices, triangle ordering, bounds, materials and collision remain intact. Native screen-error selection reduces distant triangles and mipmaps select distance-appropriate texture detail. No visibility distance cutoff was added.
- Android uses a four-pixel mesh LOD threshold, retains the native XR target size and enables low fixed foveation only when the runtime supports it. It does not adaptively reduce the center target resolution. PC defaults stay unchanged; the explicit QA flag exercises the Quest mesh threshold on PC.
- AWP's two identical optical views share one rendered image, avoiding a redundant map render. Each eye still uses its own XR projection. Pole crossings retain a continuous basis while preserving the firing direction; ordinary horizon, head-roll, 4x/8x magnification and SPR behavior retain their regression checks.

Native PC comparisons and screenshots are in the candidate evidence. They demonstrate working LOD selection and preserved geometry; they do not establish Quest frame rate, comfort, thermals or stereo appearance. Existing source-art/layout gaps remain separate.

## Interaction and spawns

- Grab on a catalog soda, shaker or banana now collects/equips it; trigger creates a ground pickup. This works locally and through host authority. Backpack capacity pickups retain their existing effect.
- Pad contacts use feet under the tracked HMD's X/Z, rather than only the locomotion root pivot. Server healing uses the validated head pose. Ghost exclusion and no healing through another floor remain enforced.
- Jump pads clear the face along its authored normal without carrying downward momentum or immediately bending to stick input/gravity. Swept contacts catch fast crossings between frames. The ordinary ballistic/glide controls resume after a short clearance interval.
- Native marker recovery now scans both world and roof geometry across the library. Battlefield Paintball's white lobby marker is recovered at local `(22.64996, 72.97363, 14.51456)`; its safe floor spawn is approximately `(22.64996, 73.0725, 14.51465)`.
- The previous Paintball warmup was chosen from its **24 recorded source spawn coordinates**, because the white roof-layer marker was missed. It was not recognizing a black circle. Match spawns retain those authored coordinates, around local Y 35 m, while warmup uses the white marker around Y 73 m.
- Both Paintball maps disable sky platforms/pods, including Team Revive. Server pod requests are also rejected there. Other maps retain their twelve-pod deployment platforms.
- READY exploration of an archive map now offers **LOBBY SPAWN** or **GAME SPAWN**. Hosting always uses the proper warmup location regardless of the prior exploration choice.

## Deep Space 5 building

Its authored rules grant **one starting build**; unlimited ammo is a separate setting and does not grant unlimited building. In a client-preview sweep on the map, one build permitted 410 of 576 sampled placements, while two builds permitted all 576. Some placements need both the selected panel and a lower support, and cannot be paid for with one build. These are synthetic aiming samples, not a physical-controller acceptance test.

An additional shared bug is fixed: archive roofs/stacked floors marked `construction_terrain` no longer count as buried earth. Real `terrain_surface` still blocks fully buried walls, and non-archive terrain rules are preserved. The map's authored one-build allowance is retained. To practice with more, change **HOST CONTROLS → EQUIPMENT → STARTING BUILDS** or **INFINITE BUILDS** before the match.

## Install and test

R16 updates `org.alingold.ashfallquest` using the same signing key as R15, version code 16, label **ASHFALL R16 Quest**. Its own calibration/settings survive an update; initialization stays manual. Close ASHFALL normally before running the supplied USB installer. It verifies the APK and both original map packs, and skips packs already installed with the correct hashes. Do not uninstall to update.

First test Alpha's frame clarity while moving/looking toward the center, damaged health on green pads, tilted pad chains, AWP orientation, and grabbing soda/shakers. Then test Paintball warmup versus match, no pods, both READY spawn choices, and Deep Space 5 with its one-build allowance versus a host override. Save the new session telemetry with the map/mode if anything fails.

Software checks, APK installation and human headset acceptance are reported separately. The authoring/export scripts and offline/native PC tests do not constitute a new Quest playtest. No tool launches/stops VR or replaces a running app.

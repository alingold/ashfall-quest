# CYBER BIRD standalone candidate

Run [Launch-Desktop.cmd](CYBER-BIRD-Candidate/Launch-Desktop.cmd) for desktop play,
or [Launch-VR.cmd](CYBER-BIRD-Candidate/Launch-VR.cmd) when you choose to test in VR.
Use **MAP SELECT → CYBER BIRD → READY**. The launchers preselect CYBER BIRD and keep
the existing map choices available. Height initialization is a manual Settings
action; entering the city retains current calibration and settings.

The original city is approximately 800 × 800 metres, with 80 buildings, four
districts, 537 connected storeys, 18 bridges, open windows and dispersed real loot.
Its terminal spawn has cover, two dogleg exits, a weapon, ammo/build supplies and
heals. See [the change manifest](CHANGE-MANIFEST-CYBER-BIRD.md) for scope and merge
details. This is an ASHFALL traversal/combat sandbox; campaign encounters remain
on their original map.

The isolated source is [CYBER-BIRD-Source](CYBER-BIRD-Source), branch
`maps/cyber-bird`, checkpoint `05635062068d831a6935080c402e9269c5fde083`.
It starts from the verified 9-9-2 checkpoint
`df1d0b2d4e97160c10f16b5e2b779c9208c4a4bd`. The unfinished 9-9-3 candidate was not
copied. No active standalone checkout or earlier delivered build was overwritten.

Executable: `CYBER-BIRD-Candidate/Ashfall-CyberBird.exe` (708,245,600 bytes).
SHA-256: `bf5139f8feac2cd9ac67c18ecdeedbebdd413c055e5e8393ce3752e4f960edfd`.

The source and exact executable each pass **38,976 offline checks**: 38,295 map
checks, 40 real movement/pickup/restart checks, 221 accepted weapon regressions
and 420 feedback regressions. The packaged executable also passes direct startup
with XR explicitly off. See [the validation receipt](CYBER-BIRD-Evidence/RECEIPT-CYBER-BIRD.json)
and [preservation audit](CYBER-BIRD-Evidence/PRESERVATION.json).

Native Vulkan Mobile screenshots from the exact package:
[overview](CYBER-BIRD-Evidence/01-OVERVIEW.png),
[street](CYBER-BIRD-Evidence/02-STREET-AVIARY.png),
[interior](CYBER-BIRD-Evidence/13-MARKET-INTERIOR.png),
[map selector](CYBER-BIRD-Evidence/08-MAP-SELECTOR.png),
[READY entry](CYBER-BIRD-Evidence/10-READY-ENTRY.png) and
[starter loot](CYBER-BIRD-Evidence/11-REAL-LOOT-AT-TERMINAL.png).
Additional roof and district views are in the evidence folder. These are native
renders of the delivered geometry and production lighting, with XR off.

**Headset testing has not been performed.** Stereo appearance, tracked hand
climbing through windows, flying feel, comfort and headset frame rate still need
a user playtest. The repeatable 1600 × 1000 desktop render samples freeze scene
processing; their timings are not stereo or headset performance measurements.
No VR session was launched or stopped during development or validation.

For a deliberate later merge, use [the one-commit Git bundle](CYBER-BIRD-Map.bundle)
or the patch in [CYBER-BIRD-Merge](CYBER-BIRD-Merge). The bundle requires the base
9-9-2 commit. Review the two small menu/main hooks against the completed newer
branch. Carry over the city scripts, shader and credits; review the isolated
profile name separately. The complete source copy remains independently usable.
No merge into another checkout has been performed.

# CYBER BIRD R3 — completed isolated candidate

[Launch VR](CYBER-BIRD-R3/Launch-VR.cmd) · [Launch desktop](CYBER-BIRD-R3/Launch-Desktop.cmd) · [Controls, hosting and solo test](CYBER-BIRD-R3/README.txt) · [Clean friend ZIP](CYBER-BIRD-R3-Friends.zip) · [Changes](CYBER-BIRD-R3/CHANGE-MANIFEST.txt)

This candidate includes all earlier R2 fixes **and** the queued square-zone / compact team HUD work. Argon's three authored skins are integrated; free hands retain wall contacts; floating stair treads open the under-stair route; the city has 144 buildings and 10× bird monuments. Backpack GRIP equips and TRIGGER drops; the four blade icons are upright. Weapon handling, recoil, damage, reloads, inventory rules and player height remain accepted; READY retains calibration and Initialize stays manual.

Players READY, then the host STARTs. Host controls expose zone enable, HP/s damage, m/s shrink speed and phase wait, plus deathmatch time and kill caps. Each next square is random and fully inside its predecessor. The tactical map shows red current and white next boundaries; an opaque, non-colliding one-foot white outline marks the next square in the world. Safe-square respawns check standing clearance. The shallow HUD adds total team kills, inward-growing bars, center kill cap, time and a weapon-named knockdown feed.

Source: `CYBER-BIRD-R3-Source`, branch `maps/cyber-bird-r3`, checkpoint `4b1218afa1dab662dd1e6c1ccbc438dcce80f0c7`. R2 parent `6f22e9fd6922fafe31bc6b83dac5e77400556ce4` and all prior deliveries are preserved. The completed 1.0 base is `c1b4d71b9752c5ee9d6774e194dea3498ca9d7be`. No active workspace, original Gemini asset, Skyrim or another game was changed.

Executable SHA-256: `09d9f37ec56517367516a0c77921fb50788d3b68ca761d795d757ce75d3c5788` (758993296 bytes). [Validation receipt](CYBER-BIRD-R3-Evidence/VALIDATION-R3.json).

17 clean source / exact-pack suites and 20 clean per-peer receipts are recorded. They cover 5,919 zone/authority checks, every city opening and tread, spawn and loot access, selector/READY, skins/body clearance, backpack actions, accepted weapon and calibration regressions, actual 2-game / 3-peer / 8-peer networking, dedicated late join, respawn, score/feed replication and rematch. The exact pack also passed a loopback relay with 120–200 ms round-trip delay and 1% configured packet loss. The included Windows PowerShell solo tests run independent hidden processes with XR OFF.

Native exact-pack screenshots: [overview](CYBER-BIRD-R3-Evidence/01-OVERVIEW.png), [street](CYBER-BIRD-R3-Evidence/02-STREET-AVIARY.png), [interior / stairs](CYBER-BIRD-R3-Evidence/06-STAIR-LANDING.png), [Argon skins](CYBER-BIRD-R3-Evidence/08-ARGON-SKINS.png), [compact HUD detail](CYBER-BIRD-R3-Evidence/07-HUD-DETAIL.png), [tactical zones](CYBER-BIRD-R3-Evidence/03-TACTICAL-ZONE-TEXTURE.png), [world outline](CYBER-BIRD-R3-Evidence/04-NEXT-SQUARE-WORLD.png), [host zone controls](CYBER-BIRD-R3-Evidence/08-HOST-ZONE-TEXTURE.png), [match caps](CYBER-BIRD-R3-Evidence/09-HOST-MATCH-TEXTURE.png). HUD/zone images use a controlled native match state; networking has separate actual-peer receipts.

**Offline verification only:** headset comfort, physical climbing/stair feel, stereo performance and real Internet reachability remain untested. No VR session was launched or stopped. No network/router/firewall setting was changed. Friends need the same build and a reachable host through LAN, private VPN, or forwarded UDP 24760. The clean share ZIP omits personal profiles, logs and test results; each friend initializes manually.

Source recovery / deliberate merge: [source notes](CYBER-BIRD-R3-Source/README-R3.md), [R2 + R3 upgrade bundle](CYBER-BIRD-R3-upgrade.bundle), [zone/HUD-only bundle from R2](CYBER-BIRD-R3-zone-only.bundle).


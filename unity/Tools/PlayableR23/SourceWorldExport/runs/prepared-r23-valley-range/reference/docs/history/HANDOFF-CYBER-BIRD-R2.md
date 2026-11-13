# CYBER BIRD R2 — completed isolated checkpoint

Launch [VR](CYBER-BIRD-R2/Launch-VR.cmd) or [desktop](CYBER-BIRD-R2/Launch-Desktop.cmd). Choose MAP SELECT → CYBER BIRD → READY. [Instructions and solo multiplayer test](CYBER-BIRD-R2/README.txt).

The exported candidate includes Argon's three upgraded skins, free-hand wall contact, floating stair treads and a standing under-stair route, grip-to-equip / trigger-to-drop backpack behavior, upright blade icons, 144 buildings, fuller walls, narrower roads and 10× bird monuments. Accepted handling, heights and manual calibration remain.

Source: `CYBER-BIRD-R2-Source`, branch `maps/cyber-bird-r2`, checkpoint `6f22e9fd6922fafe31bc6b83dac5e77400556ce4`. Completed base: `c1b4d71b9752c5ee9d6774e194dea3498ca9d7be`. Original delivered builds and Gemini originals are untouched.

Executable SHA-256: `e2ff76c48d1f8e2199dc07a2f58ccd6cb5be9781eecb9b5d4520f8226c5510bc` (758980336 bytes).

Offline source and exact-pack checks passed; real 2-game and 3/8-peer networking, dedicated late join, catalog equip, respawn and rematch passed. Added 120–200 ms loopback round-trip delay with 1% configured packet loss also passed. The shipped PowerShell solo test runs the exact embedded pack in independent headless processes with XR OFF. [Receipt](CYBER-BIRD-R2-Evidence/VALIDATION-R2.json).

Native Vulkan screenshots are in `CYBER-BIRD-R2-Evidence`: overview, street, interiors, floating stairs, three skins, selector/READY, backpack and north-up tactical map. Render-only desktop timings are recorded separately and do not establish headset performance.

No headset or real Internet test has been performed. Friends need the same build and a reachable host through LAN, private VPN, or UDP forwarding; see README. The later square-zone and compact match HUD request remains queued separately and is not included in this checkpoint.

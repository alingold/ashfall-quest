# Standalone ASHFALL 1004 local audit candidate

Isolated successor to preserved 1003 (`fc2a8bdcc1489f5e46f4dc0edb82a9df4a87ce03`). No Skyrim changes or public release. Imported maps, textures and derived mip cache remain local, ignored and excluded from exports; reuse rights are unknown. Host and clients need matching 1004 protocol and original map fingerprints.

## Corrections

Session/epoch/serial state fences; atomic delta-base rejection and bounded reliable resync retries; challenge-bound join welcome/denial; compatible-welcome timeout; close/reset and peer/reservation cleanup; loading-barrier departure handling and fresh new-round reconnect health.

Finite staged rounds follow dropped/transferred weapons. AWP insertion requires a fresh grip; actual magazine/box/DT11 tactical removal refunds live rounds exactly once, including zero-spare reinsertion and partial shells. Original accepted standalone mechanics remain otherwise preserved.

The optional local material adapter adds SHA-validated mipmapped texture copies only for the three measured heavy maps (Meli, Robotics2, Banana). Original geometry, collision and archive files are unchanged. Sampled full-Main GPU counter reductions: Meli14-26%, Robotics18-21%, Banana15-18%. Extra video memory about55MiB for Meli/Robotics,73MiB for Banana. These are view-specific desktop counters, not VR FPS claims.

## Completed evidence

105/105 geometry render profiles, three overview cameras each,36valid samples/view. 105/105 sampled actual-Main traversal audits: safe capsule/escape diagnostics, actual player motion, nearby wall-grab queries, outside/hazard recovery, fresh scene teardown. No sampled enclosure candidates or failed recovery. Sparse walking components are diagnostics, not exhaustive navigation proof.

Four-client 15-minute protocol endurance at earlier checkpoint641fb80:275churn/rematch cycles and68forced resync recoveries. Later finite-reload corrections have focused regressions and a short actual four-client wire follow-up. Two actual Main processes passed geometry-validated revive with a scripted charged window, ENet loss/rejoin, empty weapon/reserves restoration and four match epochs. Physical mechanics and tracked-grip regressions cover ballistic reloads, inventory, builds, climbing anchors, ray damage, rocket depletion and explosive collision; some use controlled dictionaries/permissive validators instead of full network delivery.

Original1003 preservation recheck:1452source records,105scene/manifests,1241deduplicated dependencies unchanged. Private report/CSV and muted actual-rendered client/three-map videos live in the owner folder's delivery directory and the user's Library. Full receipts, launch arguments and source hashes remain in owner evidence/FINAL-1004.json, profile-MATRIX.json and traversal-MATRIX.json. Reproduce tests through owner run1004.ps1 with the recorded *-process.json arguments; render windows use tools/gpu_window1004.py.

## Acceptance limits

No VR was launched; physical headset, multiple headsets and WAN remain unverified. Endurance uses lightweight protocol clients and belongs to its explicitly recorded earlier commit. Traversal does not exhaust interiors/climb/flight routes or recreate original map scripts. Geometry overview profiles and frozen full-Main comparisons do not establish maximum gameplay load or headset frame rate. Only original converter/game/audit code is committed locally; no push, public bundle, raw map upload or automatic Skyrim merge.

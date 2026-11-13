# Optional local community-map adapter

This source branch extends preserved standalone 1001. Original adapter, converter and regression code are separate from imported assets. No map or texture bytes from the archive belong to ASHFALL's Apache-2.0 license.

`local_community/` is ignored by Git and Godot's editor scan, and explicitly excluded from exports. It holds original downloaded data, a stripped/dequantized geometry GLB, required textures, provenance, the generated binary scene and preview. It must stay local. No redistribution permission has been established. Do not add this directory, derived geometry/materials, raw map metadata or texture archives to commits, releases, public hosts or distribution bundles. The user separately authorized a private exploration video/report.

The selected archive entry is **HILDRS ECHO**, credited by the archive to **GODS_PITDEVIL**, a community Sandbox map rather than an official Classic/Phoenix map. Attribution is the archive's statement, not an independent ownership determination. Source: https://maps.deliciouswines.org/maps/hildrs-echo-gods-pitdevil/ . The archive says maps were recorded in matches and rebuilt, and ownership remains with game owners/community builders; see https://maps.deliciouswines.org/about/ .

## Local conversion

Fetch the selected `map.glb`, `map.json`, poster and shared texture index through normal public HTTP access into `local_community/source`. Run `tools/prepare_community1002.py <absolute-source-directory>` with Python. It downloads only referenced textures, records their hashes, removes unused GLB materials/images and expands normalized signed-byte normals to standard float buffers. Positions/indices/UVs are preserved. No downloaded code runs.

Run Godot 4.7.2 with `--headless --path <candidate> --xr-mode off --script res://tools/convert_community1002.gd`. It rebuilds textured materials, merges render chunks per material and 240-metre cell, and keeps 60-metre world/roof collision tiles. Right-handed glTF Y-up coordinates already match Godot; no Babylon mirror is applied. One unit is one metre. The source viewer's Babylon mirror is not part of the source map data.

World and roofs remain solid, with back-face collision for reliable thin walls/floors. Architecture supports the existing player climbing query. Boundary geometry is invisible, solid and not climbable. Walk-through decoration retains its vertex colours and has no collision. Playfield is metadata, not a solid box. This selected map contains no kill-zone/trigger layers; the adapter does not pretend to implement arbitrary archive game rules or scripted triggers. No bot navigation/campaign conversion is supplied.

## Runtime integration

The optional `community_local` catalog slot uses the cached map's name, creator and rendered preview. It is disabled/rejected if not installed. The network fingerprint includes the actual cached scene hash, which embeds the geometry and textures. Both peers need the identical local cache. The map adds a scene offset only; recorded spawn positions are floor/headroom checked. ASHFALL weapons, five ammo families, builds and heals use existing providers and deterministic floor-validated sites. Reset and leave clean up owned local supply props, including ones already removed by a network client. Outside/fallen players recover to a validated spawn. Ordinary player death/respawn remains the host's existing multiplayer lifecycle.

Use the owner folder's `Launch-Desktop-Community.cmd` or, when Al is ready, `Launch-VR-Community.cmd`; neither was run in VR by the automated task. Launchers use the existing Godot runtime and local source/cache directly. There is deliberately no redistributable imported-map executable/bundle. The personal profile is isolated as `AshfallVR-Community1002-Local`.

`tests/community1002.gd` validates real-game physics, grip acquisition, a framed opening, spawns, loot/reset and old-map transitions. `tests/validation1001/community_peer.gd` runs a host plus two full-game loopback clients through a community match, rematch and return to range. `tests/explore_community1002.gd` records an explicitly scripted desktop camera tour and rendering counters. MovieWriter's fixed 30 FPS is not a performance benchmark or headset proof. Keep all per-run receipts and license-unknown provenance outside public distribution.

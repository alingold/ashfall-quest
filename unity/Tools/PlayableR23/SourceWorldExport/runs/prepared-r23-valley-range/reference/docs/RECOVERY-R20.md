# R20 recovery after R19 Quest crashes

User reported R19 crashes loading Alpha and a Crazy Train map, and lower perceived Cyber Bird fidelity. This supersedes R19's pending physical acceptance. R19 offline passes did not establish Quest stability.

Read-only USB evidence confirms two R19 VkThread SIGTRAP exits at22:02and22:04MST. The exact installed ARM64 library contains BRK1 after VkResult-4 at0x2279984; referenced literals identify command_queue_execute_and_present, rendering_device_driver_vulkan.cpp:3268, and Vulkan device lost/TDR. The library is stripped; nearest exported symbol names from disassembly are not function identification. This establishes the failure class, not the trigger. R18's earlier background low-memory kill is a separate event.

R19's final render snapshot stops during Alpha loading before any accepted experimental LOD is reported. A154284.9msGPU value is invalid as an ordinary frame measurement. Both Crazy Train records are outside the new art, postcut LOD and Alpha-score cohorts. Cyber Bird also uses the loader, so a shader-only always-crashes theory is unsupported. Shared loading/diagnostics combined with sustained archive GPU uploads remains a hypothesis.

## Conservative changes

- Replace the R19 clip-space loading shader with the compact physical R18 StandardMaterial panel. Retain simple LOADING MAP text, no progress bar, and desktop-only cover. The previous scene can briefly remain visible around the compact VR panel; full-view XR cover is deferred for stability. Godot's reverse-Z POSITION expression was valid; this rollback isolates an unverified XR/VRS/loading combination, not a proven malformed shader.
- Restore scripts/map_lods_r16.gd byte-for-byte from completed R18. The experimental raw-surface postcut additions remain dormant source/resources for investigation and are no longer invoked.
- Remove the private diagnostics launch flag from the normal Quest APK. Render/pose diagnostics remain opt-in; regular existing telemetry remains. Add bounded32row map-loading stage breadcrumbs with no render-server queries, per-frame writes, positions, player names or voice.
- Preserve automatic calibration, settings, pings, connected-peer transport fixes, scope/AWP changes, original sign/vehicle art and source-positioned scoreboards. Preserve engine, native resolution, MSAA, mip bias, foveation and LOD threshold from the last working R18 policy. No global resolution reduction.

Actual T3 Opus5.5 read-only review thread9599ae51-a6a3-4ccf-bab1-32c232a46da8 completed after the documented quota reset and supported selective recovery as isolation. Its raw report is retained in evidence; references to an opaque loader describe output alpha, while the original shader actually used the transparent pipeline. No reviewer source/device/GPU changes.

## Validation and remaining acceptance

Native PC repeated exploration/return: Alpha,10000Days,Showdown,CyberBird,Alpha39checks pass. Startup/pings27, saved calibration/Quest carry78, audio604, hosted Alpha authority/unload and build gate278pass. Source loader fixture initially assumed Cyber Bird exposes map_ready, corrected to the existing map protocol; that fixture failure remains in evidence. Counts overlap and are not summed.

Physical Quest recovery is pending. First cold-start and enter Alpha, return to menu, enter the tested Crazy Train map, return, enter Cyber Bird, then repeat Alpha. Verify no crash, saved initialization/controls and image quality. If a crash remains, preserve logs/map-loading-r20.json and the new system tombstone before further changes. Do not claim the root cause isolated or the device fix proven from PC passes. No VR app was launched or stopped during this work.

All previous maps, skins, weapon behavior, inventory, sound assets and license restrictions remain. This uses a fresh source copy/branch quest3/r20-render-recovery under C:/Users/aling/Documents/Codex/2026-11-01/ashfall-r20-recovery; active standalone, previous deliveries, Skyrim and original Gemini assets are untouched.

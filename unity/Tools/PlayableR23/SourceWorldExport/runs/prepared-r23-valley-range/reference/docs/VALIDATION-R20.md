# R20 build21 validation receipt

Runtime4941c350b25bdd5835355a0ffca8090681ffb519; isolated branchquest3/r20-render-recovery. APK1.0.0.15-r20-recovery/code21, SHA2565cdc5ff3ada8ecb20c529331712b34a40674a997f961360407f13e6f2ec1d694. Content ID3aaa3e580af6112c9c25b7c251b9a7f742e5d7a78c9ecb32f06a3c75ffc34376. Source copy under C:/Users/aling/Documents/Codex/2026-11-01/ashfall-r20-recovery.

Device installation: same signing certificate as R18/R19; installed APK hash, code21 and both existing PCK hashes read back and verified. App left closed at05:31:50UTC; no VR launch/stop. Existing app settings retained through in-place update. Installation is not physical crash-fix acceptance.

Passing tests retain separate scopes: native source map recovery39; compiled exported payload map recovery39; compiled startup/pings27; calibration/Quest carry78; audio604; hosted Alpha display/authority/unload; build gate278; compiled resource/content/recovery-policy check; actual compiled three-peer delayed flow218with zero script/engine errors. Counts overlap and are not summed. Tests are XRoff; native PC uses Vulkan on NVIDIA, not the Quest Adreno driver. Desktop ETC2conversion/vendor-setting warnings are retained.

APK audit: ARM64/OpenXR;240scripts;105public LOD records;519mobile texture artifacts;17inherited art resources;14retained R19resources;415ETC2variants; no removed narration. No diagnostics flag in default launch. Experimental postcut resources are dormant, not active R20 improvements. Native Godot library exactly matches R19; engine change is not part of recovery.

Exact byte comparisons confirm the R18 public map LOD helper, Quest render-policy helper and both render-policy JSON files are restored/preserved.299audio,538model,101UI and51material original files from R19 remain byte-identical, excluding import/UID sidecars. This includes current sounds and character models. No art resources or gameplay tuning were removed.

R19 evidence confirms VkResult-4/device loss, not the exact triggering delta. Fullscreen loader and default diagnostic changes are reverted as conservative isolation; the low-level LOD addition is bypassed. Compact loading text remains without a progress bar. Some original scenery can remain visible around the VR panel during loading. See RECOVERY-R20.md for hypotheses, limits and Opus review provenance.

An initial new fixture incorrectly assumed Cyber Bird has map_ready; it was corrected to the existing map protocol and rerun. The failed fixture log is retained separately. This was not a product fix or evidence of a Quest crash.

Physical acceptance still needed: cold-start Alpha, menu, the Crazy Train map that crashed, menu, Cyber Bird, menu, Alpha again; then host/match and scoped aiming. No headset stability, fidelity, microphone or WAN claim follows from these offline passes. Current status is in PERSISTENT-BACKLOG-R20.md. Prior versions and original workspaces remain immutable.

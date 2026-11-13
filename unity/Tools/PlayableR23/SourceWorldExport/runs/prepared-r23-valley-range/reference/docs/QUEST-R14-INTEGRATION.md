# Quest support merged over R14

This branch combines completed R14 checkpoint `b70460622e678b55ee104f284c80bf803c3fc90d` with Philip Bankier’s helper’s [PR #1](https://github.com/alingold/ashfall/pull/1), head `334e8f9315e43630f78f4993ad68e14117998870`. It is an isolated local integration; nothing has been pushed or merged on GitHub. R14’s previous source and delivered binaries are untouched.

The older fork APK and its October 28 headset report do **not** validate this newer source. A new R15 Quest APK has now been built with the isolated Android SDK/JDK and pinned dependencies. See [the current sideload and validation guide](QUEST-R15-SIDELOAD.md). No new headset installation or hardware validation has been performed. Initialize remains a manual action.

## Conflict resolutions

- Retained R14’s entire Windows export configuration and personal settings directory. Added the Quest Android/ARM64/OpenXR preset and the icon/mobile texture-import keys.
- Updated the Quest non-resource filters to include all current `assets/data/*.json`, model provenance and other R14 credits. Excluded raw archive/cache directories and generated Android sources, outputs and signing credentials.
- Retained all R14 gameplay, skins, weapon/audio assets, maps, twelve-pod platforms and fixes. Android-specific adaptations cover private-directory map-pack lookup and opt-in microphone permission. Windows lookup and microphone behavior are unchanged.
- Changed the APK runner’s preflight to `tools/test_quest_integration.gd`. The fork’s `tools/test_public.gd` checks an older public asset distribution and is not an accurate R14 gameplay gate. Public redistribution is a separate audit before publishing; this integration does not claim that audit has passed.

## Build a new APK

Install official Godot 4.7.2 export templates and OpenXR Vendors 5.1.0 using the pinned setup tool in [QUEST.md](QUEST.md). Install JDK 17 and the Android SDK/build tools required by that matching template, then configure their paths in an isolated Godot editor profile. Do not commit generated SDK/template/vendor files, keystores or export credentials.

```powershell
python tools/setup_quest.py --templates PATH_TO_TEMPLATES --vendor-zip PATH_TO_VERIFIED_VENDOR_ZIP
node tools/quest_content_manifest.cjs
python tools/build_quest.py --godot PATH_TO_GODOT_4_7_2 --output PATH_TO_FRESH_EXTERNAL_OUTPUT
```

The runner validates scripts, launch flags, ARM64-only libraries and the native OpenXR loader after export, and writes a receipt. A successful export does not establish Quest performance, controls, audio or cross-platform multiplayer. Use [the complete feedback checklist](FEEDBACK-MASTER.md) and R14’s `VALIDATE-R14.md` for the subsequent headset test.

The regenerated content manifest pins the Quest dependency lock and skips the generated vendor install tree, so setup order does not change gameplay identity. Regenerate it after later source/asset edits and rebuild both desktop and Quest before testing them together.

## The 105 optional archive maps

The original Quest fork contained the three built-in maps. R14 additionally uses two external packs, totaling about 3.65 GB. They are intentionally outside the APK; copying only the APK does not install the full library. Keep the existing verified R14 packs unchanged:

| File | SHA256 |
| --- | --- |
| `ASHFALL-Local-Maps.pck` | `6629ae3ee53b8ffa3a3a0d60fd0f0c6103924295c8bab1d2e1992e783a8ed2d6` |
| `ASHFALL-Map-Adapters-R10.pck` | `38afd14770fd010d39613d3ab6a74b7dcd23125c66563c697f9f1d252ecc04a9` |

On Android the loader checks `OS.get_user_data_dir()/map-packs/` before its existing desktop lookup. The new [R15 installer](../tools/Install-Quest.ps1) installs both packs into the debug app's private `files/map-packs/` directory, verifies their device hashes and removes only its own staging files. This device copy/path still requires physical headset acceptance. No broad storage permission, device deployment, application stop or VR launch was performed here.

The installed fork preview uses a different debug signing key. R15 deliberately uses the separate package `org.alingold.ashfallquest`, label `ASHFALL R15 Quest`, so it can coexist without uninstalling the fork or discarding its settings. Existing calibration does not automatically transfer between these separate Android apps.

Quest-to-PC sessions need builds made from the same integrated source/content identity and the same optional map library. The earlier R14 Windows executable has its earlier fingerprint. Rebuild the desktop target from this branch before testing cross-platform sessions.

# ASHFALL R15 Quest 3 playtest

This is a new ARM64/OpenXR APK built from the completed R14 game plus the merged Quest support. It includes the R14 fixes, four credited Witch Doctor skins, current development sounds and twelve-pod deployment platforms. The full delivery contains both unchanged archive packs: 105 archive maps plus the three built-in maps. It is a private development playtest, not a new public release or a claim that all source-map problems are fixed.

## Install on Quest 3

1. Extract the entire `ASHFALL-R15-Quest-Full.zip` to a folder on your Windows PC. Do not run the installer inside the ZIP. Keep at least **13 GB free on the headset** for installation and temporary map transfers.
2. Enable Developer Mode for your headset in the Meta Horizon phone app. Connect the Quest with a USB data cable and accept **Allow USB debugging** inside the headset. See [Meta's device setup instructions](https://developers.meta.com/vr/documentation/native/android/mobile-device-setup/) if Developer Mode or the Windows ADB driver is missing.
3. Close ASHFALL normally, then double-click **Install-Quest.cmd** in the extracted folder. Keep the headset connected until it prints **INSTALLATION_OK**. This installs the APK and copies both map packs; installing only the APK leaves out the archive library. ADB is bundled, so this PC needs no separate Android SDK, JDK, Python or SideQuest installation to sideload.
4. Unplug USB. In the headset's app library, open **Unknown Sources** and choose **ASHFALL R15 Quest**. Initialize/calibrate manually when you choose, then select a map and press READY or host its warmup.

The new app uses the separate package `org.alingold.ashfallquest`. It can coexist with Philip's earlier preview; it does not automatically import the other app's saved calibration. Updates of this R15 package retain its own settings. Microphone permission is requested only when you enable the microphone in-map. Use Touch controllers; hand tracking is disabled in this build.

If the transfer is interrupted, reconnect and rerun the same installer. Correct existing packs are skipped; incomplete packs are copied again. If more than one Android device is connected, disconnect the others or run `powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-Quest.ps1 -Serial DEVICE_ID` from the extracted folder. The installer never starts/stops the game or uninstalls another app.

To check the download without touching a headset:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-Quest.ps1 -VerifyOnly
```

## First headset checks

- [ ] App opens as stereo VR with working Touch controllers; body, left/right menu rays and both hands continue updating.
- [ ] Main menu shows one fixed pedestal and the chosen full-size skin. Verify Astra, Sol, Gemini/Argon and Claude/Opus labels and selection.
- [ ] The selector exposes 108 maps after both packs are installed. Load Cyber Bird and Alpha, return to the menu, and repeat without retained geometry, crashes or sustained memory growth.
- [ ] Hold/release the left trigger to show/hide the clean map. Confirm your marker follows pod travel, dive and ordinary movement; zone damage follows your real position.
- [ ] Host Alpha, test warmup, then START MATCH. Doors/windows break and building works in both phases. Check healing pads and tilted jump-pad directions.
- [ ] Team Revive starts on a twelve-pod sky platform. Try any unused own-team pod, launch, eject, glide and land. Deathmatch uses arena spawn points instead.
- [ ] Fire, reload and use the AWP scope and both Uzis. Listen for the current weapon sounds and one-shot paddle discharge, with no overlapping/stranded charge cues.
- [ ] Check HUD/menu backdrops and working pointers near walls; icons, stars and text use white, blue, purple, gold, red. Held weapons do not glow.
- [ ] Check frame stability in dense Cyber Bird streets, interiors and sky views. Archive CPU format checks do not establish GPU memory use or Quest performance.
- [ ] With another matching client, verify voice opt-in/mute, tracers, flight trails, building, zone, kills and teammate-only translucent ghosts through walls. Local tests do not prove Internet connectivity.

The complete prior feedback remains in [FEEDBACK-MASTER.md](FEEDBACK-MASTER.md) and [the detailed R14 checklist](../VALIDATE-R14.md). Original overlapping layouts, missing source artwork, unknown bespoke object/scoreboard bindings, unsupported source scripts and 40 obstructed healing-pad floor locations remain open or partial. Record map name, mode, reproduction steps and whether you were in READY, warmup or an actual match.

## Build and validation record

The APK was built from clean source commit `93a784ecf0aff9fc83a068689ea1037f15d0c1ba` on branch `quest3/r14-apk`. Later delivery-only documentation/installer commits do not change its runtime content. Version `1.0.0.9-r15-quest3`, version code 15, package `org.alingold.ashfallquest`.

APK SHA256: `e2becdb7a8107879414102b026d94f9401c1bd7bf36d39acf2d52c4ee5feb5b3` (686,293,759 bytes). It is a signed, debuggable development APK; keep the signing keystore private. The installer needs this debug identity to copy packs into Android's app-private `files/map-packs` directory. Do not replace it with an unrelated store/release APK.

The isolated local toolchain under `D:\Codex\ASHFALL-R15-Quest3\dependencies` contains Temurin JDK 17.0.20.1+1, Android platform 36, build tools 36.1.0, NDK 29.0.14206865, CMake 3.10.2.4988404 and platform-tools 37.0.1. Godot 4.7.2 and matching official export templates use OpenXR Vendors 5.1.0. Generated Android Gradle inputs require Gradle 8.11.1/Android Gradle Plugin 8.6.1. These exact SDK versions follow the matching template's `android/build/config.gradle`.

Tool paths are confined to the task's environment/editor profile; no global PATH or active standalone workspace was changed. Download URLs/checksums are retained in `dependencies/DOWNLOADS.json`. Setup and rebuild entry points are `tools/setup_quest.py`, `tools/quest_content_manifest.cjs` and `tools/build_quest.py`; use an isolated Godot editor profile pointing to this JDK/SDK and the matching template. Both packs remain external, unchanged and hash-verified.

Offline validation covers the clean APK export, signature, Android manifest, ARM64 native libraries/OpenXR loader, launch flags, scripts/data, all 105 archive maps' CPU image formats, installer integrity and exported desktop regression behavior. It does **not** cover headset launch, stereo rendering, controller feel, thermal/frame-time behavior, device microphone/audio or cross-platform/WAN acceptance. No APK was installed on a headset and no VR session was launched or stopped here. Nothing was pushed to GitHub.

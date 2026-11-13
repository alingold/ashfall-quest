# R12 build and offline checks

Use the official Godot 4.7.2 Windows editor and release export template. Tests never require XR startup. Keep copies of the unchanged original map and R10 adapter PCKs adjacent to the candidate executable.

From the source root, after runtime edits:

```powershell
node tools/r12/content_manifest.cjs
& $godot --headless --xr-mode off --audio-driver Dummy --path . --editor --import --quit
& $godot --headless --xr-mode off --audio-driver Dummy --path . --export-release 'Windows Desktop' $candidateExe
```

Use an isolated APPDATA/LOCALAPPDATA for every QA process. To exercise compiled runtime code, pass the exported executable as the main pack and a source test fixture by absolute path:

```powershell
& $qaEngine --main-pack $candidateExe --headless --xr-mode off --audio-driver Dummy --script $sourceTest -- $evidenceFolder --desktop --extended --playtest-catalog
```

launch_r12.gd runs six actual solo-host maps through warmup, team deployment, launch/eject/automatic release, direct hot drop, round return, deathmatch and unload. launch_library_r12.gd exercises all 105 imported maps with a controlled roster and real collision queries. launch_controls_r12.gd uses synthetic tracked inputs and an eight-team layout on the narrowest map. launch_cyber_height_r12.gd checks deterministic geometry height and a clear drop under Cyberbird's solid canopy. launch_peers_r12.gd requires three actual processes, each receiving role, receipt path, port and player count before ordinary desktop flags. Its supplied candidate Self-Test-Deployment.cmd handles that setup.

launch_capture_r12.gd captures native Mobile Vulkan views with XR off; use --capture-map=cyber_bird --capture-prefix=CYBER- for city views. The character_r11.gd fixture validates the retained completed R11 model. Physical headset behavior is a separate acceptance step.

pod_sounds.cjs reproduces the original PCM WAVs. Pass a temporary output directory to compare regeneration without changing assets. Without that argument it writes assets/audio/sfx/r12. After regenerating into the source, import again; preserve engine edit/loop_mode=2 (forward looping), with launch and exit left as one-shots. No external recordings are used.

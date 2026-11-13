# WITCH DOCTOR - ASHFALL R7 asset kit

Completed 31 October 2026. Recreated in Blender from the supplied rotating video reference.
Includes the wide felt hat, stitched plague mask, red lenses, leather capelet and coat tails,
brass fittings, potion bandolier, medical backpack, boots and matching glove textures.
This is a modelled recreation with interpreted detail; it is not a photogrammetric scan.

## Open and use

- Open source/witch_doctor_r7_posed.blend in Blender 5.2. All body textures are packed.
  Its collections contain the full-detail bake source, baked PCVR model and baked lightweight model.
  Only the PCVR collection is shown by default. Select the camera and render for a lit preview.
- For Ashfall, copy the CONTENTS of dropin/ into a source project's root, then let Godot reimport.
  Select WITCH DOCTOR (style 1). Retain the existing hand models and animation assets in the game.
  This is a source overlay, not an updated game executable. The existing game builds were not changed.
- The included loader changes one condition so the second lobby glove receives its material even
  when Godot auto-renames a sibling. The loader baseline matches the existing R6/R7/R8 source files.
  Merge that condition if applying to a newer edited loader.

## Runtime assets

PCVR: 62042 triangles. Lightweight: 22444 triangles. Each GLB contains 14 named meshes:
13 independently posed body pieces plus AvatarHead, with canonical transforms and baked materials.
Both GLBs embed 2048 x 2048 albedo, tangent normal, ORM (AO/Roughness/Metallic), and emission maps.
The separate 1024 x 1024 glove albedo/ORM/normal maps use the existing XR Tools glove UVs and rigs.
The body follows Ashfall's piece-based procedural pose system; it is not a standalone humanoid skeleton.

## Rebuild

From source/, with Blender installed:

    blender -b --factory-startup --python-exit-code 1 --python tools/build_witch_doctor.py -- rebuilt full 2048
    blender -b --factory-startup --python-exit-code 1 --python tools/bake_glove_pair.py -- xrtools086 rebuilt/textures_glove 1024

Use 'model' instead of 'full 2048' for a quick geometry/material preview.
Both LODs receive 4096-pixel source bakes, then coverage-aware downsampling and padding to 2048.
External atlas PNGs and procedural source code are included for further editing.

## Verification

See validation/: both GLBs passed embedded-texture, mesh-name, canonical-transform and triangle checks.
Godot's real kit loader assembled both quality tiers with all 13 body pieces, the head and both hands.
Both 26-bone gloves passed finite-transform and open/closed finger checks.
previews/ contains native Vulkan captures in an isolated harness using the game's real loaders
and pose code: front, side, back, head, lobby lineup, desktop gameplay pose, first-person
downward view, and open/closed gloves for both tiers.
No headset session, multiplayer skin sync session, or headset performance benchmark was performed.

## Authorship and existing rig

The character geometry, procedural body materials and new glove texture treatment were built locally.
The compatible hand mesh/rig is the game's existing Godot XR Tools asset by DigitalN8m4r3 / Miodrag Sejic,
published under CC0; its supplied notices are retained in source/xrtools086/.
No original reference-game mesh or texture was extracted. No image-generation service was used.

## R11 profile revision

The complete rendered head assembly is translated 3 cm forward. All 13 body meshes, elbow profiles, polygon counts, UVs and tracked eye/collision transforms are retained. Both quality tiers are updated. See PROFILE-UPDATE-R11.json and docs/CHARACTER-MODELING-WORKFLOW-OPUS-5.5.md.

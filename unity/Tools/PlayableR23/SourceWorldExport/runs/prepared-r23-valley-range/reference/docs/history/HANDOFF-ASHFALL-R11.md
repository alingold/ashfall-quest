# ASHFALL R11 Witch Doctor integration

[Modeling procedure](./CHARACTER-MODELING-WORKFLOW-OPUS-5.5.md) · [Desktop launcher](./ASHFALL-R11/Launch-Desktop.cmd) · [VR launcher](./ASHFALL-R11/Launch-VR.cmd) · [Integrated source](./ASHFALL-R11-Source/README-R11.md)

The Opus skin's full head assembly is 3 cm farther forward. Both accepted body meshes and elbow connections retain their geometry and triangle counts. Select **WITCH DOCTOR · CLAUDE OPUS** in the existing character menu.

The guide was reconstructed from Opus 5.5's thread, source and visual iteration. It makes elbow overlap, support/attachment review and purposeful polygon density mandatory quality gates, and distinguishes later Sol completion safeguards from Opus's original work.

The exported R11 executable passed 135 character checks and 35 existing gameplay regression checks. Both LODs were captured in four wrist/arm poses; joint centers remain inside both adjacent sleeve volumes. Profile/collar and actual selector screenshots are included. All 13 body meshes in each LOD are numerically unchanged; only head positions differ. [Validation](./ASHFALL-R11/VALIDATION-R11.json).

Runtime content ID: `da7bec30a95ba5706f2172474ef67ceb7c0f07c34529baedd6d7d30f41babca7`  
Executable SHA256: `963715800272d273b517d87d0b1727851535ddc343525f94a20d3d69202532a2`

R10 and its map packs are preserved. R11 uses separate settings seeded from R10. Headset testing remains. Editable Blender source: [witch_doctor.blend](./ASHFALL-R11/character-authoring/build/witch_doctor.blend).

# Original R23 pickup aim hints

Ready for root integration; no Root/Hands/Consumables/UI/Gallery files were edited. New component: `Ashfall.Integration.Playable.PickupHints.R23PickupHints`.

```csharp
Hints = gameObject.AddComponent<R23PickupHints>();
Hints.Initialize(Rig.Head.transform);

// Once per host frame, after real providers choose the eligible physical-hand targets:
Hints.Present(gameplayEnabled,
    haveLeft ? new R23PickupHintTarget(leftId,leftLabel,leftCenter) : default(R23PickupHintTarget),
    haveRight ? new R23PickupHintTarget(rightId,rightLabel,rightCenter) : default(R23PickupHintTarget));
// State branches that stop normal targeting updates:
Hints.Suspend(); // or ClearAll(), Clear(physicalHand0left1right)
```

Caller retains actual target selection, display names, aim/range/occlusion eligibility, hand availability and lobby/pause/focus policy. This component only displays supplied id/label/world center. It never calls pickup/equip, invents labels, polls hardware, moves tracked poses or creates a collider. Default/invalid target clears that hand; false gameplayEnabled clears both. Labels are kept as supplied (bounded to256 characters); target ids are diagnostic only.

Source is original `weapon_inventory.gd::_create_aim_ring/update_aim_rings` and `upright_ui.gd::view_basis`:24 closed .041×.007×.007m boxes, centers at .17m radius, tangent rotations angle+π/2, left RGB(.25,.85,1), right RGB(1,.8,.2), unshaded with real depth. One shared576-vertex/288-triangle mesh replaces24 separate draw calls per hand. Each hand has one ring draw and one name draw. Label node is at localY+.24, source font30px at .0015m per glyph pixel. It reuses the already installed original `R23GodotUI` font through its own atlas instance, with custom pixel-metric glyph quads and white text; no menu font/overlay material is reused or modified.

Orientation copies current head view direction while removing roll, not a target-to-eye look-at. The near-vertical fallback is deterministic world-right; source x-first orthonormalization is retained. Only owned ring transforms change. Rings use opaque depth writes; labels alpha-blend with depth testing and no depth writes. The dedicated Resources shader supports stereo instancing. There is no always-visible-through-wall overlay. Disable/application pause/destruction clear cues and dispose owned visuals, meshes, materials and font.

Read-only inspection: `Ready`, `VisibleCount`, `VisualRoot`, and `Read(hand)` returning Visible, TargetId, Label, Center, Rotation, Color, RingVertices, RingTriangles, LabelGlyphs. Root can validate that actual provider ids match displayed ids and inspect VisualRoot for zero colliders/four total renderers.

Checks pass:380 source geometry/upright/label-scale cases; original source constants/hash checks; explicit depth/stereo shader state and passive/no-collider ownership checks; installed Unity runtime/editor-runtime/editor build-smoke API compilation. Commands: `python unity/Tools/PlayableR23/PickupHints/check.py`, `python unity/Tools/PlayableR23/check_integration.py`, then `python unity/Tools/PlayableR23/PickupHints/check_fixture.py`.

Future native fixture: staged `R23PickupHintsSmoke.Run(Camera acceptedHead, Action<R23PickupHints> inspectOrCapture = null)`. It checks caller identities, exact centers/palette, combined geometry,30px glyph output, zero colliders, bounded renderers and invalid/lobby cleanup. Host may capture the held cue through the callback. Fixture API-compiles but is UNRUN. No engine/GPU/render/build/device/Blender job was run. Actual font rasterization/kerning/default Label3D outline parity, shader compilation and stereo wall occlusion remain unverified; no visual acceptance is claimed. Installed/historical APKs are untouched.

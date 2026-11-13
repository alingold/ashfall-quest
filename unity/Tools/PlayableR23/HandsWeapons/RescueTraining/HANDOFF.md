# Actual R6 solo rescue training dummy

Published only new `HandsWeapons/R24RescueTrainingDummy.cs`, `R24RescueTrainingDummyState.cs`, `R24RescueTrainingDummyGeometry.cs` and their new metas. No Main/paddle/Health/map edits. New tools stay here.

## Root composition

```csharp
var dummy = gameObject.AddComponent<R24RescueTrainingDummy>();
dummy.Initialize(actualHead);
// Actual source _begin_chapter/reset only, never ordinary menu resume:
dummy.SetCheckpoint(actualWorldCheckpoint, actualSandbox);
dummy.SetWorldVisible(actualExerciseWorldVisible);
// Each source process interval, also while ordinary main.paused is true:
dummy.Tick(dt, gameplayPaused);
// Append only when true to the EXISTING paddle callback target array:
R24ReviveTarget target;
if(dummy.TryReadTarget(out target)) targets.Add(target);
// In completed callback, route exact ID to this actual solo owner:
if(id==R24RescueTrainingDummyState.TargetId) return dummy.TryRevive(id);
```

Keep existing genuine network target callback separate. This dummy is explicitly a solo exercise, not a fabricated network teammate. Caller still supplies existing actual paddle charge, physical contacts, LOS, and allowed-tool policy. Completion emits `Revived` once; it owns no paddle charge/sound/counter or network Health state.

`SetCheckpoint` applies Unity offset(-4,+.45,+3) for an actual active sandbox, otherwise(-4,-1,+3), one reflection of source Z?3, then source reset(downed/HP0/timer0). Lifetime rescue_count is deliberately retained. Owned content child only; never moves the host/player. `TryReadTarget` supplies existing `R24ReviveTarget` with the existing paddle contract's Unity right+X/up+Y/forward+Z basis. Contacts separately reflect source(-/+ .13,.61,+.13) into Unity local(-/+ .13,.61,?.13). Existing paddle Contact already reflects the source asymmetric Z band[-.15,+.65] into Unity[-.65,+.15]; do not reflect its basis a second time. Real paddle unit tests prove source front.4 accepts and back.2 rejects for both contact orders.

Successful `TryRevive` accepts only the exact dummy ID, actual visible-in-hierarchy content, and downed state. Sets HP50, count++, reset12s. Hidden/completed/refused targets do nothing. Timer runs while hidden and through **ordinary gameplay menu pause**, exactly as source child `_process`: main.gd only uses its own `paused` flag for menus; its sole SceneTree.pause=true is quitting. Explicit application/tree suspension may skip Tick; the optional `paused` argument documents ordinary menu context and intentionally does not freeze this source process. A regular UI resume must not call SetCheckpoint.

## Presentation and read-only seams

Actual source .52?.7?.22 box centeredY.51, two radius.075/thickness.012 cylinders centered?.13,.61,?.13.64 radial segments/4 edge rings use [Godot CylinderMesh defaults](https://docs.godotengine.org/en/stable/classes/class_cylindermesh.html). Pure source-dimension geometry uses outward unit normals/positive triangles. No primitive/collider is ever created. Mesh UVs absent because no texture is bound.

Body source authored Color(.12,.24,.28), nonmetal/rough1; unshaded pads downed(1,.38,.15), complete(.25,1,.45). Source authored display colors decode for a Unity Linear project, remain encoded in Gamma. No glTF/material payload changes. Source caption exact, Label font30/pixelSize.002 atY1.15, yaw-only billboard toward acceptedHead. Existing R23GodotUI font + R23GunFeedback font shader reused; body uses URP/Lit, pads URP/Unlit. **Root build should retain URP/Unlit** if not already retained. Rendering/lighting/label acceptance remains native-pending, not claimed by API compilation.

Readonly runtime seams: Downed,Health,ReviveCount,ResetRemaining,Readout,WorldPosition,WorldVisible,ColliderCount and WorldRoot. Immutable actual target exposes contacts/basis. Pure geometry factory exposes actual vertices/normals/indices for focused checks. State lives only here, not in a second registry or shadow player health owner.

## Checks and provenance

`python unity/Tools/PlayableR23/HandsWeapons/RescueTraining/check.py`: **1348 PASS**, exact lifecycle/12s/ordinary pause/hidden/invalid callback/reset-count, actual shared paddle state charging and completed callback, reflected contacts/checkpoint offsets, and all body/pad triangle winding/unit normals. Query delegate in this pure unit test is explicit always-clear; no scene/LOS/native acceptance claim.

All scripts consumed are validated against immutable R6 SOURCE-MANIFEST SHA b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111; hashes in source-pins.json. Primary behavior `rescue_training_dummy.gd:1?37`, placement/reset `main.gd:578?580`, source paddle target-local contact + completed revive `revive_paddles.gd:139?160`. Source remains readonly.

Installed Unity6000.6.8f1 Runtime API compile PASS with all current source (isolated output under Tools/Consumables/WorldPickupLayout, existing owner checker). No engine/GPU/Blender/device/build/native operation executed. Root owns final3-target check/composition/native.

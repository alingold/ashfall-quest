# Source construction damage, next candidate

Root APIs now implemented in `R23VRActivities`:

```csharp
activities.TickStructure(seconds, pausedOrGameplayBlocked); // before gameplay hit dispatch
bool damaged = activities.TryDamageCollider(actualNearestHitCollider, sourceDamage);
PlacedPanel[] actualState = activities.PanelSnapshot;
```

`TryReadPanel(Collider,out PlacedPanel)` resolves an active owned native collider to the actual service snapshot. Foreign/map/preview/disabled/withdrawn colliders cannot authorize damage. The root connects only its committed carried-weapon `WorldHit` event. This worker did not modify or native-test that firearm event path.

`R23ConstructionSession.Damage` uses `CandidateService.PrepareDamage` with its actual revision; `Advance` uses `PrepareAdvance`. The existing owner supplies wall180/floor140 health and .30sec per severed-support depth. No new health or collapse clock exists. Native GameObjects reconcile after every committed placement/damage/advance; removed colliders disable and objects hide immediately before deferred Unity destruction. Empty destroyed slots rebuild through normal source placement/debit with fresh IDs. Damage, collapse and rescue never grant/refund charges. Pause blocks runtime damage, stops source advance and freezes the cosmetic .12sec hit flash.

The genuine original `construction_piece.gd` selects `assets/materials/build_panel_user.png`; copied494944bytes unchanged, SHA256 `30aa22e75d16ec540d9234a05e7d228e3aa720f67ab6414cd719a60e3ffdc9a3`. Load is frame-yielding StreamingAssets/Android-compatible. Source material: white albedo, metallic.6, roughness.78, double-sided, no permanent emission. Unity cube faces already coverUV0..1, so each shows the complete source image at tiling1. Source Godot's3x2 atlas compensation is not copied. Health darkening and transient orange hit emission use owner snapshot health and cosmetic timers only. `SourceFinishLoaded/SourceFinishFailure` report the real image load result; a missing load does not pretend the crop appeared.

Focused check: `python unity/Tools/PlayableR23/Activities/check.py` passes49 real managed mechanics assertions (including the existing movement/pad cases), source PNG identities and installedUnity6000.6.8f1 warnings-as-errors compilation of runtime plus the staged Editor fixture. New cases exercise real CandidateService damage/invalid damage,180/140 health,300/600ms cascade,100 paused seconds, support rescue/lost contact, paid same-slot rebuilding and no refunds.

Staged native entry: `Ashfall.Editor.Playable.R23ConstructionDamageSmoke.Run()` during PlayMode. It creates isolated native floor/CharacterController/Activities fixtures, uses real trigger/release placement, nearest `Physics.Raycast`, direct `TryDamageCollider`, immediate no-longer-ray-hittable collider withdrawal, pause, two support-depth cascades, rebuild/old-collider rejection and actual native floor-support removal. It was API-compiled, **not engine-run**. It does **not** prove carried-gun -> `WorldHit` -> damage; root still needs that integrated native proof.

Limits retained: current CandidateService slice builds axis-aligned walls/floors; source ramp160 is recorded in art provenance without claiming ramp placement. Existing world contacts are rechecked by the candidate owner during active advance; no separate .25sec source polling scheduler was added. No debris/audio/network/recovery expansion, engine/GPU/device job, root/Hands/shared-module edit, or source exporter execution occurred.

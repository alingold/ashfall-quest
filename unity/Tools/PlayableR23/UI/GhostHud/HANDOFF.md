# Compact local ghost HUD ? released

UI Assets are coherent and HELD; no pending writer. Root/Flight own actual motion/item gating. No engine/native/device run here.

Published UI changes:

- `R24MatchGhostHud.cs` + meta: real camera-local panel and live authoritative notice under the existing HUD. `IsLocalMatchGhost`, `GhostHudVisible`, `GhostHudRoot` are public read-only seams.
- `R24MatchRulesUi.cs`: downed/eliminated/respawning keep gameplay HUD; only obsolete death modal returns to Playing. Actual Pause/Settings menus stay open. Results still use ROUND COMPLETE/REMATCH.
- `R23VrUI.cs`: local-match HP0 no longer forces Dead; nonmatch HP0 preserves YOU DIED. Existing HUD rebuild/cleanup owns the compact notice.
- `R24MatchLifeText.cs`: pure ghost/legacy-modal predicates and source geometry constants. Caption/countdown still comes exclusively from SetLocalMatchLifeState, not a UI clock.

Source `match_hud_r3.gd:33-40,59`:

- Panel .34 x .013 m, color17212a; Godot(-.02,-.333,-.803) -> Unity(-.02,-.333,+.803).
- Text Godot(-.02,-.333,-.798) -> Unity(-.02,-.333,+.798), white, source font32, pixel_size=.0095/32. Existing Text helper takes capital height, so .0095*.72 preserves the source .0095 em without font inflation. Width cap .34 m.
- Exact source ghost/respawn/elimination strings; remaining time supplied by actual Health/Root. No fake peer/score/revive success.

`net_bridge1000.gd:310-345` explicitly enables normal actor input for alive/downed/eliminated while stowing items. UI therefore does not equate HP0 with a modal menu. IsLocalMatchGhost excludes results; notice alone leaves Page=Playing and BlocksGameplay=false. MENU/Pause/Settings have normal blocking semantics. The UI does not grant item input or own collisions/Health.

Checks: `Match/check.py` 236 source rules/life/ghost policy cases PASS; `UI/GhostHud/check.py` frozen source pins/geometry/motion predicate and all33 notice glyph outlines/advances PASS. Existing inventory-font clone adds a star, but every used notice glyph and metric remains identical to original Godot fallback. Installed Runtime API compile PASS. Existing overlay font renderer retained; source Label3D's one-pixel outline is not newly implemented. Native rasterization/movement acceptance is not claimed.

Preserved original menu/capture framing, outfit assets, legacy death, real results/rematch and user settings. No Editor/Root/Rig/Health/World files changed.

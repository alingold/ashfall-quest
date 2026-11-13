# Local R6 match rule selection — ready

Implemented real rule selection, not hosting status. UI files/Match contract are released; no writer remains. No engine, render, build or device run.

- New pure `Assets/Ashfall/Integration/Playable/Match/R24PlayableMatchRules.cs`: 37 typed readonly fields, immutable `With(sourceKey, value)`/`WithMap`, exact source defaults, finite validation, clamps, presets, starting kit and preset predicates. Personal VR preferences are untouched.
- New `UI/R24MatchRulesUi.cs` (partial `R23VrUI`): six source tabs, real sliders/toggles/choice controls, source units and preset labels, explicit START MATCH. `R23UiState` appends `MatchRules`; `R23VrUI` adds the page route/menu anchor and MATCH RULES button at source `(970,655,270,64)`. READY ? spawn choice and its MapSelected callbacks are unchanged.

Root integration:

```csharp
UI.MatchStartRequested += StartActualLocalMatch; // Action<R24PlayableMatchRules>
UI.MatchRulesChanged += StageRules;               // null clears pending selection
UI.SetLocalMatchStatus(actualStatus);
// Only after a production consumer exists:
UI.SetMatchRuleAvailable("infinite_ammo", true);
```

`UI.SelectedMatchRules` is null until deliberate preset/rule selection. Merely opening/backing out does not choose a match. `SelectMatchPreset(id)`, `SetMatchRule(key, value)`, `ClearMatchSelection()`, `RequestMatchStart()` are public seams. Request emits a snapshot using the currently selected installed map; it neither changes Playing nor invokes Explore/Training/StartRequested/MapSelected. Actual Root owns Health, map loading, countdown, zone and results. Default Explore/Training remains deliberate and separate. Contract `WithMap` trusts the installed UI/Root map catalog; untrusted `FromSourceValues` requires an archive/community validator.

Preset IDs: `team_revive`, `team_respawn`, `free_for_all`, `practice`. Practice retains all eight exact source overrides (including catalog and infinite flags). A source flag in the contract does not prove its gameplay owner consumes it.

Enabled custom controls currently match Flight's confirmed local consumers: teams/mode/time/rounds/damage/starting health+shield/walk/fling/starting builds/loadout/loot/retain inventory/respawn delay/protection/downed/zone enabled+damage+speed+wait. Unsupported controls are visibly dimmed and refuse UI edits; Root can enable them individually after binding real owners. Source networking-only controls/roster and inert save/load/balance buttons are omitted from this explicitly local page. Match fling maximum is source 3; personal VR fling remains 0–5.

Source pins are checked against frozen R6 `SOURCE-MANIFEST.json`; hashes in `source-pins.json`.

- `scripts/net_rules1000.gd:3–30`: defaults/limits/choices, strict validation, four presets, source starting catalog. `:32–34`: live-edit eligibility.
- `scripts/front_menu1000.gd:38–46`: tabs/keys; `:76–102`: actual board, controls, presets and START positions; `:280–325`: adjustment/value labels; `:340–365`: slider steps/labels and derived preset indicator.
- Existing board remains 2.20 × 1.375 m, source 1280 × 800 coordinates/font path. Offline page uses MATCH RULES instead of falsely claiming HOST CONTROLS. Source LOCAL rule controls preserve actual source values; disabled labels are muted with one local availability legend.

Checks:

- `python unity/Tools/PlayableR23/Match/check.py`: **210 PASS**, source-pin verified defaults, malformed/finite bounds, typed integers/bools, all presets, source labels, immutable snapshots/arrays, real starting catalogs and live eligibility.
- `check_api.py`: installed Unity 6000.6/URP Runtime API compiled with new UI/contract. A later rerun encountered concurrent Root calls to not-yet-published `InitializeMatch`/`InitializeTeamLaunch`; no UI errors. Root owns the final coherent full compile after those partials land. Source contract still compiles with warnings as errors.
- No native UI or controller acceptance claim. Existing appearance/material/neck fixes and all assets preserved.

# R6 UI focused native fixture ? staged, unrun

Copy **only** `R24R6UiSmoke.cs` and its `.meta` into `Assets/Ashfall/Editor/Playable` after Root's capture hold has terminated. Root routes `new Ashfall.Editor.Playable.R24R6UiSmoke().Run()` from the existing flat `R23SmokeControl` pump. `Run()` flattens its own groups and calls `R23SmokeControl.Finish` once with terminal JSON. No runtime or route files were edited by this task.

Required actual Editor-only ports agreed with Root:

- `Rig.SmokeTrackedHands(HandSample left, HandSample right)`: refuse live XR; publish tracked samples and aim poses from current real hand transforms through the actual rig input path. Called after ordinary `SmokeXrMove` has admitted the real source motor frame.
- `loop.SmokeUiUpdate()`: refuse live XR; temporarily bypass only the ExternalProbePump/ManualSmoke early-return guards and call the real `Update()` with ManualSmoke's hardware branch. Restores its guard flag afterwards. It applies the real queued backpack action; the fixture never invokes `EquipOwned` itself.

`CompileOnlyPorts.cs` contains **throwing extension placeholders**, enabled only by `R6_UI_COMPILE_PORTS`, for compiling against today's pre-port runtime DLL. **Do not copy it into Assets.** It cannot produce a native PASS. Once Root publishes actual instance methods, run `python unity/Tools/PlayableR23/UI/R6BackpackFix/NativeStaged/check.py --real-ports` against the freshly compiled runtime; that excludes the extension file and define. Missing fixture input ports are fixture availability gaps, not gameplay failures.

Eight terminal groups (required for `success`):

1. `ActualRootUiSetup` ? XR-off guard, actual loaded map/lobby/UI/wheel and existing production owners.
2. `ActualLobbyLoadoutRouteAnd180pxButtons` ? true pointer release, LOADOUT/OUTFITS exact source pixel coordinates and 180?64 size, genuine single first gallery preview, > / BACK / reopen reset, no Start/map change or carried mutation.
3. `ActualWeaponSettingsBinding` ? true Settings>WEAPONS pointer route, four actual controls; numeric pointer changes .75 damping/.40 offhand verified on production Hands readonly values; toggle roundtrip.
4. `ActualWheelBackpackAndWalkingAnchor` ? existing root-bound wheel BACKPACK release; real owned/reserve pictures, three initially locked cells; tracked head motion leaves board fixed, actual source collision/motor walking translates it by the real locomotion-anchor delta. Refused walking is a failure with its exact reason; no replacement floor is created.
5. `ActualOwnedActionBackpackEquip` ? real fresh UI grip emits exact retained action ID, closes backpack, real queued root Update equips that owner into the requested hand, without granting inventory.
6. `ActualReviveFooterCallback` ? genuine 256?171 revive image, footer click invokes real root reusable-gear owner, zero-slot inventory unchanged; real charge status remains distinct from control hints. Does not assert a revive/network completion.
7. `ActualBackpackSupplyCapacity3To6` ? three actual decoded world backpack supplies through Supplies.TryCollect and the root grant callback; real shared capacity/unlocked cells 3?4?5?6, fourth refused. No fake SetOwnedLoadout/provider.
8. `ActualSceneAndPreferenceCleanup` ? saves/restores source player local poses/settings/preference key and pump flags; restarts the disposable smoke round, restoring capacity3; fixture supply props hidden. This intentionally resets round loot rather than promising restoration of an arbitrary existing user inventory.

Six captures: `smoke-r6-ui-menu.png`, `...settings.png`, `...weapon-settings.png`, `...backpack.png`, `...backpack-revive.png`, `...backpack-six-slots.png`. Detached full-canvas desktop camera only; production source board/anchor dimensions unchanged. The later capture-only correction measures actual Lobby.MenuSize or the actual settings/backpack backdrop mesh, centers only the probe, and perspective-fits every world corner within a 6% viewport margin. Uses frozen R6 player.gd desktop FOV85/near.045/far650, verified against its manifest; the centered probe pose and 1280x720 capture are explicitly not matched source/HMD framing. Each actual projected corner is checked before rendering. Captures are observations, never automatic visual/HMD acceptance. No new map load or native job occurs during preparation.

Checked: installed Unity 6000.6.8f1/Roslyn runtime/editor APIs compile with warnings-as-errors; two pending port signatures have the explicitly throwing compile-only placeholders. **No Unity execution, shader compilation, GPU, capture, build or device action was performed.**

Capture-only correction after Root inspected the 19:52 images: the former lobby branch copied the inherited rig eye pose and cropped the footer. `Capture()` now frames the complete actual source canvas world corners. Only fixture capture code and an inline pure projection-fit helper changed; semantic routes and Root's `AutoFinish` composite seam remain intact. `capture_check.py` passes 42 CPU perspective-corner/domain cases and installed Editor/runtime API compilation with the real input ports (no stubs). No native/capture/device execution was performed for this correction.

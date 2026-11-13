Entry: `new Ashfall.Editor.Playable.R23ArsenalPickupSmoke().Start()`. Root owns `-ashfallArsenalOnly true` routing and APK gate. `Execute(R23PlayableLoop)` is also callable; read `Passed` / `Json` afterward. Only new `Editor/Playable/R23ArsenalPickupSmoke.cs` +meta changed; no runtime/router/build changes from this owner.

Receipt fields: `success`, `nativeExecuted`, `headsetVerified=false`, `listeningVerified=false`, `error`, `startedUtc`, `finishedUtc`, `groups[] {name,error,passed,observations[]}`. Exactly eight mandatory groups:

1. `ActualRootMapSetup`
2. `ActualSourceDefaultKit`
3. `ActualSourceCarriedFireAndPhysicalReload`
4. `ActualRemoteGripReplacementAndRearm`
5. `ActualShakerStackAtomicWorldEviction`
6. `ActualNoFloorRefusesInventoryMutation`
7. `SevenDecodedPrivateGalleryPreviews`
8. `SceneAndProbeCleanup`

The fixture uses actual loaded map collision, Root/Hands/Consumables, production input frames/reservations/callbacks and native health. It guards against active XR before moving simulated poses. Drop site is chosen from existing map floor and validated by the production drop search. Existing practice actors are placed on existing map floor with clear head→bore / bore→target map queries; no new platform or fake health owner is created. World props are moved away only during fire checks to prevent accidental fresh-grip pickup. The M1014 eight-cast/tracer/group check occurs on its first accepted shot before automatic-repeat checks; full reload uses actual support UP and handle BACK poses.

Replacement checks use typed actual world gun identity/center/pose and immutable native weapon state. A1.35m aimed fresh grip replaces the selected full slot, retains outgoing native identity/ammo/reserve on a reachable map floor, and held grip cannot swap again. Actual source shaker assets enter through `Consumables.Sample`; the root MakeRoom callback drops the selected gun. Second shaker shares the source slot; a free other hand picks an incoming native weapon and evicts BOTH selected members, with actual world custody/poses and no consumption effects. Actual root pickup hint providers are asserted before the incoming grips. Isolated off-map no-floor cases exercise both gun and selected two-member stack refusal, without replacement geometry; inventory/ammo/custody must remain unchanged.

Gallery uses the production `R23ExpandedGalleryFixture`, including seven real decoded preview identities, one visible model, no live private owner while docked, three new-gun captures and cleanup. Root must keep competing input pumps stopped (fixture sets ManualSmoke/ExternalProbePump, disables Rig pump, and uses actual production controls manually).

Finally cleanup clears registered probe supplies, restores source default round/targets, original lobby/play state, tracking origin/raw simulated poses and saved pump/persistence flags. Original source map geometry is never replaced. This is an explicit fresh-round native oracle; it does not preserve prior in-progress private preview ammo. It is intended for root's fresh XR-off test scene, not a running human session.

Validation performed: installed Unity Runtime/EditorRuntime/BuildAndSmoke C# API compilation passes. **No engine, GPU, device, render or build was executed by this worker; no native receipt or PASS is fabricated.** Native `success` is computed only after all eight actual groups execute successfully; errors are logged per group and propagated to terminal failure. Installed/released builds are unchanged.

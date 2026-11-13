# R6 archive pickup scatter ? working composition

Own changes are restricted to Consumables and its tools. No Root, planner, WorldLoot, map, art, physics-setting, or effect-owner edits. Existing checkpoint Range path retains its previous layout/settler.

## Root integration

Namespace `Ashfall.Integration.Playable.Consumables.WorldPickupLayout`:

```csharp
archivePickups = new R24ArchivePickupComposition(Consumables, Supplies);
// AFTER the existing single ArchiveWorld planner is Ready and collision active:
archivePickups.Apply(ArchiveWorldMapId, ArchiveWorldGeneration,
    ArchiveWorldPlan, TryArchiveSourceToWorld, collisionReady,
    forceSourceReset: actualSourceRoundReset);
```

Use the existing `R24ArchiveSpawnPlan.Loot` in source order and the planner's source-to-world mapper. This helper owns no grid/floor query and does not reflect coordinates again. It validates every converted point before changing any item. Success caches map + generation. Repeated normal frames/resume do not move items. `forceSourceReset` only bypasses that cache: **it does not reset owners, grant anything, or restore consumed items**. Root retains responsibility for its genuine source-round-reset sequence.

On leaving archive, call `archivePickups.LeaveArchive()` and the existing checkpoint layout functions for the new map. `R24VRSupplies.EnsureSourceCheckpointLayout` also invokes the same safe archive teardown, so actual nonarchive startup restores six base box visibility. Ordinary resume must not call checkpoint layout again.

## Actual placement/lifecycle

- Nine real food instances + three real backpack supply instances use the existing stable checkpoint IDs and original source row order. Select `Loot[(k*19+11)%N]`, directly assign position, preserve actual rotation. Cancel stale checkpoint settle callbacks; **no new food/bag settle** after scatter. Owned/reserved/removed/collected items stay untouched.
- Hide six source base ammo/build boxes. Create exactly 72 actual supply-owner boxes, family order rifle/pistol/shells/smg/sniper/builds repeated12 times. Select `Loot[(k*13+5)%N]`; run the existing native-render-bounds source settler after actual collision readiness and one fixed boundary. No invented floor, slope filter, source-height shortcut, or amount changes.
- Stable archive map+slot IDs omit ephemeral planner generation. Retire prior uncollected archive-owned presentation/state only; collected event tombstones remain, cannot be recollected or recreated on plan/context refresh. Refuse teardown during an actual grant callback. True source reset remains the only restore path.
- New read-only `Supplies.ArchiveSupplyIds` (copied array), `TryReadWorldVisibility(id,out bool actualVisible)` (actual view active-in-hierarchy), existing `TryReadWorldPose`, `TryReadItem`, and `TryReadSourceSettleResult` expose real state/poses. `ArchiveAssigned` explicitly distinguishes direct food positions from completed physics settling. Composition exposes AppliedMapId/AppliedGeneration/Failure/FoodResult/SupplyResult.

## Source evidence and checks

`../WorldPickupLayout/source-pins.json` validates six actually consumed scripts against immutable R6 manifest SHA b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111. `archive_map1003.gd:151?180` is the scatter; `ammo_pickups.spawn_box` queues settle; source archive enters after its physics wait. No speculative second settle.

`python unity/Tools/PlayableR23/Consumables/WorldPickupLayout/check.py`: **901 PASS**, exact base/archive permutations, family counts/identity, real finite reserve grant once, source food custody/tombstones, available-only teardown and reentrant collection refusal, explicit reset, existing8-corner math.

`python unity/Tools/PlayableR23/Consumables/ArchivePickupLayout/check_reference.py`: **588 observed-source comparisons PASS** against pinned WORLD executed reference `D:/ashfall-r6-staging/world/native/source-loot-reference-v001` (result/script/receipt/PNG hashes in native-reference-pins.json). Personally inspected Alpha food/supply PNGs. At first observed map_ready and +1/2/4/8/16/32 physics snapshots:

- Alpha all12 food/bag XYZ equal selected raw loot exactly, zero delta.
- All72 ammo/build match selected XZ within0.00001526m; Y delta ?0.07983017..?0.07975388m, consistent with raw source box minimum?.075 and source HOVER_HEIGHT.4452 above floor lootY?.6.
- Six old boxes hidden;72 new boxes visible.

Reference `resultPassed=true`, `strictPassed=false`: optional food amount metadata absent and known GL shutdown errors retained. This is genuine source XR-off observation, not clean native acceptance or Unity collision parity. Range source final food lowestY?.4451998; its inherited base ammo Y?.22980009 remains an explicit lifecycle difference from the accepted Unity base implementation; this change does not silently rewrite that e937 baseline.

No engine/device/build was executed by this worker. Root native composition/physics validation remains necessary.

# Frozen R6 checkpoint pickups ? released

Owned helper/consumable/supply source is coherent and HELD. No Root/WorldLoot/Explosives/Map/UI writes, no asset regeneration, no engine/build/device execution. Source4 scripts are SHA-bound in source-pins.json against immutable R6 manifest.

Root integration (already communicated):

```
Consumables.EnsureSourceCheckpointLayout(checkpointUnity, actualCollisionReady);
Supplies.EnsureSourceCheckpointLayout(checkpointUnity, actualCollisionReady);
```

Both return immutable Created/Relocated/Preserved counts. Caller must provide actual loaded/current-map/active-collision readiness (captured map ID and roundStarted/!switchingMap as Root now uses). Defer then crosses WaitForFixedUpdate and Physics.SyncTransforms. No placeholder floor. Ordinary menu resume must not reapply layout; explicit checkpoint/map/round composition is caller-owned.

Exact source layout:

- field_consumables.gd:6,68-81: nine foods KINDS shaker/soda/banana repeating, then three backpacks. Godot offset(-3.4+(i%4)*.65,.75,1.8+floor(i/4)*.65), i0..11; reflect Z once, add checkpoint in Unity coordinates. Backpacks use actual existing R24VRSupplies family/effect, not an invented consumable kind.
- main.gd:197-199 / ammo_pickups.gd:14-20: first rifle/pistol/shells positions(1.5,.3,-2),(2,.3,-2),(2.5,.3,-2). Remaining smg/sniper/builds use FIRST position+i*.55 =>x3.15/3.7/4.25; Unity Z+2. main.gd:584 translates ammo parent to checkpoint. Six ammo/build supplies, not a seventh backpack ammo-row entry.
- weapon_inventory.gd:384-418: stable entity/deletion checks, actual physics boundary, origin prop+UP2/ray DOWN20/mask1/exclude actor; no slope predicate. Lowest locally visible MeshInstance bounds eight transformed corners; hover .75-.3048=.4452. No hit/no visible bounds preserves original placement; wall-mounted skip supported. Converted Sprite3D glyph meshes have an explicit ignore marker because source Sprite3D is not MeshInstance3D. Labels are not included.

Stable IDs: first3 preserve r23-starter/shaker,soda,banana. Additional foods r24-checkpoint/food/3..8; bags r24-checkpoint/backpack/9..11; ammo r24-checkpoint/ammo/{rifle,pistol,shells,smg,sniper,builds}. Every row is a separate real instance. Reapply uses real available-world relocation and never resets/clears owner state. Owned, consumed, reserved/refused and collected items are preserved. ONLY existing explicit ResetRound can rehydrate; no source health/effect/ammo amounts changed. Food settle preserves actual rotation through additive RepositionWorldItem(id,position,rotation); original drop APIs remain unchanged.

Read-only native inspection:

- both owners.SourceCheckpointIds (9 food IDs; 3bag+6ammo IDs).
- food.TryReadWorldItem(id,out position,out rotation,out owned).
- supplies.TryReadWorldPose(id,out position,out rotation,out collected).
- both.TryReadSourceSettleResult(id,out R24PickupSettleResult): immutable Id/Outcome/RequestedPosition/Position. Outcomes Pending,Settled,UnchangedNoHitOrMesh,Cancelled,Refused,Preserved,Unavailable,WallMounted. Settled requires actual owner setter acceptance. Pose receipt is a snapshot; use world-pose API for current held/drop pose.

Generic shared API namespace Ashfall.Integration.Playable.Consumables.WorldPickupLayout:

```
R24WorldPickupSettler.TryReadSettledPosition(prop,actor,out position,wallMounted=false)
R24WorldPickupSettler.Defer(prop,actor,collisionReady,stillAvailable,acceptPosition,
                          wallMounted=false,finished=null)
```

Defer checks current GetEntityId, actual scene lifetime, captured caller custody/generation predicate before and after physics. Accept callback owns authoritative item state; helper creates no inventory/geometry or damage. Hands has already composed it with real grenade state/revision guards. Existing calls remain compatible; finished is an additive optional outcome callback. Original .meshbin/material/audio payloads are unchanged.

Checks:79 actual source/layout/8-corner/finite native reserve+Health owner/tombstone cases PASS; installed Runtime API compile PASS, warnings as errors. Physics/ray/render settling is UNRUN. Archive/community source _place_loot overrides base positions (including archive72 ammo) remain a separate explicit source integration; no full archive loot-parity claim.

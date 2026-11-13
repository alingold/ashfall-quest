# Original R23 FRAG and ZONE

Released for Root composition. Owned files are only new Integration/Playable/Explosives, Tools/PlayableR23/Explosives and StreamingAssets/PlayableR23/Explosives. No Root/Hands/RoundState/UI/shared audio edits. No engine/GPU/device/build/Blender activity; immutable source and installed e82 release are untouched.

Public component: `Ashfall.Integration.Playable.Explosives.R23VRExplosives`.

```csharp
service.Initialize(actorRoot, acceptedHead, acceptedLeft, acceptedRight, realEffects);
service.SetOtherSlotsUsed(actualWeaponAndConsumableSlotCount);
service.SetSfxVolume(sfxVolume);
// Projection only; safe before root motion/objective arbitration. Does not fire/advance time.
service.UpdateInputAvailability(frame);
// Root invokes TryPickup(id,hand,actualHandAvailable) for the winning fresh grip.
// Invoke once per host frame to advance item controls and latch simulation eligibility.
service.Tick(dt, frame);
```

Frame is `R23ExplosiveInputFrame`: Enabled, LeftReady, RightReady, LeftTrigger, RightTrigger, LeftTriggerClick, RightTriggerClick. Hand API is1left/2right. Enabled is global gameplay/session eligibility, **not** whether a grenade is currently held: launched projectiles continue while the player holds something else. Projectile motion/fuses use their owned native Rigidbody FixedUpdate. Root remains the sole player-root motor; no tracked pose or health field is written by these files.

Finite inventory:

- `Spawn(id,kind,position[,rotation])` registers ONE real world payload. Kinds are frag/zone; duplicate ids and spent ids cannot be registered again. There are no implicit inventory grants during initialization/equip/map visibility changes.
- `GrantOwned(id,kind,hand,handAvailable)` is an explicit initial-item entitlement, still capacity-checked and id-unique. Root controls when a new starter entitlement is authorized.
- `TryReadItem(id,out R23GrenadeItemState)` returns Id/Kind/Slot/Owned/Spent. `CanCollect(id)` checks Ready/capacity/unowned/unspent; `CanStore(kind)` checks remaining source capacity/stack room. Never parse a kind from its id.
- `OwnedItems` returns immutable R23OwnedGrenade records Id/Kind/Slot/Label. FRAG labels FRAG; zone labels ZONE GRENADE. FRAG pairs stack two in one local slot; ZONE is one per slot. Slot numbers are local0..2; Root merges the three source domains. `SetOtherSlotsUsed` must be updated from actual external slot occupancy.
- `TryReadAimedWorldItem(hand,out id,out center,out distance)` and `TryPickup(id,hand,handAvailable)` use real corrected-hand aim,2m limit, forward dot>.94 beyond .35m, plus head-to-hand and hand-to-item world LOS. They require Ready and a prior Tick/UpdateInputAvailability with Enabled/hand readiness. Root owns free-hand custody and grip priority.
- `TryEquip(id,hand,handAvailable)`, Stow(), Drop(id,position,rotation), Drop(id). Equip/drop preserve the same source item. No refill.
- `DropSlot(slot,positions[],rotations[])` validates all member counts/finite poses first, then atomically drops the complete owned group with one InventoryChanged callback. Member order is OwnedItems filtered by Slot. Root supplies reachable, collision-safe ground poses.
- `Bounds GetDropBounds(id)` (also bool/out overload) returns actual scaled **local** mesh bounds for ground placement, excluding current world pose.
- `SetWorldVisible(bool)` and `SetWorldItemVisible(id,bool)` affect unowned visuals/aim eligibility. `RepositionWorldItem(id,position)` only accepts live unowned/unspent items and updates both Position and InitialPosition for the current map home.
- ResetRound() removes launched bodies/effects, clears owned/spent state and restores registered world items at their current source-layout homes. Only explicit round restart restores spent entitlements. ClearProjectiles() cancels immediately without refunds; Suspend() freezes simulation/voices and rearms input neutrally.

HeldId/HeldHand/LeftOccupied/RightOccupied expose real custody. A fresh accepted trigger press launches; release only rearms. Selection imposes source .25s draw delay. A thrown item is permanently spent; if another FRAG occupied its stack, that existing member becomes selected with its own .25s draw/rearm. The source per-instance throw cooldown is .35s, but that spent instance cannot be reused. Original rack frag/zone reserves12 exist, but grenade code has no reload/refill consumer; this service deliberately does not turn those unused counters into infinite auto-refills. Latest arsenal_data071 sets each physical grenade instance to1 payload, superseding older explosive_weapon.setup's2 default.

Damage port is `IR23ExplosiveEffects`:

```csharp
R23ExplosiveTarget[] ReadDamageTargets(); // real ids + Core.Float3 source centers
bool ApplyTargetDamage(string targetId,float amount,Float3 origin);
bool ApplyPlayerDamage(float amount,Float3 origin);
```

Root maps these to the real Hands/Health/constructed-cover owners. Targets exclude the player; enemy centers include source +.7m, practice/build centers are their source roots. A blast snapshots all target amounts and current accepted player head before any application, preserving source panel-destruction ordering. It then calls each real effect owner once; it stores no HP/shield and performs no fake success increments. TargetDamageMultiplier applies only to outgoing targets, matching source field-menu scaling; self uses the original raw radial amount. Actual source main.gd has **no blast LOS exclusion**. Throw authorization does require head-to-launch LOS and spends nothing through a wall. Root's new actual target damage bridge may be used without a gun pending-shot token.

Source mechanics:

- Launch origin is corrected grip local(0,.045,+.10) in Unity. Existing real WeaponHandling.SourceVelocity implements the shared grenade_ballistics recipe: world-up45-degree elevation, gravity9.8, calibrated range29.2608*48/41.5, physical hand-height correction and source max-range cap. Measured wrist velocity is not the final source throw speed. Preview and launch call the same function. Preview uses .06s ray samples, max100 and source ribbon half-width .042, stopping at first geometry contact.
- Frag fuse3.5s; radius10m; power150; linear radial falloff. Native sphere radius .035m, mass .35kg, zero explicit linear damping, continuous collision and custom gravity9.8. Source ground-roll correction decelerates tangent velocity4m/s², limits roll to3.048m and reduces spin8rad/s².
- Zone sticks on first native contact, seats its actual mesh above the contacted surface, waits2.7s, then shows the source translucent purple sphere. It shrinks from15m to0 over10s. Exact .5s boundaries deliver5 constant damage;20 boundaries produce19 positive-radius damage callbacks (last zero-radius boundary causes no health mutation). A never-contacted zone expires8s without a fake frag blast.
- Warning schedule uses original .09..duration-.09 power-curve beats; audio collapses missed beats into one current cue and never plays a late warning over the blast. Pause/focus eligibility freezes source fuses and body velocity, and pauses voices. Reset/cancel prevents old callbacks even before deferred GameObject destruction.

Events: InventoryChanged, Thrown(R23GrenadeLaunch), Blast(R23GrenadeBlast), Haptic(hand,strength,duration). Throw haptic is source .45/.06. Launch receipts contain immutable itemId/kind/hand/origin/velocity. ReadProjectiles() reports native position/age/zone age/radius/ticks/stuck/active/ground roll; LiveProjectiles, TrajectoryVisible/TrajectoryPoints and HeldGripContactWorld are diagnostics. Root may override LaunchLineClear for its existing collision-query owner. Default launch/preview queries use native world layers0/8/19 and exclude the actor.

Live projectile sphere uses new layer21 (source projectile layer16), ignores the actor's colliders and other grenades. Source bullet/interaction masks exclude projectile layer16; Root should exclude Unity21 from gun/climb/support ray queries that otherwise use DefaultRaycastLayers. Visual bodies are18 and nonphysical cue meshes2. Layer19 is ballistic-only map and20 player-only map; neither is repurposed.

Source assets are the actual original variant routing: FRAG uses download_frag_alt/model.glb at heldscale .9 (34634triangles); ZONE uses download_frag/model.glb at heldscale2 (2434triangles). Flight visuals retain source ratio .035/.047 independently of the real .035 collider. The original FRAG alternate had historical source decimation already baked; this port does not add decimation. Source meshes, textures and CC-BY attributions are retained. Base/normal/emission bytes are source images; metal/rough packing includes actual material factors; emissive factor/strength and occlusion strength are applied. The original KHR_materials_specular extension metadata is preserved, but URP metallic Lit does not reproduce its textured dielectric-specular term; exact native PBR parity is not claimed.

Six disjoint audio cues: original throw/frag blast/zone bloom WAVs; authored source zone_arm WAV; CPU reproduction of original .065s24kHz warning tones1210Hz/1570Hz. WAV and PCM hashes plus original recording attributions are in payload provenance. Local gains/ranges: throw -10dB/14m with .985..1.015 pitch; warning -7dB/60m; zone-arm0dB/35m; burst -4dB/100m. SFX volume supports mute/restore;16 reusable effect/throw voices plus one warning voice per live body. Source flash light has range10/energy3 and fades3-9*age. Full fire/smoke/shard shader effects are not ported; source GLBs, trajectory, zone mist, light and actual sounds are active. Maximum live projectiles64; admission at that bound leaves the item unspent.

Checks PASS:136 finite inventory/ballistic/fuse/zone/warning/actual HealthService cases;38 original geometry/material/emission/WAV/PCM checks; installed runtime/editor-runtime/build-smoke Unity API compilation. Reproduce with installed Python: Explosives/prepare.py, Explosives/check.py, PlayableR23/check_integration.py. Payload34,152,262 bytes. CPU fixture proves frag damages actual shield then health, later source gains restore those real values, and zone uses actual health owner across exact tick boundaries. No native contact/solver, visual/shader, hearing or headset test was run here; Root owns the proposed native fixture/build slot.

# Original R23 M1014

Ready for Hands/Gallery composition. New live scripts are disjoint `HandsWeapons/R23M1014Owner.cs`, `R23M1014Mechanics.cs`, `R23M1014Controls.cs`, `R23M1014View.cs`; no root, HandsWeapons, RoundState, SourceRifleOwner or Gallery file was edited by this worker.

```csharp
// One stable retained owner per logical gun; do not recreate it on equip/map changes.
var owner = new R23M1014Owner(creationContext, instanceId,
    new R23RifleReserve(480), authorityGate); // or shared finite shells-family reserve
var controls = new R23M1014Controls();
var view = activeAssetHost.AddComponent<R23M1014View>();
view.Initialize(correctedGripParent); // no movement/rotation of tracked hand

var projection = controls.Project(inputReady, primaryClick, clickAllowed,
    primaryStick.x, primaryStick.y, desktopReload);
R23M1014Volley volley;
owner.Apply(context, projection.Action, input, view.FiringPose(),
    out volley, projection.EjectAfterShot);
view.Present(owner.Snapshot, owner.Reserve, leftHanded, visible, dt);
// On holster/pause/focus loss/selection release:
controls.Reset(); owner.SuspendControls(); view.Suspend();
```

`input` is the existing `HandlingInput`: enabled/tracking/pause/climb/head-obstruction flags, sequence, dt, actual support grip, **unrecoiled corrected-grip gun-local** SupportLocal, physical ForegripDistance and LeftHanded. dt is0..0.25 seconds; use one Sample per authoritative frame with unique command/sequence. Sample already fires a held trigger; do not follow it with another Fire in the same tick. Primary click source-ejects before fire; stick-down projects EjectAfterShot so it retains original fire-then-eject order. ClickAllowed preserves left-stick sprint ownership. Source dead zones: stick neutral<.25, back<−.7 and |x|<.5, all rearmed after neutral. Desktop ReloadStep follows ready→eject/refund, needs-mag→UP insert, needs-rack→BACK chamber.

The new partial extension uses actual existing WeaponHandling ammo, reserve/refund, replay/time, support and lifetime admission. Its underlying counter mechanism is configured ServiceRifle capacity6 with M1014 cadence/gestures/recoil; the public source identity remains `m1014`, catalog9, family`shells`. Creation explicitly authorizes the six initial rounds; retain the same owner across equip/drop/map changes. Snapshot is the existing HandlingSnapshot, and Reserve is the actual injected reserve. No separate display ammo counter is added. GripToggle follows the existing native support policy.

Current source supersedes old tube/chamber tests: `auto_shotgun.gd` is a **six-round UP pack**, not per-shell top-up. Grab requires a fresh support-grip at offer(0,−.25,+.14), move upward≥.10m to socket(0,−.10,+.14), then release/regrip handle(±.06,.045,+.10) and pull Unity-local −Z≥.085m. Reserve is debited only at completed pack seating, up to actual available shells; seated rounds stay unavailable until BACK completion. Manual eject refunds actual loaded/seated rounds. Acquisition, jitter, release, tracking/reservation loss and suspension cannot manufacture shells.

Cadence comes from original `one_star_050.json`: **.2907166667 seconds**, automatic and identical in one/two hands. Latest `one_star0991.json` supplies **77 body/head total**, eight pellets of9.625. Each deterministic direction uses angle2πi/8 and tan(3.5°) radial spread alternating .4/1; no random scatter or invented head multiplier. An accepted `R23M1014Volley` contains immutable copied directions, source instance/command/shot, origin and before/after snapshots after one actual shell debit. Range40m; `R23M1014Definition.DamageAtDistance` retains full damage through8m, linear falloff to25% at25m, then clamps. Host must perform actual nearest native rays, group damage by target/collider once per volley, and apply XR head→muzzle obstruction after discharge as original main.gd does. The owner does not invent target hits or apply fake health.

Source recoil is the authored086 plateau experiment: after first current-zero shot,2° supported/3.5° unsupported pitch, yaw0, .075m recoil accumulation×.38 when supported capped.13; decay9/s, pitch recovery after.35s, reset.8s. Existing source presentation gain1.25 remains in Snapshot. This deliberately does not install a supposedly measured M1014 recoil curve. ReloadCue and HapticAmplitude/HapticSeconds/HapticSupport report source stage/shot feedback; last-round auto-eject follows source final .24/.07 primary haptic.

Presenter API: Initialize(Transform), Ready/Failure, Model/Muzzle/Bolt/Pack/Arrow, FiringPose(), Present(snapshot,reserve,leftHanded,visible,dt), Suspend(), FlashSeconds. It creates only visual descendants. Exact body and imported irons remain in`m1014`; local moving Bolt geometry loads separately as`m1014-bolt`, with home applied exactly once. Model scale is1: original fit scale .698799 is already baked and must not be applied again. Actual muzzle(0,.045,+.77), foregrip(0,−.025,+.36), rendered-hand-only physical offset(0,+.0381,−.0381). Left dominance mirrors handle X/bolt visual scale only. Shot bolt cycle .095s/.038m is cosmetic; it cannot rack or load. Pack remains all six source shells whenever offered, including partial finite reserve. Source UP/BACK arrow is genuine procedural geometry/colors/emission.

Payload load names: `m1014`, `m1014-bolt`, `m1014-pack`, `m1014-arrow`. Original **76,594 body +2,548 bolt=79,142 triangles** are retained without decimation; pack1,584/arrow36 come directly from original source recipes. Static pack/arrow parts batch by original material (4/1 render surfaces), with no geometry change. The separate bolt manifest lists only its actual used material, avoiding duplicate unused texture allocation. Twenty-one embedded PNGs are byte-identical; seven packed MR images convert source B-metal/G-roughness to Unity R-metal/A-smoothness exactly. Model attribution and CC-BY-4.0 source links are preserved.

Exact current audio is in `m1014-audio-cues.json` with seven unique copied WAVs. Hands owner has merged weapon/action/files into shared audio.json. Use existing R23Audio.Load/Play("m1014",cue,...)/SetSfxVolume; View intentionally owns no second audio bank. Current0992 routes **shot_m1014_last_00.wav for every discharge**, despite its legacy filename; current routing supersedes older096 choices.

Checks pass:89 actual native-owner cadence/pellet/finite reload/control cases at72/90/120Hz,117 exact source-art/material/audio checks, and installed Unity runtime/editor-runtime/editor build-smoke API compilation. Gallery independently reported13 real private-owner cases and API compilation passing using the same owner/view/controls. Commands: `python unity/Tools/PlayableR23/M1014/prepare.py`, `python unity/Tools/PlayableR23/M1014/check.py`, `python unity/Tools/PlayableR23/check_integration.py`.

No engine/GPU/render/build/Blender/device job was run. Full imported model is the original high-geometry PC option; Quest rendering/performance, physical recoil/reload feel and production multi-target pellet damage require coordinated native/headset checks. Cosmetic casing physics, multiplayer transfer/save parity and source per-weapon aim-filter presentation are not claimed by this bounded owner/presenter port. Frozen installed93d331 remains untouched.

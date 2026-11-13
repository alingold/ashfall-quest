# Curb launch / glide transition repair

Live source fixes are complete; motion file ownership was released back to root. No controls, calibration, UI or input projection files were edited by this worker. No engine/GPU/device job ran.

**Curb/ramp cause reproduced through the real old owners:** `PlayerMotorFeedback.Resolve` derived velocity from total root displacement, including `HeadCollision.ResolveSupport` elevation. A 12 cm support correction in 10 ms became +12 m/s; `Locomotion` retained positive grounded velocity and applied only gravity, so the next step launched upward at +11.838 m/s. Original R23 `player.gd::_resolve_support` instead sets `velocity.y=0` after positioning feet on support.

`R23VRRig` now separates the accepted support correction from physical movement feedback. `R23RootVelocity.Accept` zeros supported vertical velocity, preserves unsupported jump/fling/fall, and clips upward ceiling contact. Climb and Flight feedback receive the physical phase without support correction. Slope drop allowance uses the actual selected motion owner's velocity. Root remains the sole pose publisher; tracked local head/hands remain untouched. `LandingMotion` keeps a <=5 mm positive numerical gap positional rather than converting a downward request into an upward motion request.

**Glide cause:** `FlightMachine.Calculate` counted its own controlled descending glide as free fall. Holding forward for one second could override spread-hand glide with Dive, and quick glide release/reentry inherited that timer. Root explicitly authorized the small shared-Flight branch repair. Actual controlled glide now resets free-fall timing and takes precedence while requested. Arms folded + forward for a genuine one-second fall still enters Dive; source gravity16.2, dive acceleration96, steering, bank, glide ratio and pad clearance are unchanged. This is a deliberate user-requested repair beyond the buggy original Godot behavior, not a full byte-parity claim.

**Concrete free-hand difference:** original `hand_wall_r2.gd` explicitly requests ray back faces. Unity's proxy ray relied on the global default and could miss an oppositely wound single-sided wall. `R23VRClimbing` now tests the reversed segment too and chooses the nearest source-side contact. It does not change global Physics flags, raw tracked poses, climbing anchors, reservations or fling gains. This repairs the interaction proxy; it does not claim to add a separate glove-render collision owner.

Validation:

- `python unity/Tools/PlayableR23/MotionFix/check.py`:86 focused CPU cases pass. These include the actual old feedback/motor numerical reproduction; support rises/drops at1–100 ms; real unsupported jump/fall/ceiling behavior; eight repeated glide/release/reentry cycles; genuine free-fall Dive; source pad clearance and glide handoff.
- `python unity/Tools/PlayableR23/MotionFix/check_native.py`: installed Unity6000.6.8f1 runtime, EditorRuntime, BuildAndSmoke and staged fixture compilation pass with warnings as errors. Compilation starts no engine process.
- Existing Activities managed mechanics:55 assertions pass. Its older isolated native compile misses a new HandsWeapons reference added outside this task; the complete integrated API compilation passes.

Staged native entry: `Tools/PlayableR23/MotionFix/Staged/R23MotionFixSmoke.cs`, class `Ashfall.Editor.Playable.R23MotionFixSmoke`, public static `Run(R23PlayableLoop loop)` or `Run()`. Copy it into root-owned Editor/Playable for the next coordinated native slot. It creates an isolated actual Rig/motor/head hull/Flight adapter and tests4/25/120 mm native curbs at5/20 ms with both source hull and capsule, a real26.6-degree ramp, sustained forward glide/release/reentry, genuine Dive, and a back-facing native hand wall while preserving tracked local poses. It does not modify the live loop rig. Fixture is API-compiled but **not engine-run or headset-verified**.

New read-only diagnostic APIs: `Rig.MotionVelocity`, `Rig.Supported`, `Climbing.LeftContactProxy/RightContactProxy`.

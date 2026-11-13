# Original R23 traversal audio

Ready for root integration. `Ashfall.Integration.Playable.AudioTraversal.R23TraversalAudio` is a host-driven MonoBehaviour. It does not move the rig, sample controls, create another listener, or run its own Update.

```csharp
var traversal = gameObject.AddComponent<R23TraversalAudio>();
traversal.Initialize(Rig); // after Rig.Initialize creates its head/listener
traversal.SetSfxVolume(sfxVolume);

// Once per accepted gameplay movement frame:
bool crouched = Calibration.Calibrated &&
    Rig.Head.transform.localPosition.y < Calibration.Receipt.TrackedStandingEye * .75f;
traversal.Tick(dt, Rig.Supported && Rig.SprintTier > 0, crouched, blocked);

// Round restart, respawn or map relocation:
traversal.ResetRound();
// Loading/menu paths which stop calling Tick:
traversal.Suspend();
```

`Tick(R23TraversalSample acceptedMotion,float seconds)` also accepts an explicit accepted-velocity/support/flight sample. The convenience rig overload reads `MotionVelocity`, `Supported`, actual flight `Mode.Glide`, anchored hands/mantle, actual tracking and XR session focus. Sprinting is the real latched motor state, not the sprint-button toggle. Crouch is supplied from source physical-baseline rules. Destroy/disable, application pause, and inactive rig focus stop voices and reset transient clocks. Desktop mirror focus alone does not override active XR focus.

`Ready`, `LoadedClipCount`, and `LoadFailure` expose asynchronous loading; `LastFrame` exposes the most recent gait/glide output. `FootfallPlayed` reports a submitted walk/sprint/landing and selected surface. `SurfaceResolver` optionally supplies original surface metadata for a native collider. Default routing applies source metal/steel/grate, wood/timber, gravel/dirt/stone matching to collider/ancestor names; unknown map chunk names use concrete. Queries skip the rig, triggers, and ballistic-only layer 19 and use the existing source-hull ray owner when available.

Exact original source: `C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source/scripts/ambience.gd`, `audio_bank.gd`, `reference_audio_099.gd`, and the current `assets/audio/reference0992/manifest.json` routing. Eighteen WAVs (1,912,536 bytes) were copied with no PCM processing: six metal steps, two concrete steps, three concrete sprint steps, three gravel steps, three wood steps, and the 16-second continuous flight cue. Source Apache-2.0 license and original source attribution files accompany the payload. Its legacy reference filenames now hold the source project's original public procedural audio, as stated in those attributions; no third-party game's recordings were added.

Behavior retains source .48-second walk/.33-second sprint cadence above .35 m/s, one transient per frame even at extreme walk gains/stalls, no walking sounds while crouched/climbing/gliding, landing only after accepted descent below -2.8 m/s, source surface fallbacks, walk/sprint/landing gain (-22/-15/-18 dB), .96–1.04 pitch variation and landing multiplier .84. Glide target is clamp((horizontal speed−2.5)/8,0,1), fade 2/s, linear volume gain .45*.6=.27, pitch .85+.3*gain. User SFX gain multiplies these voice gains, matching the existing Unity source-audio pattern. Loop stops at zero gain rather than playing an inaudible floor; pause/focus/reset also discard prior landing velocity to avoid stale resume sounds. Variant order follows source indexing; pitch jitter uses a deterministic .NET PRNG, so its sample sequence differs from Godot's PRNG.

Validation: `python unity/Tools/PlayableR23/AudioTraversal/check.py`: 42 exact routing/byte identity/PCM checks and 45 actual C# state cases pass, including extreme speed cadence, stall/no catch-up burst, landing thresholds, crouch/climb suppression, glide speed/fade/reentry/release, reset/resume, invalid samples and source surface routing. `python unity/Tools/PlayableR23/check_integration.py`: runtime, editor-runtime and editor/build-smoke compile against installed Unity APIs pass. No engine, audio device, headset or build was run for this work.

Remaining audible scope: PCVR/Quest listening and relative mix remain untested. Wind, turbines, creaks, impact voices and climb are outside this bounded movement slice. Original climb has no baked `climb_00.wav`/`climb_01.wav` and uses Godot runtime synthesis; no unrelated cue was substituted. Constructed/map surface metadata unavailable through names can be supplied through `SurfaceResolver`.

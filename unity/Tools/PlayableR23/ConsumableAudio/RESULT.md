# Original R23 consumable audio

Working component and small source payload are ready for Root composition. Only new uniquely named consumable audio files, its Audio payload folder and this tool folder were edited. No gameplay/Root/shared Hands audio files changed.

```csharp
// namespace Ashfall.Integration.Playable.Consumables
var audio = gameObject.AddComponent<R23ConsumableAudio>();
audio.Initialize(Rig.transform);
audio.SetSfxVolume(currentSfxVolume);

// Source _process first ages the previous motion window. Include true XR focus,
// actual input eligibility, tracking, death, pause, inventory/menu policy here.
audio.Tick(dt, heldPropCenter, enabled);
// Then forward accepted local actions, not button attempts:
audio.Notify(R23ConsumableAudioEvent.Equip, kind, heldPropCenter);
// Optional raw source shake qualification while the actual phase is Shake:
audio.SampleShake(actorLocalHand, heldPropCenter, dt, preparingShaker);
// Or, if the real host already qualified motion / desktop E-held input:
audio.ShakeMotion(heldPropCenter);

audio.Suspend();   // Stop voices immediately when no normal Tick occurs.
audio.ResetRound(); // Also reset source event history and diagnostic counters.
```

`SetEnabled(true)` permits event callbacks before the first Tick. Default is disabled. `Notify` returns true if a loaded cue was played; it never approves an inventory action. Missed loading-time cues are not queued or replayed. No independent Update, input polling, pickup/equip mutation, effects callback or tracked-pose writes occur. `SampleShake` needs actor-local hand position, not world travel or the consumption anchor; Tick needs held-prop world center. All source filenames are Android StreamingAssets-safe via UnityWebRequest.

Source events from preserved original `field_consumables.gd`:

| Event | Required real trigger | Voice |
| --- | --- | --- |
| Equip | Successful equip/draw | Nonspatial source swap cue, -8dB |
| Stow | A real held item stowed with source play_sound eligibility (actor enabled, alive, unpaused) | Same swap cue, -8dB; stops shake |
| Open | Accepted soda opening, or final banana `open_item()` | Spatial subject-specific opening, -12dB/max12m |
| Peel | Each real accepted banana pull of at least .10m | Banana opening, same spatial voice |
| Ready | Actual shaker charge transitions to Consume | Shaker-ready spatial cue; stops shake |
| Consume | Actual accepted item consumption/effect transaction | Subject drink cue; stops shake |

Do not emit Stow for the silent `stow(false)` after consumption. Root may deliver a real switch as Stow then Equip: two reused UI voices preserve their overlap. Source banana's fourth accepted peel calls `_play_cue("open")` and immediately `open_item()`, which calls it again on the same action voice. Deliver Peel then Open on that transition; the second call replaces the same clip at its start. No sound for rejected peel/mouth contact/full vitals or merely pending soda gain ticks. Banana has no reference consume_drink mapping, so the actual authored `sfx/consume_drink_00.wav` and `_01.wav` fallback is included. Source sequence variants increment for cue events and saturate at the original64-event history size; UI and shaking use variant0.

Shaker policy retains source0992: qualifying actor-local travel speed is inclusive .45..8m/s with travel strictly below .3m and positive dt. First sample is silent. Each accepted motion refreshes .20 seconds. The dedicated non-looping source shaker recording pauses when that window expires and resumes at its existing cursor on the next qualifying motion. It restarts only if it naturally finished; it does not restart for every movement or become an infinite loop. The direct ShakeMotion input is also suitable for accepted desktop E-held preparation. Ready, stow, equip, consume, suspension and reset stop it. Finite-input rejection and re-priming prevent discontinuous invalid tracked samples from creating a cue. `SampleShake` false phase clears its previous sample, without inventing a gameplay phase.

Four bounded reused AudioSources: two UI, one replacing spatial action voice and one paused shaker voice. The action voice stays at its event world position while shaker follows the live held center. Source local gains are `10^(-8/20)` and `10^(-12/20)` multiplied by the host's SFX setting; no separate global bus is mutated. Physical max distances are12m/action and8m/shake, Unity minimum distance1. Lifecycle pause/disable/teardown stops voices; host supplies actual XR focus rather than desktop-mirror focus. Teardown disposes owned clips and voice GameObjects.

Read-only diagnostics: Ready, Failure, LoadedClipCount, PlayedCueCount, ShakeStartCount, ShakePaused, ShakeSecondsLeft, LastAction, LastKind. The pure `R23ConsumableAudioPolicy` exposes source cue/gesture/timing behavior without an audio engine.

Ten original byte-identical PCM16 WAVs total930,680 bytes;14 source action routes deduplicate those clips at runtime. Source hashes, exact PCM hashes, channels/rates/frame counts/peaks/RMS and selected original source recording records are in payload provenance.json. Original recording attributions and authored-fallback provenance are copied verbatim. Reference recordings retain their source private-playtest scope and original owners; this work claims no new general redistribution license.

Checks:94 real policy cases PASS;34 original route/WAV/PCM checks PASS; all installed Unity runtime/editor-runtime/build-smoke API compilations PASS. Reproduce with the installed Python runtime: `ConsumableAudio/prepare.py`, `ConsumableAudio/check.py`, then `PlayableR23/check_integration.py`. Preparation reads only immutable original source and writes this small new payload. No engine/audio-device/listening/render/build/device/Blender job was run. Actual decoded playback, spatial attenuation, pause cursor behavior and headset balance require future native listening. Existing APKs and historical builds remain untouched.

# Signal Through Snow — original ASHFALL score

Composed and synthesized specifically for this project, 2026-10-18. No third-party recordings, melody quotations, sample packs, account services, or generative audio services were used. The generated music and composition source are dedicated to CC0 1.0 for unrestricted project use: https://creativecommons.org/publicdomain/zero/1.0/ . Python and NumPy are generation tools only; their code is not embedded in the WAVs.

`compose.py` reproduces three 48 kHz stereo PCM16 stems from a fixed random seed. Each lasts 40 seconds, sharing 96 BPM and sixteen bars in 4/4. The harmonic sequence is Dm(add9), Bbmaj7, F(add9), C(add9), Gm9, Bbmaj7, Csus2, Am7, two bars each. Close pad voicings and a sparse piano motif establish exploration; bowed-string pulses, rounded bass, soft hats, kick/snare and tom fills add combat energy. An upper sustained countermelody and lower piano response give extraction a broader, hopeful character while retaining compatible harmony.

Synthesis uses additive/decaying harmonic voices and shaped noise percussion. Circular stereo reflections preserve tail continuity across the loop; wrapped event releases prevent chopped final notes. A tiny distributed endpoint correction ensures sample continuity. The stems share one gain scale so their fully summed waveform stays below -2.15 dBFS. `analysis.json` records exact peaks, RMS, frame counts, boundary steps and SHA256 hashes.

`scripts/music.gd` starts all stems together, crossfades intensity over 2.5 seconds and extraction over four seconds, fades menu pause, and ducks approximately 12.4 dB during radio. Music is intentionally non-spatial; spatial world/weapon/voice cues are handled separately. Its Music bus and linear volume API allow user adjustment.

Validation is numerical and headless. No human listening, physical headset sound, subjective mix quality, or hardware spatial-audio performance is claimed. Musical balance should be evaluated by listening before final release.

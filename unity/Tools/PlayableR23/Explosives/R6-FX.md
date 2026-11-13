Published R6 source explosion graphics are now active in grenade runtime, superseding RESULT.md's earlier full-FX limitation.

FRAG detonation and ZONE activation automatically invoke exact source mesh/shader recipes: seven fire lobes, nine smoke puffs, pressure band only over a real nearby upward world hit,20embers,9debris; purple ZONE three40×6torus rings/core/28shards; source color/expansion/fade/gravity curves and range10/energy3 light. R6 `explosive_effect.gd` and `explosive_projectile.gd`, original model images and audio bytes are individually bound to frozen R6 by check_fx.py. Source local coordinates reflect Z exactly once; shader cloud noise evaluates original Godot local coordinates.

`R23VRExplosives.PlaySourceVisual(string kind,Vector3 center)` returns bool for frag/zone/smaw while gameplay simulation is enabled. It is graphics-only: SMAW owner keeps actual projectile, damage, finite reserve and impact audio. SMAW source expansion gain1.12 is supported. `ReadSourceVisuals()` exposes Kind/Position/Age/Active/PressureGrounded; `VisualFailure` reports missing resource. Hand-off sent to Hands owner.

Eight effect instances share static meshes and reuse materials/fixed combined shard/debris arrays. Source lifetime2.5s. Ninth concurrent effect reuses oldest pool slot; no gameplay damage is dropped. Tick pauses age/animation when gameplay is disabled; ResetRound/ClearProjectiles withdraw effects. Audio tails now finish naturally after visual end; reset still stops them. No new collider, health counter or actor transform write.

Checks: source motion sampled across250timesteps, source fibonacci directions/once-reflection/fade/rocket size; retained136 inventory/ballistic/fuse/zone/warning/actual HealthService and38 source model/material/audio checks; all installed Unity C# API targets pass. No shader compilation/render, native contact, headset or audio listening performed here. URP translation preserves source equations; cross-engine lighting/color/transparent overlap remains to be visually compared. Original source/history/APKs untouched.

Reproduce CPU checks: `Explosives/check_fx.py`, `Explosives/check.py`, `PlayableR23/check_integration.py`.

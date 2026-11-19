# WPN-BULLET-SPEED audit (RANGE workstream, 2026-11-15)

Checkout: `~/ashfall/unity`, branch `playtest-r12`. File references are repo-relative.

## Summary

- **Every firearm is hitscan.** There is no bullet speed anywhere in the game. When a shot is accepted, the local client raycasts and applies the impact, spark and damage in the same frame. The host resolves damage to other players with the same instant ray. The only real projectile a player fires is the SMAW rocket: 200 m/s, no gravity.
- **Tracers match hitscan.** The local tracer is a full muzzle-to-impact line that shows for 45 ms, so it never trails behind a hit. Two tracer defects and two host/local range mismatches are listed below. Remote players' shots draw no tracer at all.
- **The range lane measures the real simulation.** It timestamps `AcceptedBallisticVolley` (shot) and `SurfaceImpact` (impact), and `AcceptedRocketLaunch` / `SmawDetonated` for rockets. Today it will read **0.000 s, INSTANT (HITSCAN)** for every gun and about **200 m/s** for the SMAW. If COMBAT later adds bullet travel, the same readout shows the real flight time with no change to the range code.
- **Recommended game speeds** are in the table. They only matter if bullet travel is adopted. That is a COMBAT/LEAD design decision with protocol and host-authority cost; see "If travel time is adopted". I changed no weapon values: no speed value exists to fix, and every wrong value is in a file I don't own.

## How shots work now (evidence)

| Path | Code | Model |
|---|---|---|
| Rifles, SMGs, pistols, magnum (shared path) | `HandsWeapons/R23HandsWeapons.cs:611-629` | `PublishBallistic`, then the head-to-muzzle block check, then `Nearest(origin,direction,ActiveRange)` raycast. Spark, `SurfaceImpact`, `WorldHit` and practice-target damage all happen in that same call. |
| M60, AWP | `R23HandsWeapons.M60.cs:11`, `R23HandsWeapons.Awp.cs:10` | Same instant raycast, range 180. |
| M1014 / DT11 | `R23HandsWeapons.cs:631-654`, `R23HandsWeapons.Dt11.cs` | 8 instant pellet rays, `volley.Range` = 40 (`R23M1014Owner.cs:11`, `R24Dt11Owner.cs:6`). Damage falls from 100% at 8 m to 25% at 25 m. |
| Dual Uzis / Matadors | `R24DualFeedback.cs:22`, `R23HandsWeapons.Dual.cs` | Instant rays, `port.Trace(...,180,...)`. Matadors: 8 pellets with **no** local falloff. |
| SMAW | `R24SmawRuntime.cs:15`, `R24VRSmaw.cs:51` | Simulated rocket. `RocketSpeed=200`, constant velocity (the `9.8f` in `SourceLaunchGeometry` is not applied to rockets), `ProjectileSeconds=8` (1,600 m), sphere sweep r=0.035 m on layers 0\|19. |
| Host, other players (multiplayer) | `Networking/R24LanLobby.Combat.cs:47-88` | Instant ray per direction to 180 m (`ballisticTrace`) against sphere hitboxes. The victim pose is sampled at shot time, rewound at most 200 ms. `R24LanBallisticProfile` has rate, pellets and damage only, no speed. |
| Host, rockets | `Networking/R24LanLobby.Explosives.cs:46` | SMAW keeps `p.Velocity` with no gravity, stepped at 0.02 s, the same as local. Grenades and shells get gravity. |
| Local tracer | `R23HandsWeapons.TracerFollow.cs`, `R23WeaponFeedbackState.cs:9` | One LineRenderer from muzzle to impact (6 mm to 2 mm, yellow unlit), shown for `TracerSeconds=0.045`. Its near end follows the muzzle. |

## Per-weapon table

"Now" is the current game value. Bullets have no gravity or drop: all firearm rays are straight lines. Ranges are in metres.

| Weapon | Class | Now: speed | Now: max range (local / host) | Real-world reference | Ref m/s | Recommended game m/s |
|---|---|---|---|---|---|---|
| mk18 (MK18 / M4A1) | assault rifle | hitscan (instant) | 180 / 180 | 5.56x45 M855, 14.5 in (Mk18 10.3 in about 790) | 880 | 660 |
| akm | assault rifle | hitscan | 180 / 180 | 7.62x39, 16.3 in | 715 | 540 |
| m60 | machine gun | hitscan | 180 / 180 | 7.62x51, 22 in | 850 | 640 |
| rfb | battle rifle | hitscan | 180 / 180 | 7.62x51, 18 in | 810 | 610 |
| fal | battle rifle | hitscan | 180 / 180 | 7.62x51, 21 in | 840 | 630 |
| spr | marksman | hitscan | 180 / 180 | 5.56 Mk262, 18 in | 840 | 630 |
| awp | sniper | hitscan | 180 / 180 | .308 Win, 26 in | 850 | 640 |
| s85 | sniper (M1 Carbine geometry) | hitscan | 180 / 180 | .30 Carbine | 600 | 450 |
| cx4 | pistol-calibre carbine | hitscan | 180 / 180 | 9x19, 16.6 in | 420 | 320 |
| p90 | PDW | hitscan | 180 / 180 | 5.7x28 SS190 | 715 | 540 |
| mp5 | SMG | hitscan | 180 / 180 | 9x19, 8.9 in | 400 | 300 |
| mp9 | SMG (silenced) | hitscan | 180 / 180 | 9x19, 5.1 in | 380 | 290 |
| ump | SMG | hitscan | 180 / 180 | .45 ACP, 8 in | 285 | 250 (floor) |
| uzis | dual SMG | hitscan | 180 / 180 | 9x19, 10.2 in | 400 | 300 |
| px4 | pistol | hitscan | 180 / 180 | 9x19, 4 in | 360 | 270 |
| tec9 | pistol | hitscan | 180 / 180 | 9x19, 5 in | 360 | 270 |
| 1911 | pistol | hitscan | 180 / 180 | .45 ACP, 5 in | 255 | 250 (floor) |
| magnum | revolver | hitscan | 180 / 180 | .357/.44 Magnum, 6 in | 440 | 330 |
| m1014 | shotgun, 8 pellets | hitscan | **40 / 180** (host: 25% damage past 25 m) | 12 ga 00 buck | 400 | 300 |
| dt11 | shotgun, 8 pellets | hitscan | **40 / 180** (same) | 12 ga target load | 400 | 300 |
| matadors | dual shotgun, 8 pellets | hitscan | 180 / 180 (**local no falloff, host 25% past 25 m**) | 12 ga, short barrel | 380 | 290 |
| smaw | rocket launcher | **200 m/s projectile, no gravity**, 8 s | 1,600 (flight time) | Mk 153 SMAW HEDP | 220 | 200 (keep) |
| grapple | tool | hook, 45 m range | 45 | n/a | n/a | n/a |

Rule for the recommended column: **0.75 x real muzzle velocity, rounded to 10 m/s, with a 250 m/s floor.** It keeps the real-world order (pistol < SMG < carbine < rifle; the same cartridge gives the same speed, so M60 = AWP = FAL class), slows things for feel, and keeps short-range weapons usable within their 0-50 m role. Leave the SMAW at 200 m/s: it is already about 0.9 x real, and players can see it fly.

What those speeds mean in play: an AKM at 540 m/s needs 0.093 s to reach 50 m and 0.28 s to reach 150 m. A 6 m/s sprinter at 60 m needs about 0.67 m of lead. A 1911 at 250 m/s needs 0.1 s at 25 m, so about 0.4 m of lead on a 4 m/s runner. With gravity at these speeds, a 150 m AKM shot would drop about 0.38 m. I recommend **no gravity** for bullets this release: sights and optics are not zeroed for drop, and VR players read straight lines best.

## Tracer and consistency findings

1. **No lagging tracer today.** The tracer is drawn in full on the shot frame, together with the spark and damage, and is gone after 45 ms. It is consistent with hitscan. Do **not** add a travelling tracer while damage stays instant, because that would create exactly the "hit lands before the tracer" problem.
2. **Misses beyond 100 m: the tracer stops in mid-air** on the shared rifle path. `R23HandsWeapons.cs:611` sets `end=origin+direction*100` for a miss, but the hit ray reaches `ActiveRange` (180 m). M60 and AWP already use 180. On the new range lane this is visible when you shoot over the 150 m plate. *Request filed to COMBAT (one token).*
3. **Shotgun range differs between host and local (M1014, DT11).** The shooter's client stops pellets at 40 m. The host traces them to 180 m and deals 25% damage, so remote players can take chip damage at 40-180 m that the shooter never saw hit. *Request filed to COMBAT.*
4. **Matadors falloff differs.** Local pellets do full damage out to 180 m (`R24DualFeedback.cs:22`). The host applies the shotgun falloff (`R24LanBallisticProfile.Damage`, `Pellets>1`). This is a design call (which one is intended); flagged in the same request.
5. **Remote players' shots are invisible to others.** No tracer, muzzle flash or impact is replicated (`R24LanSession.Combat.cs` only submits; there is no echo path). This is not a speed bug, but it is the biggest "tracers look wrong" issue in multiplayer. It needs a new message or an echo of accepted shots (protocol change). Flagged as a design item.
6. The tracer is 45 ms long and 2-6 mm wide, which is sub-pixel at 100 m or more. That is fine for hitscan; if travel is adopted, the streak needs to be thicker and longer-lived.

## If travel time is adopted (COMBAT/LEAD decision, not filed as a bug)

The local and host sides must change together:
- Local: on accept, spawn a simulated bullet (speed from the table) in a preallocated pool. Sweep `Nearest` along each frame's segment, and apply the spark, `SurfaceImpact`, `WorldHit` and practice damage on contact. Draw the tracer as a short streak riding the bullet.
- Host: resolve at `shot.Time + distance/speed`, sampling the victim pose at that time (the rewind window grows from 200 ms to roughly 200 ms plus flight time). Add speed to `R24LanBallisticProfile`.
- Range: no change. The lane already measures shot-to-impact time from the live events and attributes each impact to the shot whose ray it lies on, so it will show the new times directly.

## Range lane validation (in-headset checks for the lead)

The SANDBOX RANGE lane is behind the spawn (turn around; the board says "SANDBOX RANGE LANE"). Expected readouts:
- Any gun on any plate: `NN.N m · 0.000 s · INSTANT (HITSCAN, SAME FRAME)`. The third line shows the real-world reference, e.g. `MK18 / M4A1 · 22 DMG · REAL ≈880 m/s = 0.057 s` at 50 m.
- SMAW into the 50 m plate: about `50 m · 0.25 s · 195-200 m/s`. The reading is slightly under 200 because the last frame step is cut short at contact.
- Shoot just over the 150 m plate with the MK18: the tracer stops at 100 m (finding 2) until COMBAT applies the fix.

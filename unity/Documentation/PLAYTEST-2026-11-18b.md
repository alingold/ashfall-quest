# Playtest 2026-11-18 late (r18 on Quest 3, host plus 5 PC bots): r19 issue list

- Build: r18 (c7742d71, 1.3.17-playtest-r18, lobby-v27). The author plays SEATED.
- This list grows during the session. Items are the author's dictated words, quoted where wording matters.

## Confirmed working in r18 (do not regress)
- SPR scope: "no more issues during strafing."
- Wrists ("nice job with the wrists"), paddle revive, bots dropping their inventory when killed, Banana Land, Thor City.

## BULLETTRAVEL
- **BULLET-TRAVEL:** "currently bullet travel is instantaneous. The farthest fast-moving target, I can only hit it when I'm aiming directly at it. I do not have to lead the target at all."
  - Firearms must have real travel time at Population: One speeds, with no drop. The r17 research and the implementation plan are in `~/ashfall/campaign/reports/r17/BULLETSPEED.md`. Implement that plan: host-simulated swept projectiles, a launch event for remote presentation, and the same flight model offline and on the range.
  - Weapon speeds (m/s): Tec9 450, PX4 500, .357 Magnum 550, 1911 650, MP9 680, UMP 700, Uzis 720, MP5 750, P90 780, CX4 820, MK18 850, RFB 880, AKM 900, FAL 920, M60 950, SPR 800, Sako 85 550, AWP 620, shotgun pellets 400.
  - Range: r18 RANGE raised firearm reach to 3,700 m. Keep that reach.

## SANDBOX19
- **MIRROR-RES:** "It would be nice if the mirror in the sandbox is higher resolution so I can get a better idea of how the skins move." Raise the sandbox mirror's resolution (only the mirror, no global render settings).
- **VEHICLE-GUNS-BUILDS:** "the guns on the tank, helicopter and hover scooter do not break builds." Vehicle guns must damage and destroy build pieces like hand-held guns do, offline and in rooms.
- **AWP-BOLT-SOUND:** "the sound for reloading the AWP [should be] louder. It's really hard to hear the bolt over the sound of the actual shot." Make the AWP bolt-cycle and reload sounds clearly audible after the shot, locally and for nearby players.

## HUD19
- **MENU-RENAME:** rename "PLAY WITH FRIENDS" to "MULTIPLAYER" everywhere it appears (buttons, titles, help copy).
- **PADDLE-TEXT:** remove the giant "touch with charged paddles to stow and stay within ..." text (the r18 PADDLES charge HUD). "That's ridiculous, unnecessary text." The lightning on the paddles is the cue.
- **DOWNED-TAG:** don't add "DOWNED" to a downed player's or bot's name tag. "It's pretty obvious they're downed. They're a ghost."
- **WARMUP-HUD:** remove the "WARM-UP" and "DAMAGE OFF" text. Keep the "host starts match" line (whatever its exact wording), moved down to sit right above the health bar.
- **TACMAP-ORBIT:** the left-trigger map "should stay the same distance from you if you are spinning with the right thumbstick. Currently it wobbles in and out in an oval path." The r18 MOVE follow logic turns the map around the wrong pivot. It must keep a constant distance and bearing relative to the player while snap or smooth turning.

## RULES19
- **RULES-UNLOCK:** "Why can't I change headshot and build damage? Those are useful settings." Make HEADSHOTS and BUILD DAMAGE editable in Match Rules and wire them for real: the host applies them in rooms, single-player applies them locally. BULLETTRAVEL is reworking the damage path at the same time, so coordinate through requests.
- **REVIVE-NO-TIMECAP:** "In Team Revive mode, is there an actual time cap? Does the game end at 10 minutes if people keep reviving each other and staying alive through the zone? That should not be the case. The game should only end when one team is eliminated." Team Revive has no time cap. The match ends only when one team remains. Hide or disable TIME CAP for Team Revive.

## VEHICLES19
- **EJECT-HANDLE:** "the sign to eject follows your head. Can we get rid of that and instead have a handle above you that you pull to eject." Remove the head-following eject prompt. Add a grabbable handle above the seat: grab it and pull down to eject. Applies to every vehicle with an eject prompt, offline and in rooms.
- **HELI-COLLECTIVE:** "make the helicopter go downwards faster ... what if the helicopter had a collective, so the tilt of your left hand makes you go up and down, but only while you're pulling the left grip button."
  - The helicopter descends faster.
  - While the left grip is held, the left hand's tilt (pitch) sets climb or descent rate, like a collective. Release means hold altitude, or the current behaviour.
  - SANDBOX19 owns vehicle guns versus builds; coordinate.

## MAPKIT
- **NEW-MAPS-TREATMENT:** "make sure all of the new maps get their proper treatments."
  - Red hexagons are weapon spawns, and signs that say "weapons" are also weapon spawns. Also look for jump pads and revive stations.
  - "My list probably is not exhaustive. Check what the standard procedure is and make sure it's implemented."
  - Apply every per-map treatment the existing maps received to the 21 maps MAPSITE imported or restored in r18. Find the treatments in git history and in the r12-r18 reports: weapon spawns from hexagons and signs, jump pads, revive/health stations, pod policy and boxed/open, hidden authoring markers, scoreboard frames, solid billboards, spawn audit, loot density, creator metadata.
- **ADDING-MAPS doc:** write `Documentation/ADDING-MAPS.md`, the standard procedure for future agents: download, convert, import, every treatment, verification and catalog steps.

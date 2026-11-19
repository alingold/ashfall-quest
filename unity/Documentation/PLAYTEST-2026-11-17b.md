# Playtest 2026-11-17 (second session): r16 headset (host Quest 3, seated, plus 6-7 Linux NetBots over Relay)

Build: r16 (1.3.15-playtest-r16, code 26, lobby-v25, HEAD d5ebe5ef).

Evidence:
- Voice notes: `~/ashfall/playtest-20261117b/20261117_ashfall_2.mp3`, transcript `~/ashfall/playtest-20261117b/transcript.txt` (timestamps below).
- Bot logs: `~/ashfall/mp-rig-runs/20261117-223912-fill-QJMGL9/`, `...-224141-fill-QJMGL9/` and `...-fill-QJMGL9/bot7` (bot.jsonl, player.log).

Working now (don't regress):
- bots revive each other;
- the victory screen ("first successful match");
- flying ("seems to be solved");
- grapple onto other players;
- grapple ceiling fix;
- sword hits;
- the new grass.

Godot reference (read-only):
- Full game source: `/media/al/Windows/Users/aling/Documents/ashfall/campaign-staging/20261104-three-chats/unity/Inventory/r3/inputs/quest-r22/source/scripts/` (243 scripts: player.gd, paddle_visual_089.gd, climbing, and so on).
- Scopes: `/media/al/Windows/Users/aling/Documents/ashfall/campaign-staging/20261104-three-chats/world/review-13-ui-maps-r1/full-candidate/scripts/` (awp_scope.gd, longwatch_scope.gd, the reflex sights).

## Issues

### REVIVE3 (repeat failure: REVIVE, REVIVE2)
- REVIVE-PADDLES (0:10:40-0:12:15): the author still could not revive a teammate. He died in the zone trying. Touching a downed teammate with the paddles must revive them INSTANTLY, like the Godot "rescue downed" (see paddle_visual_089.gd and player.gd). Keep the r16 hold-to-revive only if it doesn't conflict.
- REVIVE-SHOW (0:00:02-0:00:14, 0:12:11): when bots (or remote players) revive someone, others should SEE them take out the paddles (an animation, for example spinning the paddles) and HEAR the revive sound.
- RULE-SETTINGS (0:07:08-0:08:40): add match rules for friendly fire (on/off) and enemy revive (on/off). Today he cannot revive the other team, which is the right default.
- ENEMY-GHOSTS (0:10:27): you must not see the other team's ghosts (dead or spectating players).
- REVIVE-CLOCK (0:00:34-0:01:22): in Team Revive mode the match clock should count UP, not down.

### BOTAI2 (repeat area: BOTAI)
- BOT-STOP-SHOOTING (0:03:40, 0:07:19-0:08:19, 0:20:55, 0:30:45): the bots barely shoot at the other team, and after a while they stop shooting entirely. They started again after a new match ("they just get tired of shooting after a while"). He should be watching a battle between the two teams. Find why firing stops.
- BOT-ZONE (0:00:34): bots should avoid the zone.
- BOT-GUNSHOTS (0:02:09): bots should move toward the sound of gunshots.
- BOT-PINGS (0:05:53): teammate bots should go toward the player's pings.
- Optional extra ("a bit extra"): bots that grab, fling and fly. Not required.

### MATCHRESET (repeat area: rounds and spawns)
- MATCH-INVENTORY-RESET (0:04:21-0:04:47, 0:24:05-0:24:32): when a new match starts, he kept the gun from the previous match, including an unloaded one, which caused a glitch. Reset every player's inventory to the match start loadout at the beginning of each match.
- FFA-RESPAWN-STOPS (0:27:21-0:27:47): in free-for-all, after he had killed the bots many times (21-0), nobody respawned any more. The spawns stopped working.

### SCOPES (repeat failure: HANDS r15 SPR zoom)
- REDDOT-CROSSHAIR (0:13:38): the crosshair does not hide reliably while looking through the red dot sight.
- SCOPE-MAGNIFICATION (0:15:37-0:20:50): the bigger AWP and SPR lenses are good, but magnification did not increase.
  - The AWP looks about 2x. It should be 4x, and pushing the right thumbstick forward zooms in further (8x; the author also said 16x). This was ported from Godot's awp_scope.gd and lost.
  - The SPR looks 2x and should be 3x or 4x.
  - "We've got to replicate what it was in Godot because I had it perfect."
  - Verify by counting the pixels an object covers with and without the scope.

### CLIMB2
- CLIMB-CROUCH-PULL (0:03:17-0:03:29, 0:12:45): Godot had a mechanic where you grab (a wall or ledge) and pull UP to crouch, lowering the body. It was never ported to Unity. Port it.
- CLIMB-TURN (0:29:44-0:30:45): while holding a wall with the left hand, the right thumbstick must still turn (snap or smooth) all the way around without falling off. Today turning is locked until you let go.

### FLY6
- FLY-TURN-LAG (0:27:47-0:28:42): while flying, turning with the right thumbstick has a little lag before the direction changes. It must be as instant as on the ground.

### GRAPPLE2
- GRAPPLE-SPEED (0:21:11-0:22:16): the grapple reel is really slow. Make it 3x its current speed.
- GRAPPLE-PLAYER-RANGE (0:22:20-0:22:51): grappling another player works, but it should bring them closer to you, within sword range.

### FEEL (sword feedback and map placement)
- SWORD-DAMAGE-NUMBER (0:25:09-0:25:40): sword damage indicators are too small. They should appear right where the sword hits the target, and be bigger. They are a good size for guns.
- SWORD-SOUND (0:25:41-0:26:13): the sword sound is really annoying. Make it quieter, more of a swish and less white noise.
- MAP-PLACEMENT (0:26:41-0:27:13): the tactical map opens at an angle fixed relative to the body or world, so after turning 180 degrees mid-game it opens behind him. It should open in front of his face (current head yaw) but not follow the head afterwards.

### VEHICLEAIM
- VEHICLE-AIM (0:30:54-0:33:08): aiming vehicle weapons with the right thumbstick is not ideal, and a gun that only shoots straight ahead is not great. The author asked for a recommendation. Lead decision: vehicle guns aim where the controller points, with a visible reticle where shots will land. The gun follows the hand within its mount limits; the right stick keeps turning or steering.

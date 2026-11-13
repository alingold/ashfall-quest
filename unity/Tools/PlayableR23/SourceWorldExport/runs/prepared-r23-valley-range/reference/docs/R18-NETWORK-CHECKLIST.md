# R18 multiplayer validation — what each tier proves

Run the tiers in order. A tier only proves what its column says; a pass in a lower tier is
not evidence for a higher one.

| Tier | Proves | Does NOT prove |
| --- | --- | --- |
| A. Scripted LAN protocol (`tools/r18net/run-all.ps1`) | Real Main processes, real ENet, real session/bridge/voice code: authority, ordering, epochs, ownership, finite ammo, visibility rules, kick/lock | Real XR input, rendering, audio quality, microphones, Quest frame timing, WAN latency/NAT |
| B. One headset + desktop client on LAN | Hand/trigger/grip interaction against a remote authority; what a teammate/enemy *looks* like from the headset | Two-headset voice, headset-to-headset visibility, WAN |
| C. Two headsets on one LAN | Real ghost/tag/tracer visibility in-headset, real microphone capture → playback, mute/listen | NAT traversal, Internet latency/jitter, distant friends |
| D. True WAN (friend elsewhere, forwarded UDP) | Reachability through router/NAT, jitter tolerance, voice over Internet | Anything not exercised by the people playing |

## A. Scripted LAN protocol (no friends needed)

```powershell
# PowerShell 7. Optional: $env:ASHFALL_GODOT = '...\Godot_v4.7.2-stable_win64.exe'
pwsh tools/r18net/run-all.ps1 -BasePort 24900 -Tag mine
# one suite: pwsh tools/r18net/run-all.ps1 -Only combat
# exported build: add -Pack <candidate .exe/.pck>
```
Each suite writes `<Tag>-<suite>-<role>.json/.txt/.log` beside the source folder in `evidence/`.
A suite passes only if every peer's receipt passes and no `SCRIPT ERROR` or engine `ERROR` is printed.
Suites: solo (solo START/rematch/cancel), flow (3 peers: warmup, map switch, stale loot/pose,
death accounting, feed, team totals, ghost, rematch, kick/lock), combat (3 peers: pickup race,
rarity/ammo, drop/repickup, mounted gun pose, paired-Uzi jitter, moving tracer, voice
route/mute/listen/range/flood, live-rule privilege), uzi10,
ghost13, fx14 (retained tests).

`tests/r18net/map_abort_r18.gd` is an R17 synchronous-loading baseline reproducer, not part of `run-all` on R18. R18 cancellation/retirement acceptance uses `tests/map_lifecycle_r18.gd`.

## B. One headset + this PC as second player (LAN)
1. Same build + same map packs on both. Headset: HOST. PC: `Launch-Desktop.cmd`, JOIN headset IP, UDP 24760.
2. Both in warmup on the same map; headset host switches map → PC follows; old loot gone.
3. START solo works before PC joins; then START with PC present → both in match.
4. Headset: fire WHITE (1-star) dual Uzis together until empty → right and left stop at 20 each (higher rarities have larger magazines; check against that tier's capacity); reload works; PC sees tracers from the headset's muzzle while the headset player walks/flings.
5. Pick up a gold gun on headset, drop it, PC picks it up → same stars/ammo; PC sees the gun in the headset player's hand at the right angle.
6. PC kills headset player (or vice versa): one death, one feed line naming the weapon, team score +1.
7. On the same team: headset player as ghost is visible to PC through walls with name + empty bars; on opposite teams it is invisible to PC.
8. Host kicks PC: PC sees "Removed by the host."; with lobby locked, PC rejoin is refused.

## C. Two headsets, same LAN
Repeat B.6–B.7 headset-to-headset; then voice: both enable mic, talk → speaking indicator +
audio; each mutes the other → silence but indicator; listening off → silence; walk >42 m → no audio.

## D. True WAN
Host forwards UDP 24760 (or chosen port) to the hosting device; friend joins public IP. Repeat
B.3–B.6 and C voice. Record ping shown in roster. Only this tier says anything about NAT/Internet.

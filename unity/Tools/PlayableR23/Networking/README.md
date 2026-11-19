# Unity LAN multiplayer

Native hosting/joining is composed by `R24LanSession` and `R24LanPeer`; the protocol
owner is `R24LanLobby` and the membership owner is `SessionNetworkCoordinator`.
The current compatibility baseline is **lobby-v17** (2026-11-11 playtest round 3: host vehicle-mount admission accepts the new exterior tank/helicopter boarding handles; datagram layout unchanged; notes in `maintenance/20261110-playtest/features/vehicles4.md`). It builds on lobby-v16 (2026-11-10 playtest round 2: the
grapple gun item and its rules, an optional firing-hand byte on `playable.web.state/1`,
`playable.grapple.pull/1`, vehicle kinds tank and helicopter with turret/fire fields on
`vehicle.state/1`, `vehicle.fire/1`, the shell projectile kind, and the appearance style
bound raised to 139; feature notes in `maintenance/20261110-playtest/features/`). It
builds on lobby-v15 (2026-11-10 expansion: flash/smoke grenade kinds, web-line state, melee
hit events, build tier/variant/dismantle frames, hover-bike features, new match rules, pose
flag bits 32 "jetting" and 64 "riding"; notes in `maintenance/20261109-expansion/features/`). Host and client sources must
match; Godot ENet clients do not interoperate with this protocol.

The current source connects host/join and loading barriers, shared round clocks,
validated spawning and pod transitions, player presence/raw tracking, host-resolved
firearms/melee/explosives, weapon/ammo/item custody, shared loot/construction,
consumable gestures, revives and kills/scoring. Native weapon/HUD reconciliation
and round inventory retention use acknowledged host state. Render avatars remain
collider-free; combat authorization uses accepted raw history and host world queries.
Tracking gaps retain movement/world validation.

## Tests

```powershell
python unity/Tools/check.py lan --output-root maintenance/lan-check-20261109
```

`Current/check_lan_current.py` compiles live production sources from
`Current/sources.txt`, current case files and the retained Map/Presence/Round/Spawn
fixtures. All C# warnings are errors. Tests use simulated transport/ports and cover
malformed/stale/duplicate messages, authority, custody, lifecycle and crash recovery.
`--define BEFORE_FIX` on the underlying harness is reserved for historical
reproductions; normal verification runs use the default full suite.

## Rooms (campaign r12, 2026-11-15)

A hosted room is one durable Session. Shared maps, rounds and HOME are epochs of
it; the room ends only when the host ends it (`R24LanSession.Leave`) or the
transport is lost. A member's HOME leaves that member alone.

- The host's HOME calls `R24LanLobby.BeginRoomLobby()`: one new epoch that retires
  every world owner while all members stay admitted. `SelectedMap.Home` is set and
  is always `Cancelled` too, so every existing "no shared world" check applies.
- On the wire this is the map-v3 frame with selection kind 2 (0 warmup, 1 round,
  2 room lobby) in the byte that used to be a 0/1 "has round" flag. No new feature
  id; the schema byte stays 3.
- A host-side map failure returns the room to its lobby instead of closing it.
- `R26RoomStatus` is the single room picture the menus read (stage, ready count,
  countdown). CPU cases: `~/ashfall/campaign/rooms/R26RoomCases.cs`.
- Public room list: a code room is listed on Unity Lobby unless its host makes it
  private. The Lobby entry is a directory row only (host name, room code, map,
  player count, match running, build); guests read the code from it and join over
  Relay as with a typed code. `R26RoomDirectoryFlow` sends one request at a time:
  heartbeat every 15 s, updates at most every 3 s, queries at most every 2 s. The
  host deletes the entry when the room ends or turns private; the service drops
  it 30 s after heartbeats stop.

## Durable Health storage

**Native game, since campaign r12:** on-disk Health images are switched off at
player start (`R26LanHealthPolicy`, `R24LanLobby.DurableHealthImages=false`), so
the host creates no epoch directory and the quotas below cannot close a room.
The CPU/LAN suites keep the durable default. Retention when storage is bound:
`R24LanSession` keeps a live room's current and previous epoch directory and, on
`StartHost`, removes directories left by rooms that are no longer running. A
second host process sharing the folder is detected through a byte-range lock on
`LanHealthReceipts-v1.hosts` (`owner.lock`'s `FileShare.None` is process-local on
Mono/IL2CPP); while one is alive nothing is pruned.

When bound, a host uses storage at
`Application.persistentDataPath/LanHealthReceipts-v1`. Exact receipts, Health and
pending producer transitions commit together through two complete image slots
and a checksummed atomic generation manifest. No receipt eviction or probabilistic
replay filter is used. Existing partial files/corruption cause explicit failure.

Default bounds are 4,096 damage/gain/command receipt keys, a 64 MiB conservative
resident-state allowance, 2 MiB per image, 64 MiB managed-file payload quota and
128 retained epoch directories. The MonoBehaviour exposes each limit. The image
limit can be lowered but cannot exceed the typed codec's 2 MiB bound. Serialized
state can fill before the key quota. Finite storage cannot provide unlimited
arbitrary-ID history with unlimited availability: new mutations stop and the room
closes with a visible error. Ended epochs are pruned as described above; they have
no recovery value because a native restart always creates a fresh room and identity.

Trusted-host recovery/reopen requires the exact current manifest. Inspect and
explicitly preserve/quarantine partial files before retrying. Never replace the
current head with an older image to regain availability. Archive only complete
ended-room directories, outside the managed root, after their owner is closed.
Health recovery alone cannot restore a live Session, inventory or transport
acknowledgment history; native restart creates a fresh room/identity. Checksums
assume trusted local storage and cannot detect wholesale malicious cold rollback
without an external monotonic anchor.

The durable implementation and CPU crash evidence are recorded in
`campaign-staging/20261104-three-chats/unity/mp-receipts-r4/REPORT.txt` and
`STORAGE-POLICY-AND-AUDIT.txt` from the repository root. Full-image writes are
synchronous: native performance, Android replacement semantics and power-loss
behavior still require validation after the engine/build/device hold is lifted.

## Supported scope

LAN keeps explicit unicast IPv4 entry on UDP 24760. Online rooms use Unity Relay
(DTLS) with a short room code, anonymous Authentication and at most eight players;
a linked Unity cloud project with Relay enabled is required. Both paths use the
same lobby-v17 datagrams. Host migration, identity-preserving reconnect and
whole-room restart recovery remain separate contracts. CPU/source/API checks establish the behavior of their tested source;
actual native peer, Physics/UI and headset checks require separate evidence.

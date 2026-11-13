# Standalone validation candidate 1001

This candidate starts at public commit `5d206e6b5f20618e291b6148a5c311c175cf15bf`, not at an older private checkout. The pinned public asset pack was downloaded and all 925 asset files matched `asset-manifest.json`. Original public procedural audio, licenses, models and maps are retained. The public checkout, private 1000 checkout and Skyrim are not modified.

## Reproduced failures and changes

An authentic match projectile packet delivered after a lobby snapshot could recreate a projectile in the client scene. Returning home also left authoritative projectile/build snapshots populated. This was reproduced by two real ENet clients, then by two full game processes. The recording's diagnostic count reads live game projectile nodes; the old object remains at the previous map's coordinates and is not necessarily visible from the pedestal menu.

The session now discards match events outside an active match, queues valid loading events until the matching match snapshot, and rejects projectile poses after leaving the match or disconnecting. The bridge also checks match state before creating projectile visuals. Returning home clears transient build/projectile snapshots. A second observed error came from sending poses before the ENet connection/authentication was ready; poses and requests now wait for a usable connection and local identity.

The network revision derives a distinct fingerprint from the preserved public asset fingerprint, so this patched candidate cannot accidentally join unpatched public clients. It uses preference folder `AshfallVR-Validation1001` and version `1.0.0.1`.

Two further lifecycle failures were reproduced over real local connections. Duplicate loading acknowledgments could respawn/heal a living player; loading is now idempotent. A reserved identity reconnecting after the host advanced the round could retain weapon IDs already cleared by the new round. Reservations now record their epoch: same-round reconnects preserve inventory/state, while cross-round reconnects preserve identity but receive the current loadout and a fresh current-map spawn. Both scenarios have regression coverage.

## Reproduction

The owner workspace includes `run1001.ps1`, `legacy_wire1001.ps1`, immutable `public-baseline/`, this `candidate/` and timestamped `evidence/`. `run1001.ps1` launches only its own hidden, bounded child processes, supplies `--xr-mode off --audio-driver Dummy`, isolates profiles and records exact arguments and process IDs. Ports are test-specific; no firewall or network setting is changed.

```powershell
# Unit regression; absolute output path under the owner workspace.
.\run1001.ps1 -Label focused -Script res://tests/validation1001/focused.gd -UserArgs "$PWD\evidence\focused.json"
# Dedicated server + two clients, then four after this passes.
.\run1001.ps1 -Label matrix2 -Clients 2 -Port 24930
.\run1001.ps1 -Label matrix4 -Clients 4 -Port 24931
# Actual game reconnect, empty gun/reserves/calibration, four match epochs.
.\run1001.ps1 -Label reconnect -Clients 1 -Game -UserArgs reconnect -Port 24932
# Actual game delayed packet replay; one offscreen render client + headless host.
.\run1001.ps1 -Label visual -Clients 1 -Game -Video -UserArgs after -Port 24933
```

Add `-Package <absolute-path-to-Ashfall.exe>` to test the exact exported pack with the same external fixtures under the matching Godot editor runtime. The Windows release template ignores external `--script` fixtures and boots its normal game, so native-executable startup is checked separately with `-NativeSmoke` and `--smoke`. This distinction is recorded in process arguments; pack tests are not described as native-executable fixture runs. Test fixtures and build tools are excluded from the game pack. `-PeerScript handshake.gd -Clients 1` verifies incorrect passwords, the old public fingerprint and the matching candidate. The baseline focused regression can load the immutable public session script via its second user argument; no original file is overwritten to reproduce it.

The protocol matrix covers simultaneous pickup/ownership, unauthorized mutations, empty staged reload transfer, uncapped ammo/build supplies, build placement/destruction state, friendly fire, downing/revive/respawn, duplicate loading acknowledgments, transport reconnect and fresh late join, expiry of reserved ownership, five match epochs including reconnect across a round change, lobby return, deliberately delayed packets and host loss. It seeds a charged revive state and uses a permissive geometry validator; it does not establish paddle-motion timing or collision correctness. Separate full-game fixtures exercise actual world loading, instances, remote avatars, tactical map, a shared practice target, active zone late joining, reconnect recovery and repeated matches. The actual-game revive case scripts nearby positions and the charged timer, then uses the real reach/obstruction validator and checks client health/input restoration. Its first placement attempt was rejected correctly because teammates were 30 metres apart; this fixture error is not reported as a game bug.

## Video interpretation and limits

Before and after are separate native client captures with scripted inputs and a deliberately replayed/delayed authenticated packet. They are not simultaneous perspectives or measured Internet packet loss. The diagnostic overlay is test-only; no replacement game imagery or fabricated remote view is composited into the scene. Silent MP4s are compressed from Godot's fixed-30-FPS MJPEG captures; capture duration is not a performance measurement. A malformed first launcher attempt was stopped by its watchdog and is explicitly excluded from proof.

No VR session was launched or stopped. All networking runs are on one Windows computer over loopback. Real Internet latency/loss, multiple headsets, physical reload/revive feel, headset fit and sustained real-player performance remain unverified. Public hosting, publishing and pushing are outside this validation task.

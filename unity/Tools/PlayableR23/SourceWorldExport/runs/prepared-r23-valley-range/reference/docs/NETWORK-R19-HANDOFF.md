# SOL NET R19 handoff

Provider codex, model gpt-6.1-sol; exclusive sol-net-r19 workspace, branch sol/r19-peer-retirement. Parent evidence/source read only. No build/export/APK/install/push. Offline headless ENet proof only.

Runtime checkpoint: 29a1fb1 (cherry-pick first). net_session1000.gd adds _broadcast_live and routes heartbeat, pose, event, state, delta broadcasts through connected ENetPacketPeer/channel checks. Retains periodic heartbeats and sends separately to each healthy recipient. No logging suppression or gameplay edits. Graceful disconnect can leave SceneMultiplayer IDs registered after ENet channels retire. Parent chronological .log and local baseline show three channel-2/max-channels-0 failures.

Receipts in ../evidence:
- sol-before: host 83 assertions pass, three engine errors; p1/p2 pass; launcher exits 1.
- sol-after: host 83, clients 63/61 checks, all pass, no engine errors; launcher exits 0. Includes kick target ignoring denial and locked rejoin denial.
- sol-delay-fixed: intermediate controlled-delay test revealed spawn revision/entity IDs were also captured after host restart; preserved assertions failed as intended. Corrected by capturing both before barrier11.
- sol-delay-final: host83 / p1 63 / p2 61, all pass, zero engine errors, two-second p2 delay after barrier11 and barrier12.

Fixture: report epoch in barriers11/12; read sync.fact(1,phase).epoch as canonical pre-transition epoch. Snapshot prior entity IDs and spawn revision before barrier11. --r19-delay injects scheduling delay; no timeout extension or removed assertions.

Limitations: no headset/WAN/native/XR proof. Full Main integration reproduced transport failure; dedicated transport state/channel timeline and isolated healthy-client delivery counters remain to be added. No claim that APK is fixed or installed. Parent must cherry-pick and create its own checkpoint/build.

Fixture commit: 793e9ea (cherry-pick after 29a1fb1). Controlled old-fixture sol-delay-old-p2.log records FAIL rematch reloads every peer into a fresh match. Control launcher remains bounded by its 300-second timeout. Exact old-control script preserved in evidence/r19_old_epoch_repro.gd.
Merge hooks: git cherry-pick 29a1fb1 793e9ea. Run tools/r18net/run-peers.ps1 with explicit R19 Engine, own Project/Evidence; optional ExtraUserArgs @('--r19-delay'). Existing launcher already fails engine ERROR as well as SCRIPT ERROR.

## Fixture-only follow-up (runtime frozen)
Root independently cherry-picked runtime29a1fb1 as72f1035. Verified local runtime diff against29a1fb1 is empty.
Old delayed fixture completed naturally: sol-delay-old all peers fail; p2 receipt records both rematch and shared-warmup transition failures. Fixed sol-delay-final passes all peers with the same two-second p2 delay. Both transition assertions retained. Exact old source is evidence/r19_old_epoch_repro.gd.
Additional delivery run sol-delivery-final passes host91/p1 65/p2 61 with zero engine errors. Host facts healthy_pong_ms=[257,255,256,254,256,253,262,261]: eight real guarded _ping RPCs and real client _pong responses using now()-0.25 stamps. p1 facts healthy_channel2_events=8, healthy_channel3_serial_before=103, after=300. These counters run after forced kick, alongside locked rejoin denial. No positive RTT assumption for ordinary zero-ms loopback ping.

Final fixture-only delivery checkpoint: sol-delivery-periodic all pass (host91 / p1 66 / p2 61), zero engine errors. p1 receives8 channel2events; channel3serial98->332 then ordinary periodic progression334. Eight positive real pong samples are in host JSON. No runtime changes. Cherry-pick fixture commit793e9ea then the delivery commit reported below, after runtime already integrated as72f1035. All test processes exited; source clean.
4e701d829a4eed5468b41f8a006c065ffd581dcc

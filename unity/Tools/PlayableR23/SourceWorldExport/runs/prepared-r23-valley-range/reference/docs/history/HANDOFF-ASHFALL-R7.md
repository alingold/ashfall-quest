# ASHFALL R7

Rarity glow is confined to world pickups. Both local and remote held weapons retain their normal materials. Existing weapon levels, stats and authored blade emission are preserved.

MAP SELECT now offers all 105 completed imported maps plus Relay Valley, Sandbox Range and Cyber Bird: **108 maps across 27 pages**. Use Previous/Next, select a card, then READY. The adjacent private map PCK mounts automatically.

Run [Launch-VR.cmd](./ASHFALL-R7/Launch-VR.cmd), or [Launch-Desktop.cmd](./ASHFALL-R7/Launch-Desktop.cmd). Keep the builds folder intact. Initialization remains manual; R7 has a separate userdata folder seeded with unchanged R6 settings.

The isolated source is [ASHFALL-R7-Source](./ASHFALL-R7-Source), branch `standalone/r7-library-ground-glow`, checkpoint `1d2e2d8b8acc42b30aed54128ce41f2eded367f2`. It starts from the newest completed 1004 source and deliberately ports R6 runtime features while retaining 1004 reload and network-fence fixes. Original workspaces and earlier deliveries remain untouched. [Porting manifest](./ASHFALL-R7/PORTING-MANIFEST-R7.json).

Offline checks passed: 768 selector/rarity assertions against the exported build; all 105 READY entries and map-node unloads; real two-process loopback gameplay with local/remote held-weapon checks; finite reload and replay-fence fixtures; 6,391 aim checks and 116 interaction checks. Native Vulkan screenshots were captured with XR off. [Validation receipt](./ASHFALL-R7/VALIDATION-R7.json).

Headset testing and real Internet connectivity remain untested in this turn. No VR session was launched or stopped. The existing map library remains private/local-use with creator metadata retained; no public map archive was created.

Screenshots: [selector](./ASHFALL-R7-Evidence/02-MAP-SELECT-LIBRARY.png), [READY](./ASHFALL-R7-Evidence/04-ARCHIVE-SELECTED-READY.png), [in game](./ASHFALL-R7-Evidence/05-ARCHIVE-IN-GAME.png), [held versus ground rarity](./ASHFALL-R7-Evidence/06-HELD-NORMAL-GROUND-RARITY.png).

Executable SHA-256: `b537718b6d075e8e5ecd87bac3494f934270c9c9c86171c77177be5b1e198cac`

Map PCK SHA-256: `6629ae3ee53b8ffa3a3a0d60fd0f0c6103924295c8bab1d2e1992e783a8ed2d6`

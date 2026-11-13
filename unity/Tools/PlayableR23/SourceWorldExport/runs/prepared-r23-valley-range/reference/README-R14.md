# ASHFALL R14 — Map systems and launch platforms

Isolated from the verified completed R13 source checkpoint `1122846d2b14d55736f4b972c95c84e56193c56a`, on branch `standalone/r14-map-systems`. Candidate: sibling `ASHFALL-R14-MapSystems`; final checkpoint, executable hashes and receipts are recorded in its `VALIDATION-R14.json` and `SOURCE-CHECKPOINT.txt`.

The deployment platform now has twelve reusable-geometry red pods, open side windows, a paneled 49 × 18 m deck, angled framework walls and segmented roof. Platforms sit at the arena perimeter and face along cardinal sides. Any unused own-team pod works. Departures clear the open front before crossing the map; walking/flying off remains available. Original procedural art, informed by the primary concept and film references retained in `REFERENCES-R12.md`; no reference artwork copied.

New hash-bound native assembly catalog covers all 105 archive maps: white-ring warmup sites, black-ring/person deathmatch sites, directional jump-pad frames, and green/cross health pads. Marker visuals disappear in hosted play and decorative marker/glyph collision is removed. Spawn height is recovered from a bounded floor search with headroom validation; unsupported/floating/obstructed locations use existing validated fallbacks. Authored off-arena warmup hubs receive a bounded warmup-only exemption. Team Revive retains sky decks; deathmatch uses arena sites.

Health pads provide 10 health per second up to 100, with clear contact and no through-floor healing or ghost revival. Tilted pads launch at the existing 45 m/s along the physical frame normal, with independent site cooldowns for chains. Dive activates after one second of descent while holding the left stick forward (desktop W); releasing forward permits glide. Existing normal locomotion/weapon numbers and calibration transforms are retained.

Source-map defaults are normalized for 79 maps; 26 lack source rule settings. Host overrides win and remain editable. READY applies the same supported movement/damage/supply defaults temporarily, without writing them to personal preferences. Source custom scripts are retained for audit but are not executed. Unsupported fields and clamps are explicit in `assets/data/map_rules_r14.json`.

Authoritative pose handling now separates invalid hands from valid body tracking, permits real fast falls/dives independently of horizontal tuning, and sends a bounded correction after persistent rejection. This avoids a permanently stale position driving zone damage, passage contact and build reach. Actual hosted-match tests cover Alpha doors, windows and construction.

UI cleanup removes tactical-map explanatory clutter and the release-controls toast, while keeping neutral-input safety. Menus, backdrops and both interactive lasers render ahead of world geometry. Selector/backpack icons, labels and stars use white, blue, purple, gold, red; held weapon meshes remain unchanged. Revive sounds wait for authoritative confirmation and charging cues do not overlap. All accepted WAVs are preserved byte-for-byte.

Elbow trails are 2.5 times thicker and remote avatars produce them during glide/dive, preserving team-only ghost visibility. Remote tracers retain the shooter's origin instead of following the viewer's nearby gun. Arbitrary ping-distance caps are removed.

The material audit covers every archive map and all 4,366 exported image bindings. None is missing or undecodable. Many props arrived as averaged vertex colors without their original UV/artwork. Guarded procedural detail restores 2,056 container ends and nine exact wood panels across thirteen maps, without changing collision. Signs, trucks, fences and other unavailable original artwork remain unresolved; intentional pink materials are not blindly recolored. See `assets/data/map_art_audit_r14.json`.

Every feedback item from this turn is tracked in `FEEDBACK-REGISTER-R14.md`; earlier registers remain retained. The final candidate includes an offline/headset validation distinction, porting manifest, credits, native screenshots and headset checklist. No VR runtime or another running game was launched/stopped. Initialize remains a manual menu action.

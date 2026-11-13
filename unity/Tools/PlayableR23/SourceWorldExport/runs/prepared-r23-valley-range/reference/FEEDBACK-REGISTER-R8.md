# ASHFALL R8 — feedback and verification register

Updated: 2026-10-31. Working source: `ASHFALL-R8-Source`, branch `standalone/r8-imported-map-gameplay`, based on delivered R7 checkpoint `1d2e2d8b8acc42b30aed54128ce41f2eded367f2`. Prior deliveries, the active game, original map caches, Skyrim and original modeling assets are read-only.

This is the working checklist. A reported issue stays open until the implementation and its relevant checks are recorded. Offline checks do not count as headset acceptance.

## Current playtest feedback — all comments retained

| ID | Report / requested behavior | Status and verification needed |
| --- | --- | --- |
| MAP-01 | BANANA LAND appears to contain two maps overlaid. Determine source geometry versus runtime duplication; fix the actual cause. | UPSTREAM SOURCE ISSUE, remains. Raw Delicious mesh diagnostic reproduces overlap with no Main, gameplay or previous map. Binary buffers/transforms unchanged. RAW-HEADER-DIAGNOSTIC.json and RAW-SOURCE-banana-land-gods-pitdevil.png. |
| MAP-02 | BAMBOO also appears to contain two overlaid maps. | UPSTREAM SOURCE ISSUE, remains. Same independent reproduction for Bamboo; RAW-SOURCE-bamboo-gods-pitdevil.png. All 105 actual READY/unload checks released old map weak references. |
| UI-01 | Right hand has two menu lasers: lower beam works, upper beam does nothing. Keep one accurate interactive beam. | Implemented shared grip ray and explicit menu-beam ownership; pointer shadows disabled. Exported feedback checks pass. Tracked right-hand headset acceptance pending. |
| LOOT-01 | Red hexagons/question marks are weapon spawn placeholders, expected to produce approximately six random weapons each. | Recognized baked ring/glyph triangles and matching collision removed in prepared adapters. Valid nearby sites activate six real varied weapons using existing authoritative inventory/rarity systems; bounded activation and art allocation. Exported five-map checks and two-peer pickup/equip/drop pass. |
| LOOT-02 | Atrocious geometry otherwise appeared correct, but weapons must spawn at the marked sites. | Atrocious: 378 cache sites recovered. Geometry preserved except recognized authoring symbols and breakable panes. Real exported cache items and two-peer match verified. |
| SPAWN-01 | Starting Atrocious spawns player below the map. | Fixed zone fallback that discarded valid sites and skipped grounding. Exported Atrocious warmup, zone match, death/respawn, rematch and return warmup pass with actual floor/headroom checks. |
| HUD-01 | HUD disappears into walls; HUD must render in front of geometry like the reticle. | HUD quads/labels/icons use no-depth rendering; world markers retain normal occlusion. Exported R8-09 screenshot and feedback checks pass. Headset edge cases pending. |
| VIEW-01 | Moving against walls can black out the screen. | Removed obstruction blackout; solid tracked head collision retained. Exported fade assertion passes. Physical headset wall approach remains pending. |
| TEX-01 | Paintball grass and gray wall textures shimmer/flicker beyond a few metres. | Mipmaps and anisotropic filtering apply to every imported map, including normal maps; roughness floor and foliage alpha scissor. Native Vulkan views recorded. Headset motion/long-distance comfort remains pending. |
| SPAWN-02 | Paintball lobby and match spawns appear swapped; match can start above the arena. | Separate supported warmup versus arena candidates; unsupported Y=100 and tallest fallback roofs no longer become lobby sites. Exported Paintball warmup and zone match floor/headroom checks pass. |
| SCORE-01 | Imported scoreboards missing, specifically beside “Welcome to Battlefield Paintball.” | Live ASHFALL world scoreboards added near warmup and distant arena spawn. Paintball native screenshot beside arena sign; session-derived score/team/player data. Original scoreboard metadata absent, so exact source placement is not reconstructed. |
| MARK-01 | Player spawn markers should not be visible in hosted warmup/matches. Review view may show them. | Functional player spawn sites are invisible. Recognized baked weapon symbols hidden/removed in play; raw archive remains preserved for review. No generic decoder for every possible bespoke authoring glyph or new review-mode toggle is claimed. |
| MARK-02 | During matches red weapon placeholders must be weapons, not hexagons. | Implemented with LOOT-01. Actual random cache weapons replace recognized red symbols and use normal grabbable item systems in hosted sessions. |
| TEX-02 | Classic rocks and trees shimmer; its grass seemed fine. | Same all-map mipmap/filter/foliage changes cover Classic rocks/trees. Native Classic views recorded; actual headset shimmer acceptance pending. |
| AUDIO-01 | Weapon sound effects sound reverted; ensure latest accepted weapon audio is shipped and selected. | Confirmed real PCM regression in R7/1004 under unchanged filenames. Restored accepted R6/0992/0993 audio and full manifests. Shipped executable: 604 decoded-sample/routing checks, zero failures. Headset mix remains separate. |
| MAP-03 | Classic offhand map is only a blank blue grid. | Actual geometry captured once into aligned north-up tactical texture; map/zone/player coordinates use catalog bounds. R8-08 native screenshot replaces blank grid. Held-trigger behavior retained. |
| PAD-01 | Classic jump pads do not work. | Classic: 12 new standable launch-pad sites with open vertical clearance, visible discs/arrows, original sound and cooldown. Real player velocity/cooldown tested in export. These are ASHFALL placements; original exact pad sites not recovered. |
| BREAK-01 | Doors/windows must break when walked through, allowing building entry, with a loud break sound. | Recognized glass plus Classic thin entry panels become collision-bearing breakables, shatter on approach/shots, sound, reset each round and replicate broken state. Classic 1070 panels (730 glass + 340 thin entry panels). Bespoke opaque doors need authored semantics. Shipped peer panel-state check passes. |
| LOOT-03 | Classic has missing weapon drops throughout. | Classic: 64 distributed supported cache sites, six real weapons per active site plus existing ammo/build/heal pickups. Nearby activation bounds runtime cost; shipped checks verify real instances and ground access. |
| SPAWN-03 | Starting matches can spawn player in midair generally. Lobby and match have different spawn types. | Common imported-map zone fallback grounds and validates candidates; retains valid recorded arena sites, separates warmup. Five reported maps checked in export; all 105 actual READY/load/unload tested in source with final prepared packs. |
| SKIN-01 | Add an obvious skin-selector button. | SKINS button beside LOADOUT plus field-menu selector. Seven total styles, preview/USE CHARACTER, saved selection and remote replication. Calibration unchanged; initialization remains manual. Native selector screenshots and exported interaction checks pass. |
| SKIN-02 | Incorporate new Witch Doctor skins and label each by the model that made it. | All FOUR new skins integrated: GPT-6 Astra, GPT-6.1 Sol, Gemini 4 Argon and completed Claude Opus. Donor copies fitted to existing avatar segmentation; Claude includes light variant/glove maps. Model provenance retained, source donors untouched. Exported four-model native view; two-peer Claude body/hands pass. |
| PROCESS-01 | Write down every comment and work systematically; do not lose earlier feedback. | Register created before implementation, all current reports retained, prior accepted features listed below. Candidate/source contain register, change/porting manifests and offline receipts with separate headset/Internet limits. |

## Previously accepted behavior to preserve

- Cyber Bird remains its own playable avian city with dense routes, giant bird landmark, street lights, useful stairs/windows, cars/NPCs and real distributed loot.
- Held weapons have no rarity glow; world pickups retain rarity glow/stars and all five weapon levels.
- All 105 added maps remain selectable beside the three built-ins; READY enters the selected map and returning home unloads it.
- Manual initialization, persistent calibration, player height, accepted weapon aim/damage/recoil/reloads/inventory and existing controls remain intact.
- Backpack grip equips and trigger drops; blade icons remain upright; CX4 sight remains aligned.
- Lobby is in-map warmup; host controls are accessible from the left menu; host can start solo, change mode/zone damage/speed/time/kill caps, and return everyone to warmup afterward.
- Shrinking square zone targets a contained random square; red current and white next boundaries appear on the held map and world surfaces, with zone warning visuals/audio and no “Danger Zone” text.
- Compact team kill bars/feed are centered above the HUD; sprint remains at the bottom; no obsolete team/opponent/mic/alive text returns.
- Left trigger map is held, not toggled. Ping type persists after selection. Voice preferences stay under General; player list shows speaking/mute controls and host list provides kick.
- Existing voice, Argon skins, collision/climbing/building/flying layers, six inventory slots, current loot quantities and network session/reload fences remain supported.
- PC VR performance: bounded chunks/material sharing, mipmaps, sensible visibility and lifecycle; no automatic VR launch/stop and no changes to another running game.

## Delivery and evidence

Delivered: isolated R8 source branch/checkpoint, standalone launchers, unchanged settings seed, change/porting manifests, native Vulkan screenshots and per-issue offline receipts. See VALIDATION-R8.json and SOURCE-CHECKPOINT.txt in the candidate. No public redistribution of the private imported map cache.

Offline validation: 105 READY entries/unloads (210 checks), shipped selector/rarity (768), shipped feature integration (534), shipped audio preservation (604), shipped CX4 sight/rarity (6391), replay/session fences, finite reload bridge, and real two-process Atrocious gameplay. These do not constitute headset/controller comfort, actual microphone testing or Internet matchmaking validation.

Native captures use the shipped executable with XR off and the Mobile Vulkan renderer. The raw-source diagnostic only removes unsupported required-extension declarations and rendering-material references in a temporary GLB copy; mesh binary, node transforms, normalized accessors and scene membership are preserved. Donor assets and original/private packs remain unchanged.

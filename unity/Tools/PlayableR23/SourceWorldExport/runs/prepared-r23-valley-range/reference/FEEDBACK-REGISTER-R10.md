# ASHFALL R10 feedback register

Base: completed R9 008b01f819dc2e9e4fbedd7c2e7466dbe6709c45, verified handoff and validation receipt. Isolated branch: standalone/r10-runtime-corrections. Prior candidates, active standalone work, original donor assets, Skyrim and VR sessions remain untouched. R8/R9 feedback registers remain alongside this register; their open source-data gaps are retained.

| ID | User comment / required result | Result and evidence |
| --- | --- | --- |
| RARITY-01 | White, blue, purple, gold, red, in that order. | Corrected shared palette and base metadata. Runtime asserts every color; selector checks preserve held/no-glow versus ground/glow behavior. |
| SELECTOR-01 | Gun type/star labels already exist; request retracted. | Existing labels preserved, without duplicate text. Blades and grenades remain fixed form. |
| SPAWN-01 | Hosted START returned to warmup position. | Initial match spawns prefer safe separated sites. Actual exported Main host transitions passed on Alpha, Afterlife, Poppy and Classic: approximately 147, 865, 186 and 213 m separation. All 105 maps pass supported-spawn checks. Missing authored spawn-type tags are not reconstructed. |
| SKINS-01 | Remove skins from field menu; forbid mid-match changes. | Field-menu entry and activation routes removed. Stale page returns to pause. Host rejects active-round style changes. Main-menu selector retained. Actual host and two-peer checks passed. |
| SOUND-01 | Wooden door break, distinct from glass. | New original 0.7 s wooden fracture WAV, routed by passage type. All 271 accepted WAVs byte-identical to R9; 604 exported audio-route checks passed. Listening/headset mix untested. |
| GLASS-01 | Opaque windows. | Custom maps using official-template s48 glass now recognized. Recognized panes use shared transparent material; native screenshot shows scenery through intact glass. |
| GLASS-02 | Sound plays but pane remains. | Pane owns duplicate layers, crosspieces and collision; aperture splitting removes welded leftovers. 558 exported checks include actual Alpha/Classic collision ray fans, assembly visibility and audio routing. Break state passes across real ENet peers. Afterlife decorative glass-named letters/posts excluded. Bespoke unrecognized geometry remains a coverage limit. |
| PAD-01 | Sound without launch or intermittent activation; launch high. | Floor support canceled the impulse on the next tick; glide intent could cancel ascent. New ascent state preserves 45 m/s launch until apex/collision/climb/reset. Only successful launch emits sound/cooldown. Source pad sites grounded and deduplicated. Actual Alpha source pad and exported Main peers sustain roughly 59–61 m rise during measured flight. Arms-out ascent intent checked separately. R9's 12 safe Classic/Phoenix replacements remain explicitly new; original sockets unrecovered. |
| MATERIAL-01 | Alpha vans/containers lack detail; pink/white/green surfaces. | Raw-source/native comparison confirms gray low-detail bodies and plain boxes already lack original UV/art. Original procedural replacement detail only on matched shapes: Alpha 54 van bodies, 52 containers and 52 wheel pieces. Collision-face bytes, triangle counts and original textured bindings preserved. Original missing art remains partly unresolved. |
| MATERIAL-02 | Afterlife missing textures. | Source provenance, native overview and original thumbnail inspected. Much of its flat bright palette exists in supplied source. All declared dependencies verify. Unknown original art remains open; no blanket recoloring. |
| TEAM-01 | Team Revive explanation cut off after “you actually have…”. | Open clarification: whether each team should use a fixed authored base. Supplied spawn triples lack team/lobby tags. Existing Team Revive rules retained; authored base reconstruction is not claimed. |
| UZI-01 | Both triggers can make right Uzi fire indefinitely. | Reproduced 87 emissions from a 40-round pair, one authority magazine still holding 20. Removed muzzle-proximity guessing; relay carries actual child-hand context. Exported simultaneous coincident-hand tests emit exactly 20 per hand; both authority/local magazines stay empty. Matador finite magazines and one-shot-per-press also pass. Stats/handling unchanged. |

## Offline coverage

- 105 actual Main READY/unload cycles, 4,591 full-library checks; 108 entries and 768 selector/rarity checks.
- 558 passage checks; 56 hosted transitions; six sustained movement/palette/menu checks; nine paired-trigger checks; 35 inherited menu/hand/audio regression checks.
- 604 accepted audio-route checks; 271 existing WAVs byte-identical. Nine rendering-only prop preservation checks.
- Real loopback peers exercise host START, replicated glass removal, skin lock, pad flight and finite dual-trigger firing. Packaged self-test records executable hashes.
- Controlled session, epoch, reordered-state and finite-reload fences pass against compiled runtime code; distinguished from transport and headset evidence.
- Native Mobile Vulkan overview, street, interior, intact/broken window and pad screenshots included.

Headset/controller comfort, actual microphone audio and Internet reachability remain untested. No VR session launched/stopped. Source overlaps, missing original art and absent bespoke gameplay-object bindings remain open in R9's retained audit and per-map coverage.

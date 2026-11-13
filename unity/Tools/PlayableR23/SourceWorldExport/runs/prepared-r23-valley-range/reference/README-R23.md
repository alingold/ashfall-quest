# ASHFALL R23 (Quest)

Built from **R21** (`Documents/Codex/2026-11-02/ashfall-r21-interactions`). R22 was not used.
Version `1.0.0.17-r23`, Android versionCode 23, package `org.alingold.ashfallquest`.

## Feedback addressed

| Feedback | Change |
|---|---|
| First map load shows the old valley world behind the loading panel | `map_loading_r18.gd`: opaque backdrop sphere around the head while loading |
| Weapon render distance 60 m → 40 m | `ground_item_visibility_r21.gd` `DISTANCE_M=40`, detail retire 45 m, streaming radius follows it |
| Ping 4× bigger at every distance; dots must match | `ping_sprite_r19.gd` 4° (was 1°; target mark keeps 5°). New `ping_column_r23.gd`: dots sized/spaced from the same angular width every frame |
| Backpack style | `item_catalog_ui_088.gd`: lighter gray background, no yellow borders/lines, tiles keep their dark fill, selection uses the menus' blue fill + cyan underline, cyan title |
| Build / revive paddles / ammo / backpack slot style | New white line-art icons `assets/ui/backpack_r23_*.svg` (ammo keeps its family color as an accent) |
| Infinite ammo/builds show 10000 / dash | Backpack shows `∞` (`item_catalog_service_088.gd` `infinite_r23`); gun readouts show `∞` (`ammo_panel.gd`); builds HUD and build status show `∞` |
| Floating stars above ground weapons are redundant | Removed from `weapon_rarity_r6.gd` `decorate` (stars stay in the selection text) |
| Weapon glow may be inefficient | `glow.gdshader` is now additive with no `discard` and no depth writes (cheaper on tiled GPUs); same rim look |
| Rocket launcher ammo counter at the back | `explosive_weapon.gd`: SMAW readout sits beside the hand grip |
| Remove tree-leaf colliders | `foliage_r23.gd`: leaf triangles are removed from the baked world collision |
| Fewer tree leaves far away | `foliage_r23.gd`: leaves split into 24 m ground cells; beyond 45 m each cell draws 1 card in 3 (kept cards 25% larger) |
| Lower-res guns on the map, not in hand | Ground copies use reduced materials (no normal/detail/rim/clearcoat maps, trilinear instead of anisotropic) via `ground_item_visibility_r21.gd` `lite_materials_r23` |
| New skins | 53 styles from the SkinLibrary build (`Documents/ashfall/game`): registry, render body profiles, chest-plate follow, gallery hand reference. PCVR tier only, textures capped at 1024 px for Quest |

Leaf materials are identified from `archive_semantics_r9.json`: any alpha texture named
`*leaves*`/`*leaf*`/`*foliage*` (pine leaves, 22 maps), plus the batched demo-map tree textures
`demo-gamemap-2-batched/s4`, `demo-gamemap-batched/s7`, `demo-gamemap-batched/s46`
(verified by inspecting the textures). Launch flag `--original-foliage-r23` disables the pass.

## Not changed / not verified
* No headset playtest was done by the build. Frame rate, shimmer and feel need on-device checks.
* The one-off Alpha crash was not reproduced; no specific fix.
* Gun pickup lag and jerky flight were not specifically addressed.

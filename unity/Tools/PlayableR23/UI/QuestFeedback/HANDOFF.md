Quest UI feedback implementation, CPU-only; files released to root.

Wheel: no interior TextMesh labels remain. Actual item pictures and original
bag/revive geometry remain inside the arc. The single top label uses source
weapon NAME +space+one-to-five★, source rarity colors, no ammo/reload statistics,
and a larger42-pixel-equivalent em. Root-supplied exact action IDs are untouched.
`R23WeaponWheelSlot(...,stars=0)` accepts actual rarity; omitted values infer
existing★ labels, defaulting to1 for tiered weapons. FRAG/ZONE stay untiered.
`StatusText`/`InteriorTextCount` are readonly native-check seams; the existing
callable wheel fixture asserts actual MK18★ and zero interior text.

Backpack: actual non-testing `item_catalog_ui_088`1.2x.64m board, four columns
and three pinned rows. Inventory cells0/1/4/5/8/9 show three carried slots plus
three locked slots at current capacity; resource cells2/3/6/7/10/11 show actual
host-supplied counts. Genuine weapon PNGs, item SVG pictures and original R23
resource line art are runtime RGBA decoded. No catalog/rarity picker appears.
Fresh grip press equips and closes the backpack before dispatch. Fresh trigger
press drops. Neutral entry, focus/tracking recovery, identity refresh and held
ray changes cannot create extra actions. The X closes with grip or trigger.

Root bindings:

- `R23LoadoutOption` appended optional `profileId`, `stars`. Pass the resolved
  profile and actual instance Stars, including1; consumables pass their Kind.
  `Id` remains the exact action/copy ID for existing EquipRequested/DropRequested.
- `SetBackpackResources(IEnumerable<R23BackpackResource>)` accepts readonly
  `(family,count,canDrop=false)` rows for pistol/rifle/shells/sniper/smg/builds.
  Missing counts display an em dash. Root must explicitly enable and handle
  `ResourceDropRequested(family)` for real source resource drops. This UI never
  owns/debits reserves or grants items. Drop stays disabled without that provider.
- Existing wheel backpack callback must invoke `UI.OpenLoadout`, not OpenWeapons.
  The current root callback already does this; the obsolete catalog-like UI was
  replaced here. Main-menu appearance button now says OUTFITS as requested.
- Existing `SettingsChanged(R23UiSettings)` publishes the new readonly fields:
  `FlingPower`3,[0,5],slider.05; `MotionDamping`1,[0,2],slider.01;
  `OffhandInfluence`0,[0,1],slider.01; `GripToggle`false;
  `DominantHand`1(right),0(left). Root binds actual fling/grip/dominance and
  disarming. MotionDamping/OffhandInfluence are persisted UI fields/events only;
  the current Hands owner has no live filtering/blending implementation yet.
  Settings>WEAPONS calls `OpenWeaponSettings` and shows these source tuning rows.
  Fling lives in LOCOMOTION. All fields persist in the bounded existing v1 local
  record; old records use defaults for missing fields. `With(...)` preserves
  unrelated fields when changing audio/motion.

Fling source distinction: `player.gd:67` initializes3.0. `main.gd` invokes
`field_menu.apply_settings` only if user://settings.json exists and is a
Dictionary. `menu.gd:639` missing-field fallback and reset_movement_defaults are
1.0. Fresh Unity and an old Unity record lacking the field keep3.0, preserving
the admitted climbing behavior. No silent reduction to the saved-file fallback.

Actual `R23GodotUI.ttf` lacks U+2605. Godot's platform font fallback is not
available in legacy Unity TextMesh. New separate `R23InventoryUI.ttf` retains
all1,150 original outlines/metrics and adds only an OFL Noto Sans Symbols2 black
star; the original font remains byte-identical. Font fallback shape is explicitly
platform-dependent; this does not claim identical original OS fallback outlines.
Licenses ship beside the font. Official fallback input:
https://github.com/google/fonts/tree/main/ofl/notosanssymbols2

Source references: original `weapon_wheel.gd` status/icon/utility/rarity methods,
`weapon_rack.gd:get_weapon_label`, `weapon_rarity_r6.gd`,
`item_catalog_ui_088.gd:refresh,point_input,_activate`,
`item_catalog_service_088.gd:cards`, `supply_catalog_088.gd:icon`,
`menu.gd:WEAPON_SETTINGS_DEFAULTS,_labels,_numeric_spec,apply_settings`, and
`main.gd` conditional settings load. Tools/WeaponWheel/icons.json records exact
18 original PNGs (including26/28) and nine source SVG/rasterization hashes.
RGBA RGB is preserved; rarity modulates the sampled source texture without
turning RGB into alpha or losing dark visor details. Existing e82 assets remain
intact; the new fallback font is additive.

Checks passed:37 wheel behavior/instance/name/stars cases;18 original PNG byte
checks plus nine source SVG rasterizations;29 backpack input/identity cases;
30 persistence/default/range/roundtrip checks; retained map/audio/source-font
checks; full installed Runtime/EditorRuntime/BuildAndSmoke API compilation.
Native fixture is callable but unrun. No engine, GPU, device, listening or build
job was performed. Installed e82 and historical releases remain unchanged.

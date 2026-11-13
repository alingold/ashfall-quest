Catalog5 gallery and catalog-caption correction, 2026-11-05. Files released to root.

`R23LobbyWeaponGallery` now supports sixteen profiles, adding `1911`, `tec9`,
`rfb`, `fal`, `s85`. These use the actual `SourceRifleDefinition`, private
`LocalRifleSandbox`, original payload/fit, `SourceCatalog5Feedback`,
`SourceCatalog5Optics`, and merged original audio routes. No carried inventory,
ammo provider, or world damage authority is attached. Source live097 previews
have unlimited private reserve. Original PX4/Magnum/M1014 finite preview behavior
is preserved. S85 hides its seated clip, retains gun-basis loose clip, uses the
raised offer-relative insertion zone and real per-shot bolt cycle. RFB has the
source custom click-only path: fire then eject, with no stick-down eject.

`R23ExpandedGalleryFixture.RunCatalog5(...)` is additive; `RunAll(...)` now covers
sixteen actual decoded single previews. It synchronizes/restores the real UI's
browse caption through `UI.BrowseWeapon(id)`, without equip. Root must populate
UI catalog options for all gallery profiles independently of carried-runtime
availability. Gallery/action profile IDs remain separate from inventory instance
action IDs. Wheel icons are actual source indices19/20/23/24/25; existing eleven
icon bytes are preserved. Same-profile wheel copies retain distinct action IDs.

The native images in `floor-20261105-051246` had a real font error: the UI used
full generated line-box height to scale capitals. Actual retained R23GodotUI at
96px has line height130.734px and WEAPONS ink70.547px; native bounds13.07 and
47.10 match the font's line metrics and advance at Unity's0.1 scale. Corrected
scaling preserves source em size and actual-width overflow guard; it does not
change the2.20x1.375m board. Weapon caption now matches source17px,350x28 at
(465,550). Removed invented visible SELECT WEAPON; legacy event remains callable
through `UI.RequestWeaponSelection(id)`.

Projection limit: original `front_menu_091.gd:21-25,41` renders desktop UI through
a fullscreen1280x800 CanvasLayer, while XR uses the world board with no depth
test. Original `lobby_gallery_091.gd:33-35` docks full-size weapons1.10m forward
and.16m below the viewing camera. The Unity native gallery PNGs render that VR
world board through a1280x720 desktop camera. They are not source desktop-UI
captures. Production source board/dock placement is preserved. The inspected
genuine source lobby reference is
`docs/agent-runs/20261104-three-chats/world/r12-world-engine-batch/r8-map-runtime/game/incumbent/zero-request-no-application-lobby.png`;
no genuine source gallery screenshot was located in the bounded source/capture
search. Font and layout decisions use primary source code and actual font/native
metrics, not an invented reference screenshot.

Primary inputs are the preserved original
`C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source/scripts/`:
`front_menu_091.gd:25,32-36,58-61`, `front_menu1000.gd:214-220`,
`lobby_gallery_091.gd`, `catalog_firearm.gd`, and the real source-art provenance
beside each `<id>-fit.json`. RFB ordering follows `catalog_firearm._poll_vr`;
S85 bolt/insertion/clip behavior follows that script's explicit profile branches.

Checks passed:70 new actual private-owner/control/reload cases; five actual
payload/texture/fit/audio bindings;33 wheel cases and sixteen byte-identical
original PNGs; retained seven-profile/SMG gallery suites; actual TTF/native
line-box comparison; installed runtime, EditorRuntime and BuildAndSmoke API
compilation. No new engine, rendering, audio-listening, headset or device run.
Installed7430 and all released builds remain unchanged.

# SPR Compact Rectangular Scope — Asset Collaboration Handoff (`gemini-argon`)

## 1. Summary of Improvements Over `094` / `097`

In standalone `094`–`097` (`scripts/spr.gd` + `scripts/spr_housing_094.gd`), the SPR optic combined:
- **An oversized housing**: `LongSPRScopeHousing094` scaled by `0.8333x` in model space (`1.10x` in `1.32x` held weapon space) produced an outer width of **`111.7 mm` in model space (`147.4 mm` in held weapon space)**—more than **3.2x wider** than the rifle receiver (`34.7 mm` model / `45.8 mm` world).
- **A tall stacked riser**: `RectangularSPRScopeMount084` (`y = 0.071`) + `SPRClampedOpticRiser088` (`y = 0.084`) + the housing base block (`y = 0.104`) placed the optical centerline at **`y = 0.12583 m` in model space (`0.16610 m` in world space)**, floating **`51.0 mm` (`67.3 mm` world)** above the receiver's top Picatinny rail (`y = 0.07482 m`).
- **An open hollow rectangular rear box**: the `0.122 x 0.083 x 0.120` rear walls had no internal baffle bulkhead behind the cylindrical front tube, leaving off-axis gaps.

### New Compact Low-Mount SPR Scope Design (Matching User Reference Image)
1. **Low Credible Picatinny Mount (`ScopeMount`)**:
   - Eliminates the tall riser legs completely.
   - Clamps directly onto the receiver's top Picatinny rail (`y = 0.07482 m`, `x = ±0.01054 m`, `z = -0.048 m` to `-0.176 m`) with dovetail side jaws, **two left-side clamping blocks with cross-bolts** (`z = -0.082 m` and `-0.148 m`), and **two low-profile scope rings** cradling the central tube.
   - Lowers the optical axis from `y = 0.12583 m` to **`y = 0.09800 m` in model space (`0.12936 m` in `1.32x` weapon space)**—only **`23.18 mm` above the top rail in model space (`30.60 mm` in world space)**, a **54.6% reduction in height above the rail**.
   - Bottom of the rear rectangular ocular housing sits at `y = 0.08050 m` in model space, maintaining **`4.4 mm` clearance above the Picatinny rail** and **`6.37 mm` vertical clearance above the rear T-shaped charging handle (`Bolt`, `y_max = 0.07413 m`)** so charging-handle articulation is completely unobstructed.
2. **Compact Beveled Rectangular Rear Ocular Unit (`ScopeHousing`)**:
   - Outer dimensions reduced from `111.7 mm × 73.3 mm` to **`54.0 mm × 35.0 mm` in model space (`71.28 mm × 46.20 mm` in `1.32x` world space)** with clean `4.5 mm` 45° corner bevels and a rear rubberized eyecup (`54.8 mm × 35.8 mm`).
   - Includes sculpted side pockets with 4 vertical grip ribs, the **rear circular throw-lever / focus boss** (`z = -0.0105 m`), seated transition fasteners, and the **top flat keypad plate** (`z = -0.022 m`) with 4 miniature buttons and corner fasteners matching the reference image.
3. **Central Turret Saddle & Flared Stadium Front Objective Bell (`ScopeHousing` + `FrontObjectiveGlass`)**:
   - Smoothly transitions from the rectangular rear box into a `28.4 mm` diameter central saddle between the two mounting rings, with a **low-profile crenellated top elevation turret** (`y_max = 0.1200 m`) and a **knurled left-side windage/parallax drum** (`x = -0.0185 m`, `z = -0.1150 m`).
   - Ahead of the front mounting ring (`z = -0.148 m`), flares into a **horizontally elongated stadium / rounded-rectangular front objective hood** (`49.0 mm` wide × `33.5 mm` tall at `z = -0.270 m`).
   - Inside the **`14.0 mm` recessed matte-black front hood baffle (`z = -0.256 m`)**, houses the **main cyan/teal multi-coated circular objective lens (`Ø 24.4 mm` at `x = +0.0055 m`)** alongside **two vertically stacked auxiliary rangefinder/sensor lenses (`Ø 8.2 mm` at `x = -0.0135 m, y = 0.0980 ± 0.0062 m`)**, matching the reference image.
4. **Unobstructed `22:13` Rectangular Aperture, Recessed Glass, and Dark Sealed Interior (`RearOcularGlass`, `ReticleViewPlane`, `ScopeDarkInterior`)**:
   - Preserves the exact standalone `097` rectangular aspect ratio: **`44.0 mm × 26.0 mm` in model space (`58.08 mm × 34.32 mm` in `1.32x` world space)**, where `0.044 / 0.026 = 0.11 / 0.065 = 22 / 13 ≈ 1.692308` (matching `viewport.size = Vector2i(520, 307)`).
   - `RearOcularGlass` is recessed `4.0 mm` inside the rear rubber eyecup (`z = +0.0180 m`), backed by `ScopeDarkInterior` (a sealed matte-black stepped optical baffle chamber extending to `z = -0.0820 m`) so static views and off-axis angles never show hollow see-through gaps.
   - `ReticleViewPlane` sits at `[0.0, 0.0980, +0.0185]` with eye-facing normal `(0, 0, +1)` and optical forward axis `(0, 0, -1)` aligned directly to the accepted firing datum (`origin = [0.0, 0.045, -0.69]`, `forward = [0.0, 0.0, -1.0]`) without any arbitrary reticle offset.

---

## 2. Named-Part Contract & Coordinate Documentation

All standalone scope GLBs (`models/spr_scope_pcvr.glb`, `models/spr_scope_light.glb`) and full rifle assembly GLBs (`models/spr_rifle_with_scope_pcvr.glb`, `models/spr_rifle_with_scope_light.glb`) are authored in **unscaled SPR `model` local space** (`1 unit = 1 metre`; held weapon world scale in `model_scale_084.gd` is `1.32`).

| Named Part / Node | Role | Key Local Coordinates (`model` space, metres) | Effective Dimensions (`model` / `1.32x` world) |
| :--- | :--- | :--- | :--- |
| `ScopeMount` | Low-profile dual-clamp Picatinny base + 2 scope rings | Base center `[0, 0.0762, -0.112]`; rings at `z = -0.082, -0.148` | `41.0 × 45.0 × 126.0 mm` / `54.1 × 59.4 × 166.3 mm` |
| `ScopeHousing` | Rectangular rear ocular box, turret saddle, stadium front bell, rubber eyecup | Optical centerline `y = +0.0980`, `z ∈ [-0.2700, +0.0220]` | `56.9 × 41.5 × 292.0 mm` / `75.1 × 54.8 × 385.4 mm` |
| `ScopeDarkInterior` | Sealed matte-black front & rear optical baffle chambers | Rear baffle `z ∈ [-0.0820, +0.0172]`; front baffle `z ∈ [-0.2700, -0.2540]` | `44.6 × 29.2 × 287.2 mm` / `58.9 × 38.5 × 379.1 mm` |
| `FrontObjectiveGlass` | Recessed main cyan objective lens + 2 stacked auxiliary sensor lenses | Center plane `z = -0.2560` (`14.0 mm` recessed inside `-0.2700` hood) | Main `Ø 24.4 mm` (`x = +0.0055`), Aux `2 × Ø 8.2 mm` (`x = -0.0135`) |
| `RearOcularGlass` | Recessed rectangular rear ocular lens | Center `[0.0, +0.0980, +0.0180]` (`4.0 mm` recessed inside `+0.0220` bezel) | `44.0 × 26.0 mm` / `58.1 × 34.3 mm` (Aspect `1.6923:1`) |
| `ReticleViewPlane` | Unobstructed per-eye scope shader / reticle quad (`DynamicScopeLens`) | Center `[0.0, +0.0980, +0.0185]`, normal `+Z`, optical forward `-Z` | `44.0 × 26.0 mm` / `58.1 × 34.3 mm` (`520 × 307` viewport) |

---

## 3. Deliverables & Statistics

| File | Description | Vertices | Triangles | Materials / Embedded Textures |
| :--- | :--- | ---: | ---: | :--- |
| `models/spr_scope_pcvr.glb` | Self-contained PCVR detail scope asset | `4,238` | `3,504` | `6` PBR materials / `6` embedded PNGs |
| `models/spr_scope_light.glb` | Self-contained lightweight scope variant (`-49.7%` tris) | `2,066` | `1,764` | `6` PBR materials / `6` embedded PNGs |
| `models/spr_rifle_with_scope_pcvr.glb` | Accepted SPR rifle (`Body`, `Magazine`, `Bolt`) + PCVR scope | `28,670` | `11,648` | `10` PBR materials (self-contained) |
| `models/spr_rifle_with_scope_light.glb` | Accepted SPR rifle (`Body`, `Magazine`, `Bolt`) + Light scope | `26,498` | `9,908` | `10` PBR materials (self-contained) |
| `godot_prototype/spr_housing_094.gd` | Drop-in Godot housing script (supports GLB or procedural build) | — | — | Uses `optic_material_094.gd` |
| `godot_prototype/spr_scope.gd` | Isolated Godot 4 per-eye 2x scope script (`WINDOW_SIZE = (0.044, 0.026)`) | — | — | Preserves `097` stereo shader & `1.0 m` reticle |
| `godot_prototype/spr.gd` | Isolated Godot 4 SPR script without old `0.8333x` scale / riser legs | — | — | Preserves `1.32x` weapon fit & firing datum |
| `godot_prototype/spr_scope_prototype.tscn` | Isolated Godot 4 scene referencing `spr_scope_pcvr.glb` | — | — | — |

---

## 4. Limitations & Remaining Integration Work

- **Isolated Draft Only**: No files in `standalone097-playtest` or the native workspace outside `asset-collaboration/gemini-argon` were modified.
- **Godot Prototype vs. Native Render Target**: The scripts in `godot_prototype/` preserve standalone `097`'s per-eye `SubViewport` (`520 × 307`), `1.0 m` virtual reticle depth, and rectangular edge vignette. If used in native Skyrim VR later, `ReticleViewPlane` provides the exact unobstructed aperture quad, while native render-target hooks and shot/reticle synchronization remain owned by Sol.
- **Test Update Note for Standalone Integration**: If adopting `WINDOW_SIZE = Vector2(0.044, 0.026)` and `COMPACT_OCULAR = Vector3(0.0, 0.098, 0.0185)` directly in standalone `scripts/spr_scope.gd` and `scripts/spr.gd` (with `optic.scale = Vector3.ONE`), update the legacy `086` mount-height assertion in `tests/feedback_086.gd` and the raw `0.065` constant in `tests/snipers_empty_carry_051.gd` / `052.gd` to read `spr.optic.WINDOW_SIZE.y`.


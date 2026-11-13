# Source lobby shader corrections

Only `R23LobbySurface.shader` and `R23LobbySky.shader` changed. Their existing metadata GUIDs and the runtime property contract remain unchanged: `_BaseColor`, `_Metallic`, `_Roughness`, `_Emission`, `_Facade`; the sky still needs no material parameters. No runtime, geometry, scene, UI, built executable or preserved source file was edited.

Primary inputs are `quest-r24/source/scripts/lobby_map_091.gd`, `scripts/front_menu_091.gd:83–84`, `assets/materials/lobby_facade_091.gdshader`, and the already-retained original generated ProceduralSkyMaterial code in `Tools/PlayableR23/Lobby/GodotSkyReference.cpp:300–402`. File hashes are in `checks.json`.

## Concrete changes

- Source facade thresholds use the interpolated `MODEL_NORMAL_MATRIX * NORMAL` before normalization. The old Unity shader normalized first, changing wall-axis selection and roof/pane suppression around rounded/beveled edges. The vertex stage now carries the raw inverse-transpose world normal. Fragment lighting separately normalizes it. Godot world positions/normals are reconstructed by one Z reflection, preserving the actual +1000 Z lobby origin and window phase.
- Facade grid `.62/.64`, pane bounds `.16/.84/.20/.80`, hash constants, occupancy threshold, albedo and emission now live in a directly comparable source formula function. Source linear ALBEDO is encoded once for Unity Gamma working-space PBR. Linear EMISSION is added after decoding that lighting result, then encoded with the combined result. The previous shader treated linear procedural RGB as display RGB and added it there.
- Ordinary source materials now use URP metallic/roughness PBR with the genuine source values, replacing the old custom Blinn exponent and invented .08 ambient floor. Facade parameters remain source metallic .6/roughness .43; deck/pedestal/neon values come through the unchanged existing bindings. Native Godot-vs-URP BRDF/reflection/exposure parity is not claimed.
- Current `R23Lobby.BuildMaterials` sends neon `_Emission = Hex(color) * 1.5`. The shader reconstructs `SRGBToLinear(_Emission / 1.5) * 1.5`, so source energy is applied after source-color decoding. This is deliberately tied to the actual R23 fixed emission gain, not a general new emission schema. No current Gamma-project binding change is needed. A future switch to Linear color space should revalidate Unity Color-property uploads and this binding before claiming parity.
- Sky colors are decoded from source sRGB before the exact generated linear interpolation, followed by one output Gamma encoding. Source top `246eb6`, horizon `a3c9e4` and bottom `354859` are preserved, with the actual `.15/.02` sky/ground curves, 30-degree sun-angle limit and `.15` sun curve. The old Gamma interpolation differs by up to .0907 in the focused working-RGB samples.
- The invented .5-degree disk was removed. The source does not override the default zero light angular distance. Godot passes that angle in radians to the sky; Unity now uses `cos(0) = 1` for the same procedural sun/halo formula. See [Godot 4.7 Light3D defaults](https://docs.godotengine.org/en/4.7/classes/class_light3d.html#class-light3d-property-light-angular-distance) and [primary renderer sky-light mapping](https://github.com/godotengine/godot/blob/master/servers/rendering/renderer_rd/environment/sky.cpp#L1018). The real lobby sun direction/radiance remain supplied by `GetMainLight`, matching root's source light placement.

Neither active source shader has an animation clock or a fire background. None was added. Godot's framebuffer debanding, sky reflection convolution, tone/exposure and native BRDF differences remain explicit limits.

## Narrow root follow-up, outside this ownership

The PBR surface now uses the scene's reflection environment. `R23Lobby.ApplyEnvironment` currently changes skybox/sun/flat ambient only; it does not explicitly refresh the default sky reflection. At the next reserved native slot, verify that the reflection source follows the lobby sky when arriving from a map. If it remains stale, the root owner can preserve/restore `RenderSettings.defaultReflectionMode`, set `DefaultReflectionMode.Skybox` while the lobby is active, and request `DynamicGI.UpdateEnvironment()` once after each environment transition. That engine work was neither executed nor added here. Source ambient is `Color("bbd9ed")` with separate .55 energy; the current runtime's `Hex("bbd9ed") * .55` also needs native/API color-space calibration before exact ambient parity can be claimed.

## CPU check

```powershell
python unity/Tools/PlayableR23/Lobby/ShaderParity/check.py
```

Twelve original GLSL/HLSL facade cases compare grid, pane, occupancy, albedo and emission with zero expression error, including a case that exposes the old normal-normalization defect. Seven source-generated sky gradient cases and six source sun-halo cases pass, along with current emission binding, coordinate reflection, source properties and unchanged meta GUIDs. `checks.json` retains source hashes, representative values and limits. The pseudo-random occupancy hash uses the original float formula; GPU sin/precision differences are still a native comparison matter.

No HLSL compilation, native engine, GPU, capture, headset, Blender or external export job was started. Existing human-test executables remain unchanged; next native/render checks belong to the coordinator.

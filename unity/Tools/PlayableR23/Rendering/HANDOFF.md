# Source Godot FILMIC in URP17

New owned runtime: `Assets/Ashfall/Integration/Playable/Rendering`. No map, loop, UI, build/editor or pipeline asset was changed. The feature defaults to **disabled and input-unqualified**. Installed Unity6000.6.8/URP17.6 compile with warnings-as-errors and57 actual compiled C# math/domain cases passed. Shader compilation, native rendering and Quest/XR execution are **unrun**.

## Small installation / session API

```csharp
var feature = R23GodotFilmic.Install(rendererData);
// In the root-owned Editor builder: persist feature as rendererData's subasset,
// dirty/save both; the installer itself has no UnityEditor dependency.
R23GodotFilmic.PrepareHdr(pipelineAsset, camera); // explicit HDR64 + camera HDR
// Only after proving the actual shader/compositing producer is scene-referred:
R23GodotFilmic.QualifyPipeline(pipelineAsset, camera, producerIsSceneReferred: true);
R23GodotFilmic.SetCamera(camera); // optional; null permits final Game/VR cameras
R23GodotFilmic.SetSourceEnvironment(active: sourceUsesFilmic, exposure: 1, white: 1);
// Lobby/map/environment exit must update the source flag to that environment's mode.
```

The namespace is `Ashfall.Integration.Playable.Rendering`. `Install` reuses an existing feature. Root owns Editor subasset persistence and renderer/pipeline installation. `PrepareHdr` changes only HDR precision/enabling; it **does not qualify the producer**, change ColorSpace, enable a Volume, or activate FILMIC. `QualifyInput(false)` withdraws qualification. `LastStatus` and `RecordedPasses` are recording diagnostics, never evidence of GPU execution or headset delivery.

The pass requires **URP RenderGraph enabled**, uses `BeforeRenderingPostProcessing`, requests an intermediate color texture, clones the actual HDR/XR descriptor, blits to a separate graph texture and swaps `cameraColor`. A fresh property block snapshots exposure/white/domain per pass. It uses `TEXTURE2D_X`, stereo eye setup and all texture slices; no `cmd.Blit`, per-eye camera manipulation or backbuffer read/write aliasing. LDR/backbuffer/HDR-display output is bypassed explicitly. It applies once on the final Game/VR camera of a stack. Native shader support and XR acceptance remain unverified.

## Input and output contract

- Required: genuine **scene-referred radiance before display tone mapping/clipping**, stored in a float HDR camera buffer. `R16G16B16A16_SFloat` is requested; the graph also accepts known RGB float HDR formats. Camera and pipeline HDR must both be enabled.
- Default input domain is `LinearSceneReferred`. `SrgbEncodedSceneReferred` is available only for an actual sRGB encoding of that radiance. **Gamma-space lighting arithmetic is not repaired by decoding its finished frame.** This project currently uses Gamma; do not assert producer qualification solely because HDR storage is enabled.
- Source exposure multiplies radiance before the FILMIC rational function; white normalizes that function. Both source defaults are1. Negative input follows source max0; values above1 are not preclamped. With source white1, values above white can still exceed1 and clip at the SDR output. This does not invent highlight recovery from an already clipped image.
- The Compatibility output uses Godot's power-based linear-to-sRGB approximation, including its different near-black behavior. In a Linear Unity project the pass inverses the standard final sRGB conversion once, so URP's later standard SDR output conversion yields the source encoding. In Gamma it emits that encoding directly. Root must preserve that final output contract; no additional ACES/Neutral/tonemapping/color grading/OETF should follow. HDR display output needs a separate transform and is bypassed.
- This pass maps the complete scene color, including world UI/transparent composition. Godot Compatibility maps base and additive material passes before framebuffer composition; a full-frame transform cannot reproduce that ordering exactly for translucency/additive lighting. Native matched comparison is required before claiming full-frame parity.

## Source binding

Primary [Godot4.5 Compatibility equations](https://raw.githubusercontent.com/godotengine/godot/4.5/drivers/gles3/shaders/tonemap_inc.glsl): `tonemap_filmic`, bias2, constants A=.22/B=.30/C=.10/D=.20/E=.01/F=.30 and white normalization. [Current master](https://raw.githubusercontent.com/godotengine/godot/master/drivers/gles3/shaders/tonemap_inc.glsl) has the same numerator/constants; the white denominator moved to a CPU-computed uniform. The implementation uses the requested4.5 form, not ACES/Neutral or a contrast substitute.

[Environment defaults](https://raw.githubusercontent.com/godotengine/godot/4.5/scene/resources/environment.h) are exposure1/white1. Existing qualified Valley environment data also captured FILMIC2/exposure1/white1. Frozen R6 `archive_map1003.gd` and `cyber_bird.gd` select FILMIC without overriding those defaults. Frozen `project.godot` selects **Mobile** by default; Compatibility requires a launch override. [Compatibility scene order](https://raw.githubusercontent.com/godotengine/godot/4.5/drivers/gles3/shaders/scene.glsl) maps after lighting/emission/ambient/fog and exposure, then encodes for its framebuffer; additive passes map separately.

Official small inputs, hashes, MIT license and source notes are retained under `official/` and `source.json`. The MIT notice is also packaged as `Resources/GODOT-LICENSE.txt`. Lighting BRDF, shadowing, material gamma, fog/glow and renderer-order differences remain separate from the exact FILMIC curve.

## Compile and finite native check

`python unity/Tools/PlayableR23/Rendering/check.py` uses installed Roslyn, not the obsolete.NET4 C#5 compiler: `Editor/Data/NetCoreRuntime/dotnet.exe` + `Editor/Data/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll`. Keep warnings-as-errors. `runtime-references.txt` lists the six additional installed Core/Shared/Universal/Config/ShaderLibrary/GPUDriven DLL references needed by root's full runtime checks. No dependencies were installed.

One future admitted render check: float HDR patches at radiance0/.001/.18/.5/1/4, source exposure1/white1, verify output samples and unchanged alpha against the source equations; compare bypass and enabled on the same unmodified scene/camera. Check actual render-graph HDR format and both XR array slices. Repeat one matched opaque source map view; keep Lobby and other environment modes disabled unless their source selects FILMIC. This check is prepared, not executed.

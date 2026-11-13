# Original Valley alpine terrain shader

Published resource: `Assets/Ashfall/Integration/Playable/World/Resources/R23AlpineTerrain.shader`, shader name `Ashfall/R23AlpineTerrain`. Load with `Resources.Load<Shader>("R23AlpineTerrain")`. The only material property is `_SnowHeight`, mapped directly from original `snow_height`; default is 55.0. Do not replace the 24 actual per-material values with the default.

Primary source is `C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source/assets/materials/alpine_terrain.gdshader`, SHA256 `888361e92cce4fd849455f77a16ad928387403e2efb74a8a50228245a2991170`. The completed `SourceWorldExport/runs/prepared-r23-valley-range/valley/world.json` contains 24 ShaderMaterials using this exact exported shader hash. `materials.json` lists each source material resource ID and its `_SnowHeight` value (35.196442947387695 through 79.20827270507813). Root owns material creation and v2 mesh channel population.

## Mesh channel contract

| Channel | Meaning | Use |
|---|---|---|
| POSITION | Baked Unity position relative to map root | Projection, lighting, shadows, fog |
| NORMAL | Transformed Unity normal | Lighting only |
| TEXCOORD0 | Source UV | Unused by this shader |
| COLOR | Source color | Unused by this shader |
| TEXCOORD1 float3 | Original **unreflected Godot local** VERTEX | Exact source fragment position |
| TEXCOORD2 float3 | Original **unreflected Godot local** NORMAL | Exact source slope calculation |

The last two attributes pass unchanged from vertex to fragment, independent of node transforms, chunk batching or map origin. Normalize the source normal in the fragment, as in the original; do not normalize it before interpolation. Do not reflect source local Z, substitute world height/world normal, or bake the terrain formula into vertex colors.

`R23SourceAlbedo` preserves the actual drift, altitude smoothstep, slope smoothstep, accumulation, strata and stone/snow mix. It evaluates per fragment. The source constants are linear shader ALBEDO, not sRGB color-picker data. The current Unity project is Gamma (`m_ActiveColorSpace: 0`): `R23WorkingAlbedo` encodes this linear result to sRGB once, then uses URP's ordinary working-space light/SH/fog convention. In a Linear project it passes through unchanged. There is no extra output gamma conversion. In particular, raw source stone .15 maps to about .424 working-space sRGB under white illumination, rather than incorrectly staying .15 or being encoded twice.

Lighting adapts source `diffuse_burley`, roughness 1, `specular_disabled` and `cull_disabled`: Burley rough diffuse uses URP's light-intensity convention, with no specular/reflection lobe; the main light, additional lights, SH ambient, attenuation and shadow receivers use installed URP APIs. Back faces retain the source albedo/slope varying and use a flipped lighting normal. Opaque URP shadow-caster and depth passes preserve terrain participation; forward/depth support stereo and instancing. The extra-light loop follows the installed URP 17.6 forward/clustered APIs.

The albedo formula is the exact port. Godot environment/sky/exposure, tonemapping, diffuse radiometric scaling, ambient convolution, light attenuation/shadow bias, backface handling and Unity Gamma-space lighting can still change the final image. The source's `ROUGHNESS=1`/`SPECULAR=0` are represented by diffuse-only lighting, not by an unverified stock metallic shader. No final rendered or tone parity is claimed.

## Focused CPU check

```powershell
python unity/Tools/PlayableR23/World/ValleyShader/check.py
```

The small restricted expression evaluator reads the actual original GLSL and published HLSL source formulas. Twelve cases compare every intermediate term and final linear albedo across altitude/slope boundaries, down-facing/high wall normals, differing normal magnitudes, positions, and minimum/maximum actual material uniforms. A deliberately reflected local-Z sample detects the wrong-coordinate path. It also checks all 24 exported shader hashes/uniforms, the source-channel contract, and single-encode Gamma roundtrips. `checks.json` retains values and hashes.

These are CPU formula checks and installed package source inspection. No HLSL compilation, Godot/Unity process, GPU/render job, hardware/device test, or source-tree edit occurred. Native shader import/lighting/stereo/shadow appearance remains to be verified by the coordinator's reserved engine lane.

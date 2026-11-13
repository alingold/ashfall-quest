Shader "Hidden/Ashfall/R23GodotFilmic"
{
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" }
  ZWrite Off ZTest Always Cull Off
  Pass
  {
   Name "GodotFilmic"
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #pragma multi_compile _ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #define USE_FULL_PRECISION_BLIT_TEXTURE
   #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
   float4 _R23FilmicParameters; // exposure, white, input encoding, URP final sRGB conversion

   // Godot Engine MIT; adapted from 4.5/drivers/gles3/shaders/tonemap_inc.glsl.
   // Copyright (c) 2014-present Godot Engine contributors.
   // Copyright (c) 2007-2014 Juan Linietsky, Ariel Manzur. See GODOT-LICENSE.txt.
   float3 GodotFilmic(float3 color, float white)
   {
    const float exposure_bias=2.0;
    const float A=.22*exposure_bias*exposure_bias, B=.30*exposure_bias;
    const float C=.10, D=.20, E=.01, F=.30;
    float3 mapped=((color*(A*color+C*B)+D*E)/(color*(A*color+B)+D*F))-E/F;
    float mappedWhite=((white*(A*white+C*B)+D*E)/(white*(A*white+B)+D*F))-E/F;
    return mapped/mappedWhite;
   }
   float3 ExactSrgbDecode(float3 encoded)
   {
    encoded=max(encoded,0.0);
    return float3(encoded.r<=.04045?encoded.r/12.92:pow((encoded.r+.055)/1.055,2.4),
                  encoded.g<=.04045?encoded.g/12.92:pow((encoded.g+.055)/1.055,2.4),
                  encoded.b<=.04045?encoded.b/12.92:pow((encoded.b+.055)/1.055,2.4));
   }
   float4 Frag(Varyings input):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    float4 sceneColor=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,input.texcoord);
    float3 radiance=_R23FilmicParameters.z>.5?ExactSrgbDecode(sceneColor.rgb):sceneColor.rgb;
    // Source exposure precedes FILMIC; negative radiance is clamped, HDR highlights are not.
    float3 mapped=GodotFilmic(max(radiance*_R23FilmicParameters.x,0.0),_R23FilmicParameters.y);
    // Exact Compatibility output approximation, including its different near-black behavior.
    float3 display=max(1.055*pow(max(mapped,0.0),.416666667)-.055,0.0);
    // A Linear URP final blit/sRGB target encodes later. Precompensate that ONE
    // conversion so the display receives the source transfer, not a second OETF.
    float3 output=_R23FilmicParameters.w>.5?ExactSrgbDecode(display):display;
    return float4(output,sceneColor.a);
   }
   ENDHLSL
  }
 }
}

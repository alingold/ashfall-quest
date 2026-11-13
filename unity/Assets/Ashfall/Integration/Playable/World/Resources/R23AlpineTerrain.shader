Shader "Ashfall/R23AlpineTerrain"
{
 Properties
 {
  _SnowHeight("Original Godot snow_height", Float) = 55.0
 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
  Pass
  {
   Name "SourceAlpineForward"
   Tags { "LightMode"="UniversalForward" }
   Cull Off ZWrite On
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #pragma multi_compile_fog
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
   #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float _SnowHeight;
   CBUFFER_END

   struct Attributes
   {
    float4 positionOS:POSITION; // v2: baked Unity positions, relative to the map root.
    float3 normalOS:NORMAL;    // v2: baked Unity normals, for lighting only.
    float2 uv:TEXCOORD0;
    float4 color:COLOR;
    float3 sourcePosition:TEXCOORD1; // Original unreflected Godot LOCAL VERTEX.
    float3 sourceNormal:TEXCOORD2;   // Original unreflected Godot LOCAL NORMAL.
    UNITY_VERTEX_INPUT_INSTANCE_ID
   };
   struct Varyings
   {
    float4 positionCS:SV_POSITION;
    float3 positionWS:TEXCOORD0;
    float3 normalWS:TEXCOORD1;
    float3 sourcePosition:TEXCOORD2;
    float3 sourceNormal:TEXCOORD3;
    float fog:TEXCOORD4;
    float4 shadowCoord:TEXCOORD5;
    UNITY_VERTEX_OUTPUT_STEREO
   };
   Varyings Vert(Attributes a)
   {
    Varyings o;
    UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    VertexPositionInputs positions=GetVertexPositionInputs(a.positionOS.xyz);
    o.positionWS=positions.positionWS;o.positionCS=positions.positionCS;
    o.shadowCoord=GetShadowCoord(positions);
    o.normalWS=TransformObjectToWorldNormal(a.normalOS);
    // Do not reflect, normalize, transform or replace either source varying here.
    // Godot interpolates local NORMAL then normalizes it in fragment().
    o.sourcePosition=a.sourcePosition;o.sourceNormal=a.sourceNormal;
    o.fog=ComputeFogFactor(o.positionCS.z);
    return o;
   }

   // SOURCE_ALBEDO_BEGIN: alpine_terrain.gdshader fragment formula, in linear RGB.
   float3 R23SourceAlbedo(float3 p,float3 terrainNormal)
   {
    float drift=sin(p.x*0.055+sin(p.z*0.037))*3.0+sin(p.z*0.071)*2.0;
    float altitude=smoothstep(_SnowHeight-12.0,_SnowHeight+10.0,p.y+drift);
    float slope=smoothstep(0.52,0.90,normalize(terrainNormal).y);
    float accumulation=altitude*lerp(0.08,0.90,slope);
    float strata=sin(p.y*0.22+sin(p.x*0.035)*1.5+sin(p.z*0.04));
    float3 stone=float3(0.15,0.175,0.19)*(0.94+strata*0.06);
    float3 snow=float3(0.44,0.49,0.53);
    return lerp(stone,snow,accumulation);
   }
   // SOURCE_ALBEDO_END

   float3 R23WorkingAlbedo(float3 linearSource)
   {
    // Source shader ALBEDO literals/math are linear. URP's Gamma project lighting
    // consumes working-space albedo; encode exactly once here, never again at output.
    #if defined(UNITY_COLORSPACE_GAMMA)
     return LinearToSRGB(linearSource);
    #else
     return linearSource;
    #endif
   }
   float R23Pow5(float x){float x2=x*x;return x2*x2*x;}
   float3 R23BurleyLight(Light light,float3 n,float3 view)
   {
    float nl=saturate(dot(n,light.direction));
    float nv=saturate(dot(n,view));
    float lh=saturate(dot(light.direction,SafeNormalize(light.direction+view)));
    // Disney/Burley diffuse with the source's roughness fixed at 1.0.
    // URP light intensities use its ordinary diffuse convention (Lambert = N.L).
    float fd90=0.5+2.0*lh*lh;
    float scatter=(1.0+(fd90-1.0)*R23Pow5(1.0-nl))*(1.0+(fd90-1.0)*R23Pow5(1.0-nv));
    return light.color*(light.distanceAttenuation*light.shadowAttenuation*nl*scatter);
   }
   half4 Frag(Varyings i,FRONT_FACE_TYPE frontFace:FRONT_FACE_SEMANTIC):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float3 albedo=R23WorkingAlbedo(R23SourceAlbedo(i.sourcePosition,i.sourceNormal));
    float3 n=normalize(i.normalWS)*IS_FRONT_VFACE(frontFace,1.0,-1.0);
    float3 view=GetWorldSpaceNormalizeViewDir(i.positionWS);
    // No baked vertex-color albedo, normal noise, specular or reflection lobe.
    float3 irradiance=max(SampleSH(n),0.0);
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
     float4 shadowCoord=i.shadowCoord;
    #else
     float4 shadowCoord=TransformWorldToShadowCoord(i.positionWS);
    #endif
    Light mainLight=GetMainLight(shadowCoord);
    irradiance+=R23BurleyLight(mainLight,n,view);
    #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
     InputData inputData=(InputData)0;
     inputData.positionWS=i.positionWS;
     inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
     #if USE_CLUSTER_LIGHT_LOOP
      [loop] for(uint lightIndex=0;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);lightIndex++)
      {
       CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
       irradiance+=R23BurleyLight(GetAdditionalLight(lightIndex,i.positionWS,half4(1,1,1,1)),n,view);
      }
     #endif
     uint pixelLightCount=GetAdditionalLightsCount();
     LIGHT_LOOP_BEGIN(pixelLightCount)
      irradiance+=R23BurleyLight(GetAdditionalLight(lightIndex,i.positionWS,half4(1,1,1,1)),n,view);
     LIGHT_LOOP_END
    #endif
    return half4(MixFog(albedo*irradiance,i.fog),1.0);
   }
   ENDHLSL
  }
  Pass
  {
   Name "ShadowCaster"
   Tags { "LightMode"="ShadowCaster" }
   Cull Off ZWrite On ZTest LEqual ColorMask 0
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex ShadowPassVertex
   #pragma fragment ShadowPassFragment
   #pragma multi_compile_instancing
   #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
   // Original terrain is fully opaque; the standard URP caster needs no albedo.
   #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float _SnowHeight;
   CBUFFER_END
   ENDHLSL
  }
  Pass
  {
   Name "DepthOnly"
   Tags { "LightMode"="DepthOnly" }
   Cull Off ZWrite On ColorMask R
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex DepthOnlyVertex
   #pragma fragment DepthOnlyFragment
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float _SnowHeight;
   CBUFFER_END
   ENDHLSL
  }
 }
 FallBack Off
}

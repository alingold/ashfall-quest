Shader "Ashfall/EditorControl/R23AppearanceSpecularBefore"
{
 Properties
 {
  _BaseMap("Source albedo",2D)="white"{} _BaseColor("Source tint",Color)=(1,1,1,1)
  _BumpMap("Source normal",2D)="bump"{} _BumpScale("Source normal strength",Float)=1
  _MetallicGlossMap("Baked source metallic and smoothness",2D)="white"{}
  _Metallic("Source metallic",Float)=1 _Smoothness("Source smoothness",Float)=.5
  _OcclusionMap("Source AO",2D)="white"{} _OcclusionStrength("Source AO strength",Float)=1
  _EmissionMap("Source emission",2D)="white"{} _EmissionColor("Source emission times energy",Color)=(0,0,0,1)
  _SpecularFactor("glTF KHR_materials_specular factor",Float)=1
  _SourceDoubleSided("Source two-sided normal flip",Float)=0
  _HasNormalMap("Has normal",Float)=0 _HasMetallicMap("Has metallic",Float)=0
  _HasOcclusionMap("Has AO",Float)=0 _HasEmissionMap("Has emission",Float)=0
  [Enum(UnityEngine.Rendering.CullMode)] _Cull("Source culling",Float)=0
 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Pass
  {
   Name "SourceSpecularForward" Tags{"LightMode"="UniversalForward"} Cull [_Cull]
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
   #define _SPECULAR_SETUP 1
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);TEXTURE2D(_BumpMap);SAMPLER(sampler_BumpMap);
   TEXTURE2D(_MetallicGlossMap);SAMPLER(sampler_MetallicGlossMap);TEXTURE2D(_OcclusionMap);SAMPLER(sampler_OcclusionMap);
   TEXTURE2D(_EmissionMap);SAMPLER(sampler_EmissionMap);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseColor,_EmissionColor;
   float _BumpScale,_Metallic,_Smoothness,_OcclusionStrength,_SpecularFactor;
   float _HasNormalMap,_HasMetallicMap,_HasOcclusionMap,_HasEmissionMap,_SourceDoubleSided;
   CBUFFER_END
   struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V{float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float4 tangentWS:TEXCOORD2;float2 uv:TEXCOORD3;half fog:TEXCOORD4;UNITY_VERTEX_OUTPUT_STEREO};
   V Vert(A a)
   {
    V o=(V)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.positionWS=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);
    o.normalWS=TransformObjectToWorldNormal(a.normalOS);o.tangentWS=float4(TransformObjectToWorldDir(a.tangentOS.xyz),a.tangentOS.w*GetOddNegativeScale());o.uv=a.uv;o.fog=ComputeFogFactor(o.positionCS.z);return o;
   }
   half4 Frag(V i,FRONT_FACE_TYPE facing:FRONT_FACE_SEMANTIC):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    half4 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor;
    half4 packed=SAMPLE_TEXTURE2D(_MetallicGlossMap,sampler_MetallicGlossMap,i.uv);
    half metallic=saturate(_HasMetallicMap>.5?packed.r:_Metallic);
    half smoothness=saturate(_HasMetallicMap>.5?packed.a*_Smoothness:_Smoothness);
    // glTF specular extension changes only the dielectric F0. Transform to
    // URP specular workflow while preserving source diffuse energy exactly.
    half dielectric=.04*saturate(_SpecularFactor);half3 f0=lerp(dielectric.xxx,base.rgb,metallic);
    half remaining=max(1-Max3(f0.r,f0.g,f0.b),.0001);
    SurfaceData s=(SurfaceData)0;s.albedo=base.rgb*(1-metallic)*(1-dielectric)/remaining;s.specular=f0;
    s.metallic=0;s.smoothness=smoothness;s.alpha=base.a;s.normalTS=half3(0,0,1);
    if(_HasNormalMap>.5){half3 n=SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv).xyz*2-1;n.xy*=_BumpScale;n.z=sqrt(max(0,1-dot(n.xy,n.xy)));s.normalTS=normalize(n);}
    float3 geometricNormal=normalize(i.normalWS),tangent=normalize(i.tangentWS.xyz);float3 bitangent=cross(geometricNormal,tangent)*i.tangentWS.w;
    float3 normal=_SourceDoubleSided>.5&&!IS_FRONT_VFACE(facing,true,false)?-geometricNormal:geometricNormal;
    normal=normalize(s.normalTS.x*tangent+s.normalTS.y*bitangent+s.normalTS.z*normal);
    s.occlusion=_HasOcclusionMap>.5?lerp(1,SAMPLE_TEXTURE2D(_OcclusionMap,sampler_OcclusionMap,i.uv).g,_OcclusionStrength):1;
    s.emission=_EmissionColor.rgb*(_HasEmissionMap>.5?SAMPLE_TEXTURE2D(_EmissionMap,sampler_EmissionMap,i.uv).rgb:half3(1,1,1));
    InputData input=(InputData)0;input.positionWS=i.positionWS;input.normalWS=normal;input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    input.shadowCoord=ComputeScreenPos(i.positionCS);
    #else
    input.shadowCoord=TransformWorldToShadowCoord(i.positionWS);
    #endif
    input.fogCoord=i.fog;input.bakedGI=SampleSH(normal);input.vertexLighting=VertexLighting(i.positionWS,normal);input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);input.shadowMask=half4(1,1,1,1);
    half4 result=UniversalFragmentPBR(input,s);result.rgb=MixFog(result.rgb,i.fog);return result;
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}

Shader "Ashfall/Vehicle vertex paint" {
Properties { _BaseColor("Paint",Color)=(1,1,1,1) _EmissionColor("Emission",Color)=(0,0,0,0) }
SubShader { Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" } Pass {
Tags { "LightMode"="UniversalForward" }
HLSLPROGRAM
#pragma vertex Vert
#pragma fragment Frag
#pragma multi_compile_instancing
#pragma multi_compile _ _ADDITIONAL_LIGHTS
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
CBUFFER_START(UnityPerMaterial)
half4 _BaseColor; half4 _EmissionColor;
CBUFFER_END
struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
struct V { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; half4 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
V Vert(A i) { V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionWS=TransformObjectToWorld(i.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.positionWS); o.normalWS=TransformObjectToWorldNormal(i.normalOS); o.color=i.color; return o; }
half4 Frag(V i):SV_Target { UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); half3 n=normalize(i.normalWS); Light l=GetMainLight(); half3 lit=SampleSH(n)+l.color*saturate(dot(n,l.direction));
#ifdef _ADDITIONAL_LIGHTS
uint count=GetAdditionalLightsCount(); for(uint k=0;k<count;k++){Light a=GetAdditionalLight(k,i.positionWS);lit+=a.color*saturate(dot(n,a.direction))*a.distanceAttenuation;}
#endif
return half4(i.color.rgb*_BaseColor.rgb*lit+_EmissionColor.rgb,1); }
ENDHLSL
} } }

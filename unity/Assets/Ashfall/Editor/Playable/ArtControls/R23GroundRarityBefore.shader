Shader "Hidden/Ashfall/Editor/GroundRarityBefore" {
Properties { _RarityColor("Source rarity",Color)=(1,1,1,1) }
SubShader { Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
Pass { Blend One One ZWrite Off ZTest LEqual Cull Back
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_instancing
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
struct Attributes { float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID };
struct Varyings { float4 positionCS:SV_POSITION;float3 normalWS:TEXCOORD0;float3 positionWS:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO };
CBUFFER_START(UnityPerMaterial)
float4 _RarityColor;
CBUFFER_END
Varyings vert(Attributes input){Varyings output;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);output.positionWS=TransformObjectToWorld(input.positionOS.xyz);output.positionCS=TransformWorldToHClip(output.positionWS);output.normalWS=TransformObjectToWorldNormal(input.normalOS);return output;}
half4 frag(Varyings input):SV_Target {UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);float facing=abs(dot(normalize(input.normalWS),normalize(GetWorldSpaceViewDir(input.positionWS))));float rim=1-smoothstep(.12,.34,facing);return half4(_RarityColor.rgb*rim*2.2,1);}
ENDHLSL
} } }

Shader "Ashfall/R24 Kestrel line"
{
 Properties { _Tint("Tint",Color)=(1,.12,.02,.7) _Clearance("Near clearance",Vector)=(.65,1.15,0,0) }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
  Pass
  {
   Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float4 _Tint;float4 _Clearance;
   CBUFFER_END
   struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float2 uv:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
   Varyings Vert(Attributes input){Varyings o;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionWS=TransformObjectToWorld(input.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);o.uv=input.uv;return o;}
   half4 Frag(Varyings input):SV_Target {UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);float distanceToEye=length(input.positionWS-GetCameraPositionWS());float alpha=smoothstep(_Clearance.x,_Clearance.y,distanceToEye);float taper=sqrt(saturate(1-abs(input.uv.y*2-1)));return half4(_Tint.rgb,_Tint.a*alpha*lerp(.6,1,taper));}
   ENDHLSL
  }
 }
}

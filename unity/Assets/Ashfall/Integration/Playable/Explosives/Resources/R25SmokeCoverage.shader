Shader "Ashfall/R25 Smoke Coverage"
{
 Properties { _Density("Density",Range(0,1))=1 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="AlphaTest" }
  Pass
  {
   Tags { "LightMode"="UniversalForward" }
   Cull Back ZWrite On ZTest LEqual AlphaToMask On
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial) half _Density; CBUFFER_END
   struct A { float4 position:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct V { float4 position:SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
   V Vert(A a) { V o=(V)0; UNITY_SETUP_INSTANCE_ID(a); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.position=TransformObjectToHClip(a.position.xyz); return o; }
   half4 Frag(V i):SV_Target { UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); clip(_Density-.001h); return half4(.48h,.49h,.5h,_Density); }
   ENDHLSL
  }
 }
}

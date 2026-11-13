Shader "Ashfall/R24 Transparent Zone Mist"
{
 Properties { _BaseColor("Purple mist", Color)=(.55,.15,.95,.06) }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
  Pass
  {
   Tags { "LightMode"="UniversalForward" }
   Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial) half4 _BaseColor; CBUFFER_END
   struct A { float4 position:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct V { float4 position:SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
   V Vert(A a) { V o=(V)0; UNITY_SETUP_INSTANCE_ID(a); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.position=TransformObjectToHClip(a.position.xyz); return o; }
   half4 Frag(V i):SV_Target { UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); return _BaseColor; }
   ENDHLSL
  }
 }
}

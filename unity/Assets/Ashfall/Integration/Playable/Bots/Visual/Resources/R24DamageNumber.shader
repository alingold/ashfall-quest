Shader "Ashfall/R24DamageNumber"
{
 Properties { _MainTex("Font atlas",2D)="white"{} [HideInInspector] _ZTest("Depth Test",Float)=4 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+50"}
  Pass
  {
   Name "DepthTestedCameraFacingNumber" Tags {"LightMode"="UniversalForward"}
   Cull Off ZTest [_ZTest] ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
   struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
   V Vert(A a){V o=(V)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);float3 center=TransformObjectToWorld(float3(0,0,0));float3 world=center+mul((float3x3)UNITY_MATRIX_I_V,float3(a.positionOS.xy,0));o.positionCS=TransformWorldToHClip(world);o.uv=a.uv;o.color=a.color;return o;}
   half4 Frag(V v):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);return half4(v.color.rgb,v.color.a*SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,v.uv).a);}
   ENDHLSL
  }
 }
}

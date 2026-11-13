Shader "Ashfall/R24 Source Item Surface"
{
 Properties{_MainTex("Source image/font atlas",2D)="white"{} _Color("Source tint",Color)=(1,1,1,1) _Font("Font alpha only",Float)=0}
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
  Pass
  {
   Tags{"LightMode"="UniversalForward"}
   ZTest LEqual ZWrite Off Cull Off Blend SrcAlpha OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial) half4 _Color;float _Font;CBUFFER_END
   struct A{float4 position:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V{float4 position:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
   V Vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.position=TransformObjectToHClip(a.position.xyz);o.uv=a.uv;return o;}
   half4 Frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);return half4((_Font>.5?half3(1,1,1):c.rgb)*_Color.rgb,c.a*_Color.a);}
   ENDHLSL
  }
 }
}

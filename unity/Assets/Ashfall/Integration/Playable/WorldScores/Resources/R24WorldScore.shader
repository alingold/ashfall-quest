Shader "Ashfall/R24 World Score" {
 Properties { _MainTex("Shared source canvas",2D)="white"{} }
 SubShader { Tags {"RenderType"="Transparent" "Queue"="Transparent+100"} Pass {
  Cull Off ZWrite On ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
  HLSLPROGRAM
  #pragma vertex Vert
  #pragma fragment Frag
  #pragma multi_compile_instancing
  #include "UnityCG.cginc"
  sampler2D _MainTex;
  struct A{float4 position:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
  struct V{float4 position:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
  V Vert(A a){V o=(V)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.position=UnityObjectToClipPos(a.position);o.uv=a.uv;return o;}
  half4 Frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);return half4(tex2D(_MainTex,i.uv).rgb,1);}
  ENDHLSL
 }}
}

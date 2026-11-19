Shader "Ashfall/R24 World Score" {
 Properties { _MainTex("Shared source canvas",2D)="white"{} }
 SubShader { Tags {"RenderType"="Transparent" "Queue"="Transparent+100"} Pass {
  Cull Off ZWrite On ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
  HLSLPROGRAM
  #pragma vertex Vert
  #pragma fragment Frag
  #pragma multi_compile_instancing
  #include "UnityCG.cginc"
  sampler2D _MainTex;float _ContentHeight;
  struct A{float4 position:POSITION;float2 uv:TEXCOORD0;float2 fit:TEXCOORD1;UNITY_VERTEX_INPUT_INSTANCE_ID};
  struct V{float4 position:SV_POSITION;float2 uv:TEXCOORD0;float aspect:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
  V Vert(A a){V o=(V)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.position=UnityObjectToClipPos(a.position);o.uv=a.uv;o.aspect=a.fit.x;return o;}
  half4 Frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float2 uv=i.uv;if(i.aspect>0){float h=max(.28,_ContentHeight),aspect=.8/h;uv=(uv-.5)*float2(max(1,i.aspect/aspect),max(1,aspect/i.aspect))+.5;if(any(uv<0)||any(uv>1))return half4(tex2D(_MainTex,float2(.001,.5)).rgb,1);uv.y=1-h+uv.y*h;}if(any(uv<0)||any(uv>1))return half4(tex2D(_MainTex,float2(.001,.5)).rgb,1);return half4(tex2D(_MainTex,uv).rgb,1);}
  ENDHLSL
 }}
}

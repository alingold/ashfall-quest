Shader "Ashfall/R24ZoneDanger" {
 SubShader {Tags {"RenderType"="Transparent" "Queue"="Transparent"} Pass {Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile_instancing
 #include "UnityCG.cginc"
 float4x4 _WorldToGodotMap;float3 _GodotOrigin;float _SourceTime;
 struct A{float4 position:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};struct V{float4 position:SV_POSITION;float3 world:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
 V Vert(A a){V v=(V)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(v);float4 world=mul(unity_ObjectToWorld,a.position);v.world=mul(_WorldToGodotMap,world).xyz+_GodotOrigin;v.position=mul(UNITY_MATRIX_VP,world);return v;}
 half4 Frag(V v):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);return half4(1,.025,.055,.14);}
 ENDHLSL
 }}
}

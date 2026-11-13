Shader "Ashfall/R23ImpactMarks"
{
 Properties { _MainTex("Original fracture",2D)="white"{} _Color("Source fade",Color)=(1,1,1,1) }
 SubShader {Tags {"Queue"="Transparent" "RenderType"="Transparent"}
  Pass {ZTest LEqual ZWrite Off Cull Off Blend SrcAlpha OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #include "UnityCG.cginc"
   sampler2D _MainTex;float4 _Color;
   struct A{float4 position:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V{float4 position:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
   V Vert(A a){V v=(V)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(v);v.position=UnityObjectToClipPos(a.position);v.uv=a.uv;return v;}
   half4 Frag(V v):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);return tex2D(_MainTex,v.uv)*_Color;}
   ENDHLSL
  }
 }
}

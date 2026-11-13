Shader "Ashfall/R23GunFeedback"
{
 Properties { _MainTex("Source font atlas",2D)="white"{} _Color("Source color",Color)=(1,1,1,1) _Font("Font alpha only",Float)=0 _Depth("Depth comparison",Float)=4 _Cull("Cull",Float)=2 }
 SubShader {Tags {"Queue"="Transparent+20" "RenderType"="Transparent"}
  Pass {ZTest [_Depth] ZWrite Off Cull [_Cull] Blend SrcAlpha OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #include "UnityCG.cginc"
   sampler2D _MainTex;float4 _Color;float _Font;
   struct A{float4 position:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V{float4 position:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
   V Vert(A a){V v=(V)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(v);v.position=UnityObjectToClipPos(a.position);v.uv=a.uv;return v;}
   half4 Frag(V v):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);half4 pixel=tex2D(_MainTex,v.uv);pixel.rgb=lerp(pixel.rgb,half3(1,1,1),_Font);return pixel*_Color;}
   ENDHLSL
  }
 }
}

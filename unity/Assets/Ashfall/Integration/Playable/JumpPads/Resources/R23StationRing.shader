Shader "Ashfall/R23StationRing"
{
 Properties {_Recharge("Source INSTANCE_CUSTOM.r",Range(0,1))=0}
 SubShader {Tags {"Queue"="Transparent+200" "RenderType"="Transparent"}
 Pass {Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile_instancing
 #include "UnityCG.cginc"
 float _Recharge;
 struct A{float4 position:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V{float4 position:SV_POSITION;float2 uv:TEXCOORD0;float distanceToEye:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
 V Vert(A a){V v;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(v);v.position=UnityObjectToClipPos(a.position);v.uv=a.uv;float3 center=mul(UNITY_MATRIX_MV,float4(0,0,0,1)).xyz;v.distanceToEye=length(center);return v;}
 half4 Frag(V v):SV_Target
 {
  UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
  float2 p=v.uv*2-1;float radius=length(p);float aa=max(fwidth(radius),.002);
  float band=smoothstep(.76-aa,.76+aa,radius)*(1-smoothstep(.84-aa,.84+aa,radius));clip(band-.01);
  float angle=frac(atan2(p.x,-p.y)/6.2831853+1);float available=1-_Recharge;
  float3 color=_Recharge<.001?float3(.16,1,.42):lerp(float3(.07,.13,.11),float3(1,.65,.08),step(angle,available));
  // Godot source ALBEDO=color, EMISSION=color*.65; source constants linear.
  color*=1.65;
  #ifdef UNITY_COLORSPACE_GAMMA
  color=LinearToGammaSpace(color);
  #endif
  return half4(color,band*.92*(1-smoothstep(55,85,v.distanceToEye)));
 }
 ENDHLSL
 }
 }
}

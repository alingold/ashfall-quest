Shader "Ashfall/R24ZoneSurface" {
 SubShader {Tags {"RenderType"="Opaque" "Queue"="Geometry+100"} Pass {Cull Off ZWrite On ZTest LEqual
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile_instancing
 #pragma multi_compile_fog
 #include "UnityCG.cginc"
 float4x4 _WorldToGodotMap;float4 _CurrentSquare,_NextSquare;
 struct A{float4 position:POSITION;float3 normal:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};struct V{float4 position:SV_POSITION;float3 map:TEXCOORD0;UNITY_FOG_COORDS(1) UNITY_VERTEX_OUTPUT_STEREO};
 V Vert(A a){V v=(V)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(v);float3 world=mul(unity_ObjectToWorld,a.position).xyz;v.map=mul(_WorldToGodotMap,float4(world,1)).xyz;world+=UnityObjectToWorldNormal(a.normal)*.002;v.position=mul(UNITY_MATRIX_VP,float4(world,1));UNITY_TRANSFER_FOG(v,v.position);return v;}
 float Edge(float2 p,float4 r){float2 q=abs(p-(r.xy+r.zw)*.5)-(r.zw-r.xy)*.5;return abs(length(max(q,float2(0,0)))+min(max(q.x,q.y),0));}
 half4 Frag(V v):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);float red=Edge(v.map.xz,_CurrentSquare),white=Edge(v.map.xz,_NextSquare);if(red>.60&&white>.1524)discard;float3 color=red<=.60?float3(1,.012,.055):float3(.92,.98,1);half4 result=half4(color*(red<=.60?3.8:3.0),1);UNITY_APPLY_FOG(v.fogCoord,result);return result;}
 ENDHLSL
 }}
}

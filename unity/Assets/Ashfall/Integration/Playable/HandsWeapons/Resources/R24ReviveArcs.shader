Shader "Ashfall/R24ReviveArcs" { SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent"} Pass { ZWrite Off Cull Off Blend SrcAlpha OneMinusSrcAlpha
HLSLPROGRAM
#pragma vertex Vert
#pragma fragment Frag
#pragma multi_compile_instancing
#include "UnityCG.cginc"
struct A { float4 vertex:POSITION;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID };struct V {float4 position:SV_POSITION;float4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
V Vert(A a){V v=(V)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(v);v.position=UnityObjectToClipPos(a.vertex);v.color=a.color;return v;}half4 Frag(V v):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);return v.color;}
ENDHLSL
} } }

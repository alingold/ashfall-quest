Shader "Ashfall/R24KatanaGuard" {Properties {_FieldColor("Original field color",Color)=(.055,.94,.22,1)} SubShader {Tags {"Queue"="Transparent" "RenderType"="Transparent"} Pass {ZWrite Off Cull Off Blend SrcAlpha OneMinusSrcAlpha
HLSLPROGRAM
#pragma vertex Vert
#pragma fragment Frag
#pragma multi_compile_instancing
#include "UnityCG.cginc"
float4 _FieldColor;struct A{float4 vertex:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};struct V{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};V Vert(A a){V v=(V)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(v);v.pos=UnityObjectToClipPos(a.vertex);v.uv=a.uv;return v;}
float segment_distance(float2 p,float2 a,float2 b){float2 d=b-a;return length(p-a-d*saturate(dot(p-a,d)/dot(d,d)));}
half4 Frag(V v):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);float2 p=v.uv*2-1,q=abs(p*float2(.8,1));float frame=segment_distance(q,float2(.768,0),float2(.768,.83));frame=min(frame,segment_distance(q,float2(.768,.83),float2(.672,.96)));frame=min(frame,segment_distance(q,float2(.672,.96),float2(.344,.96)));frame=min(frame,segment_distance(q,float2(.344,.96),float2(.240,.84)));frame=min(frame,segment_distance(q,float2(.240,.84),float2(0,.84)));float core=1-smoothstep(.004,.013,frame);float halo=(1-smoothstep(.014,.049,frame))*.19;float trace=segment_distance(q,float2(.701,.29),float2(.701,.69));trace=min(trace,segment_distance(q,float2(.701,.69),float2(.625,.79)));trace=min(trace,segment_distance(q,float2(.625,.79),float2(.386,.79)));float inner=(1-smoothstep(.001,.0035,trace))*.32;float badge_half_width=lerp(.135,.188,smoothstep(.719,.759,q.y));float badge=(1-smoothstep(badge_half_width-.003,badge_half_width+.003,q.x))*smoothstep(.715,.722,q.y)*(1-smoothstep(.756,.762,q.y));float boundary=(1-smoothstep(.001,.005,min(1-abs(p.x),1-abs(p.y))))*.09;float detail=max(core*.79+halo,max(inner,badge*.43));return half4(_FieldColor.rgb*(1+core*.35),clamp(.09+max(detail,boundary),0,.93));}
ENDHLSL
} } }

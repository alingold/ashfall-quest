Shader "Ashfall/R25LobbyEmbers" {
 SubShader { Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
 Pass { Cull Off
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct A {float3 p:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
 V Vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
 float3 p=TransformObjectToWorld(a.p);float seed=frac(sin(a.p.x*7+a.p.z)*413.7);
 p.y=frac((a.p.y+_Time.y*.12)/6)*6;p.x+=sin(_Time.y*.16+seed*20)*.22;
 float size=.009+seed*.012;
 p+=UNITY_MATRIX_I_V._m00_m10_m20*a.uv.x*size+UNITY_MATRIX_I_V._m01_m11_m21*a.uv.y*size;
 o.p=TransformWorldToHClip(p);o.uv=a.uv;return o;}
 half4 Frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);clip(1-dot(i.uv,i.uv));return half4(1,.33,.08,1);}
 ENDHLSL
 } } }

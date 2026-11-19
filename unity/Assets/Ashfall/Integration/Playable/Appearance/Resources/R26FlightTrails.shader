Shader "Ashfall/R26FlightTrails"
{
    Properties { _Core("Emissive core",Color)=(1.8,3.2,3.5,1) _Glow("Cyan",Color)=(.02,1.25,2,1) _Edge("Daylight edge",Color)=(.008,.035,.09,1) _Bands("Core and glow edges",Vector)=(.28,.72,.9,0) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest LEqual Cull Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Core,_Glow,_Edge;float4 _Bands;
            CBUFFER_END
            struct A {float4 vertex:POSITION;float3 tangent:NORMAL;float2 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 position:SV_POSITION;float across:TEXCOORD0;half alpha:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
            V Vert(A a)
            {
                V v=(V)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(v);
                float3 p=TransformObjectToWorld(a.vertex.xyz),t=TransformObjectToWorldDir(a.tangent);
                // Per-eye billboard keeps the ribbon broad in peripheral and remote views.
                float3 side=cross(t,GetWorldSpaceNormalizeViewDir(p));
                if(dot(side,side)<.00001)side=cross(t,float3(0,1,0));
                if(dot(side,side)<.00001)side=float3(1,0,0);
                p+=normalize(side)*a.uv.x*a.uv.y;v.position=TransformWorldToHClip(p);v.across=a.uv.x;v.alpha=a.color.a;return v;
            }
            half4 Frag(V v):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
                float x=abs(v.across);
                half3 rgb=lerp(_Core.rgb,_Glow.rgb,smoothstep(0,_Bands.x,x));
                rgb=lerp(rgb,_Edge.rgb,smoothstep(_Bands.x,_Bands.y,x));
                return half4(rgb,v.alpha*(1-smoothstep(_Bands.z,1,x)));
            }
            ENDHLSL
        }
    }
}

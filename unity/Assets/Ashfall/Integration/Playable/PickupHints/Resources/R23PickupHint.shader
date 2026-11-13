Shader "Ashfall/R23 Source Pickup Hint"
{
    Properties
    {
        _MainTex("Source font atlas",2D)="white"{}
        _Color("Source hand tint",Color)=(1,1,1,1)
        _Font("Font alpha",Float)=0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source blend",Float)=1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination blend",Float)=0
        [Toggle] _ZWrite("Depth write",Float)=1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull",Float)=2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZTest LEqual ZWrite [_ZWrite] Cull [_Cull] Blend [_SrcBlend] [_DstBlend]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;float _Font;
            CBUFFER_END
            struct A{float4 position:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V{float4 position:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
            V Vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.position=TransformObjectToHClip(a.position.xyz);o.uv=a.uv;return o;}
            half4 Frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);half alpha=_Font>.5?SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a:1;return half4(_Color.rgb,_Color.a*alpha);}
            ENDHLSL
        }
    }
}

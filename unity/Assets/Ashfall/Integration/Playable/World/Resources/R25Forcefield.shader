Shader "Ashfall/R25Forcefield"
{
    Properties
    {
        _BaseMap("Authored blue lattice", 2D) = "white" {}
        _ScrollSpeed("Horizontal speed metres per second", Float) = 0.2
        _RepeatMetres("Texture repeat metres", Float) = 12
        _CaptureTime("Capture clock, negative uses game time", Float) = -1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float _ScrollSpeed, _RepeatMetres, _CaptureTime;
            CBUFFER_END
            struct A { float4 position:POSITION; float3 normal:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 position:SV_POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A a)
            {
                V o=(V)0;
                UNITY_SETUP_INSTANCE_ID(a);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 world=TransformObjectToWorld(a.position.xyz);
                float3 n=TransformObjectToWorldNormal(a.normal);
                // Wall tangent is horizontal and measured in world metres, independent of mesh UV/scale.
                float2 tangent=normalize(float2(n.z,-n.x)+float2(0.000001,0));
                float clock=_CaptureTime<0?_Time.y:_CaptureTime;
                o.uv=float2(dot(world.xz,tangent)-clock*_ScrollSpeed,world.y)/_RepeatMetres;
                o.position=TransformWorldToHClip(world);
                return o;
            }
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 lattice=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);
                // Original cyan lines become translucent; filled blue cells supply a steady haze.
                half latticeLine=saturate((lattice.g-0.35h)*2.0h);
                return half4(lerp(half3(0.025h,0.25h,0.8h),half3(0.06h,0.85h,1.0h),latticeLine),0.22h+0.30h*latticeLine);
            }
            ENDHLSL
        }
    }
}

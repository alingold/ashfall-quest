Shader "Ashfall/R23 Original Archive Glass"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #define _SPECULAR_SETUP 1
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "../../Resources/R25Style.hlsl"
            struct A{float4 position:POSITION;float3 normal:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V{float4 position:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
            V Vert(A a){V o=(V)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.position=TransformObjectToHClip(a.position.xyz);o.world=TransformObjectToWorld(a.position.xyz);o.normal=TransformObjectToWorldNormal(a.normal);return o;}
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n=normalize(i.normal),v=GetWorldSpaceNormalizeViewDir(i.world);float edge=pow(1-abs(dot(n,v)),3);
                n=dot(n,v)<0?-n:n;
                SurfaceData surface=(SurfaceData)0;surface.albedo=R25StyleAlbedo(half3(.28,.48,.56));surface.alpha=.07+edge*.14;
                surface.specular=half3(.044,.044,.044); // Godot F0 = .08 * source SPECULAR .55
                surface.smoothness=.84;surface.occlusion=1;surface.normalTS=half3(0,0,1);
                InputData input=(InputData)0;input.positionWS=i.world;input.normalWS=n;input.viewDirectionWS=v;
                input.shadowCoord=TransformWorldToShadowCoord(i.world);input.bakedGI=SampleSH(n);input.vertexLighting=VertexLighting(i.world,n);input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.position);input.shadowMask=half4(1,1,1,1);
                half4 result=UniversalFragmentPBR(input,surface);
                Light mainLight=GetMainLight();result.rgb=R25StyleFinish(result.rgb,saturate(dot(n,mainLight.direction)),i.world);return result;
            }
            ENDHLSL
        }
    }
}

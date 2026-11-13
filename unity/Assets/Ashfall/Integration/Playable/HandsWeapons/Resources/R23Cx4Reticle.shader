Shader "Ashfall/R23Cx4Reticle"
{
    Properties { _AimTarget("Bore target", Vector) = (0,0,100,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _AimTarget;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 planeOrigin : TEXCOORD1;
                float3 planeNormal : TEXCOORD2;
                float2 circle : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input) {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.planeOrigin=TransformObjectToWorld(float3(0,0,0));
                output.circle=input.positionOS.xy*2;
                output.planeNormal=TransformObjectToWorldDir(float3(0,0,1));
                return output;
            }
            half4 Frag(Varyings input) : SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if(length(input.circle)>.88)discard;
                float3 eye=GetCameraPositionWS();
                float3 ray=_AimTarget.xyz-eye;
                float divisor=dot(ray,input.planeNormal);
                if(abs(divisor)<.00001)return half4(0,0,0,0);
                float travel=dot(input.planeOrigin-eye,input.planeNormal)/divisor;
                float3 projection=eye+ray*travel;
                if(travel>0&&travel<1&&distance(projection,input.positionWS)<.0007)return half4(1,.19,.045,1);
                return half4(0,0,0,0);
            }
            ENDHLSL
        }
    }
}

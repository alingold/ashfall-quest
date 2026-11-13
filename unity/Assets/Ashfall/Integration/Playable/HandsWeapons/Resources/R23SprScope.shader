Shader "Ashfall/R23SprScope"
{
    Properties { _OpticalLeft("Source left optical view",2D)="black"{} _OpticalRight("Source right optical view",2D)="black"{} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_OpticalLeft); SAMPLER(sampler_OpticalLeft);
            TEXTURE2D(_OpticalRight); SAMPLER(sampler_OpticalRight);
            CBUFFER_START(UnityPerMaterial)
                float4 _OpticRight,_OpticUp,_OpticForward,_ReticleCenter,_ReticleHalfExtent;
                float4 _TangentLeft,_TangentRight,_AimLeft,_AimRight;
                float4 _OpticalLeft_TexelSize,_OpticalRight_TexelSize;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float2 uv:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input) {
                Varyings output;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);output.positionCS=TransformWorldToHClip(output.positionWS);output.uv=input.uv;return output;
            }
            half4 Frag(Varyings input):SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                int index=0;
                #if defined(USING_STEREO_MATRICES)
                    index=unity_StereoEyeIndex;
                #endif
                float3 eye=GetCameraPositionWS(),ray=input.positionWS-eye;
                float depth=dot(ray,_OpticForward.xyz);
                float2 angular=float2(dot(ray,_OpticRight.xyz),dot(ray,_OpticUp.xyz))/max(.0001,depth);
                float2 extent=index==1?_TangentRight.xy:_TangentLeft.xy;
                float3 virtualRay=_ReticleCenter.xyz-eye;
                float2 reticleAngle=float2(dot(virtualRay,_OpticRight.xyz),dot(virtualRay,_OpticUp.xyz))/max(.0001,dot(virtualRay,_OpticForward.xyz));
                float2 targetAngle=index==1?_AimRight.xy:_AimLeft.xy;
                float2 p=((angular-reticleAngle)/2+targetAngle)/extent;
                float2 uv=p*.5+.5;
                // Unity render targets can invert Y on top-origin graphics APIs.
                // Use the actual camera texture sign, not a guessed platform rotation.
                #if UNITY_UV_STARTS_AT_TOP
                    if((index==1?_OpticalRight_TexelSize.y:_OpticalLeft_TexelSize.y)<0)uv.y=1-uv.y;
                #endif
                half3 picture=index==1?SAMPLE_TEXTURE2D(_OpticalRight,sampler_OpticalRight,uv).rgb:SAMPLE_TEXTURE2D(_OpticalLeft,sampler_OpticalLeft,uv).rgb;
                if(depth<=0||any(abs(p)>1))picture=0;
                float planeDepth=dot(_ReticleCenter.xyz-eye,_OpticForward.xyz);
                float3 offset=eye+ray*(planeDepth/max(depth,.0001))-_ReticleCenter.xyz;
                float2 aim=float2(dot(offset,_OpticRight.xyz),dot(offset,_OpticUp.xyz))/_ReticleHalfExtent.xy;
                float radius=length(aim*float2(520.0/307.0,1));
                float ring=1-smoothstep(.004,.009,abs(radius-.14));float dotMark=1-smoothstep(.006,.011,radius);
                float edge=1-smoothstep(.64,1,max(abs(input.uv.x*2-1),abs(input.uv.y*2-1)));
                return half4(lerp(picture*edge,half3(.94,.055,.10),max(ring,dotMark)*edge),1);
            }
            ENDHLSL
        }
    }
}

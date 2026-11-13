// Translation of frozen R6 scripts/awp_scope.gd angular compositor.
Shader "Ashfall/R24AwpScope" {
Properties { _OpticalView("Source shared binocular view",2D)="black"{} }
SubShader {Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Overlay+900" "RenderType"="Transparent"}
Pass { ZTest Always ZWrite Off Cull Off Blend One Zero
HLSLPROGRAM
#pragma target 3.5
#pragma vertex Vert
#pragma fragment Frag
#pragma multi_compile_instancing
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
TEXTURE2D(_OpticalView);SAMPLER(sampler_OpticalView);
CBUFFER_START(UnityPerMaterial)
float4 _OpticalView_TexelSize,_ViewRight;float _AngularScale;
CBUFFER_END
struct A {float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
struct V {float4 positionCS:SV_POSITION;float2 clipXY:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
V Vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.clipXY=a.positionOS.xy*2;o.positionCS=float4(o.clipXY,1,1);return o;}
half4 Frag(V i):SV_Target {
 UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
 // Unity's per-eye inverse projection already has an eye-centered origin;
 // unlike Godot combined-camera EYE_OFFSET, do not subtract the IPD twice.
 float4 unproject=mul(UNITY_MATRIX_I_P,float4(i.clipXY,1,1));
 float3 localRay=normalize(unproject.xyz/unproject.w);
 float2 aperture=localRay.xy/max(.0001,-localRay.z)/_AngularScale;
 float3 forward=normalize(mul((float3x3)UNITY_MATRIX_I_V,float3(0,0,-1)));
 float3 right=cross(float3(0,1,0),forward);
 if(dot(right,right)<.00001)right=_ViewRight.xyz-forward*dot(_ViewRight.xyz,forward);
 right=normalize(right);if(dot(right,_ViewRight.xyz)<0)right=-right;
 float3 up=normalize(cross(forward,right));
 float3 ray=normalize(mul((float3x3)UNITY_MATRIX_I_V,localRay));
 float2 p=float2(dot(ray,right),dot(ray,up))/max(.0001,dot(ray,forward))/_AngularScale;
 float r=length(aperture);float2 uv=p*.5+.5;
 #if UNITY_UV_STARTS_AT_TOP
 if(_OpticalView_TexelSize.y<0)uv.y=1-uv.y;
 #endif
 half3 colour=SAMPLE_TEXTURE2D(_OpticalView,sampler_OpticalView,uv).rgb;
 float edge=1-smoothstep(.96,1,r);
 float crosshair=max(step(abs(p.y),.004)*step(abs(p.x),.90),step(abs(p.x),.004)*step(abs(p.y),.90));
 return half4(r>=1?half3(0,0,0):lerp(colour*edge,half3(.002,.002,.002),crosshair),1);
}
ENDHLSL
}}
}

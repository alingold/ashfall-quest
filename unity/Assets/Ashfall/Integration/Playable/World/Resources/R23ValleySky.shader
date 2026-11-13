Shader "Ashfall/R23ValleySky"
{
 Properties { _Panorama("Original linear HDR panorama",2D)="black"{} _Energy("Source sky energy",Float)=.4 _FogColor("Source fog color",Color)=(.48,.61,.69,1) _SkyFog("Source sky fog affect",Float)=.18 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"}
  Pass {
   Cull Off ZWrite Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma target 3.0
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
   TEXTURE2D(_Panorama);SAMPLER(sampler_Panorama);
   CBUFFER_START(UnityPerMaterial)
    float _Energy,_SkyFog;float4 _FogColor;float4x4 _WorldToSky;
   CBUFFER_END
   struct Attributes{float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct Varyings{float4 positionCS:SV_POSITION;float3 direction:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
   Varyings Vert(Attributes a){Varyings o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.direction=a.positionOS.xyz;return o;}
   float4 Frag(Varyings i):SV_Target {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float3 direction=normalize(mul((float3x3)_WorldToSky,i.direction));
    // Pinned Godot4.7.2 sky.glsl SKY_COORDS: atan2(g.x,-g.z),acos(g.y).
    // g=reflectZ(Unity): atan2(u.x,u.z). Raw row0 is sampled at v0;
    // do not flip the preserved source image or add an arbitrary sky yaw.
    float2 uv=float2(frac(atan2(direction.x,direction.z)/(2*PI)+1),acos(clamp(direction.y,-1,1))/PI);
    float2 dx=ddx(uv),dy=ddy(uv);dx.x-=round(dx.x);dy.x-=round(dy.x);
    float3 color=SAMPLE_TEXTURE2D_GRAD(_Panorama,sampler_Panorama,uv,dx,dy).rgb*_Energy;
    float3 fog=_FogColor.rgb;
    #ifndef UNITY_COLORSPACE_GAMMA
     fog=SRGBToLinear(fog);
    #endif
    color=lerp(color,fog,saturate(_SkyFog));
    #ifdef UNITY_COLORSPACE_GAMMA
     color=LinearToSRGB(color);
    #endif
    // Keep HDR output. Source filmic2 is a scene-wide postprocess, not baked
    // into this sky; applying a sky-only curve would disagree with the world.
    return float4(color,1);
   }
   ENDHLSL
  }
 }
}

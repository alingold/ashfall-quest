Shader "Ashfall/R23ValleySky"
{
 Properties
 {
  _Panorama("Original linear HDR panorama",2D)="black"{}
  _Energy("Source sky energy",Float)=.4
  _FogColor("Source fog color",Color)=(.48,.61,.69,1)
  _SkyFog("Source sky fog affect",Float)=.18
  _AtmosphereStrength("Atmosphere tint",Range(0,1))=.16
  _CloudStrength("Cloud and ash veil",Range(0,1))=.16
  _HorizonHaze("Horizon haze",Range(0,1))=1
  _SunGlow("Sun intensity",Range(0,2))=.35
  _SunRadius("Sun angular radius in degrees",Range(.1,2))=.55
  _DitherStrength("Gradient dither",Range(0,2))=1
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"}
  Pass {
   Cull Off ZWrite Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma target 3.5
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
   TEXTURE2D(_Panorama);SAMPLER(sampler_Panorama);
   CBUFFER_START(UnityPerMaterial)
    float _Energy,_SkyFog;float4 _FogColor;float4x4 _WorldToSky;
    float _AtmosphereStrength,_CloudStrength,_HorizonHaze,_SunGlow,_SunRadius,_DitherStrength;
   CBUFFER_END
   struct Attributes{float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct Varyings{float4 positionCS:SV_POSITION;float3 direction:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
   Varyings Vert(Attributes a){Varyings o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.direction=a.positionOS.xyz;return o;}
   // Four lattice corners at once: no sine, texture, or dynamic loop.
   float SkyNoise(float2 p)
   {
    float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
    float4 h=frac((cell.x+cell.y*57+float4(0,1,57,58))*.1031);
    h*=h+33.33;h=frac(h*(h+h));
    return lerp(lerp(h.x,h.y,f.x),lerp(h.z,h.w,f.x),f.y);
   }
   float4 Frag(Varyings i):SV_Target {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float3 worldDirection=normalize(i.direction);
    float3 direction=normalize(mul((float3x3)_WorldToSky,worldDirection));
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
    float height=worldDirection.y;
    // Broad ash, cool middle sky, and blue zenith; retain the HDR photograph.
    float3 atmosphere=lerp(fog*float3(.70,.72,.78),fog,smoothstep(-.35,0,height));
    atmosphere=lerp(atmosphere,fog*float3(.74,.94,1.12),smoothstep(.02,.32,height));
    atmosphere=lerp(atmosphere,fog*float3(.38,.61,.94),smoothstep(.25,.92,height));
    color=lerp(color,atmosphere,saturate(_AtmosphereStrength));
    // Direction-space coordinates have no longitude seam. Static low-frequency
    // detail avoids animated shimmer and fades before the polar projection folds.
    float2 cloudUV=direction.xz*float2(7,9)+direction.y*float2(3,18);
    float noise=SkyNoise(cloudUV)*.65+SkyNoise(cloudUV*2.07+13.7)*.35;
    float cloudBand=smoothstep(-.03,.10,height)*(1-smoothstep(.36,.72,height));
    float cloud=smoothstep(.42,.76,noise)*cloudBand*saturate(_CloudStrength);
    color=lerp(color,fog*.92,cloud);
    // Use URP's existing main directional light, so the disk follows the key
    // light without another C# binding. A missing light contributes no radiance.
    float3 sunVector=_MainLightPosition.xyz;
    float3 sunDirection=sunVector*rsqrt(max(dot(sunVector,sunVector),1e-6));
    float sunDistance=1-saturate(dot(worldDirection,sunDirection));
    float radius=max(_SunRadius,.1)*(PI/180);
    float diskEdge=.5*radius*radius;
    float edgeWidth=max(fwidth(sunDistance),2e-6);
    float disk=1-smoothstep(diskEdge-edgeWidth,diskEdge+edgeWidth,sunDistance);
    float halo=rcp(1+sunDistance*1800);halo*=halo;
    float aureole=saturate(1-sunDistance*12);aureole*=aureole;aureole*=aureole;
    color+=_MainLightColor.rgb*(1-saturate(_MainLightPosition.w))*max(_SunGlow,0)
        *(disk*1.8+halo*.55+aureole*.08)*(1-cloud*.65);
    color=lerp(color,fog,saturate(_SkyFog));
    float haze=(1-smoothstep(0,.14,abs(height)))*saturate(_HorizonHaze)*saturate(_SkyFog*4);
    color=lerp(color,fog,haze);
    #ifdef UNITY_COLORSPACE_GAMMA
     color=LinearToSRGB(color);
    #endif
    // Stationary interleaved noise: roughly one display code, scaled for a
    // linear render target. Do not clamp the upper end of the HDR panorama.
    float dither=frac(52.9829189*frac(dot(i.positionCS.xy,float2(.06711056,.00583715))))-.5;
    float ditherStep=1.0/255;
    #ifndef UNITY_COLORSPACE_GAMMA
     ditherStep=max(.0003,.009*sqrt(saturate(dot(color,float3(.2126,.7152,.0722)))));
    #endif
    color=max(color+dither*ditherStep*_DitherStrength,0);
    // Keep HDR output. Source filmic2 is a scene-wide postprocess, not baked
    // into this sky; applying a sky-only curve would disagree with the world.
    return float4(color,1);
   }
   ENDHLSL
  }
 }
}

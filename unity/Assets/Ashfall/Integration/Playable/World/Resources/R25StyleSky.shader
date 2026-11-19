Shader "Ashfall/R25StyleSky"
{
 Properties
 {
  _Zenith("Zenith",Color)=(0,0,0,1)
  _Horizon("Horizon",Color)=(0,0,0,1)
  _Nadir("Nadir",Color)=(0,0,0,1)
  _ToSun("Direction to sun",Vector)=(0,1,0,0)
  _SunColor("Sun colour",Color)=(1,1,1,1)
  _SunGlow("Glow",Float)=0
  _CloudStrength("Cloud and ash veil",Range(0,1))=.18
  _HorizonHaze("Horizon haze",Range(0,1))=1
  _SunRadius("Sun angular radius in degrees",Range(.1,2))=.55
  _DitherStrength("Gradient dither",Range(0,2))=1
 }
 SubShader
 {
  Tags {"Queue"="Background" "RenderType"="Background" "RenderPipeline"="UniversalPipeline" "PreviewType"="Skybox"}
  Pass
  {
   Cull Off ZWrite Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma target 3.5
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float4 _Zenith,_Horizon,_Nadir,_ToSun,_SunColor;
   float _SunGlow;
   float _CloudStrength,_HorizonHaze,_SunRadius,_DitherStrength;
   CBUFFER_END
   // Match the manual style fog when active, otherwise Unity's scene fog.
   // These remain globals; a material property would mask the C# bindings.
   float _R25StyleEnabled;
   float3 _R25FogColor;
   struct A {float4 position:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 position:SV_POSITION;float3 direction:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
   V Vert(A a)
   {
    V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.position=TransformObjectToHClip(a.position.xyz);o.direction=a.position.xyz;return o;
   }
   float SkyNoise(float2 p)
   {
    float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
    float4 h=frac((cell.x+cell.y*57+float4(0,1,57,58))*.1031);
    h*=h+33.33;h=frac(h*(h+h));
    return lerp(lerp(h.x,h.y,f.x),lerp(h.z,h.w,f.x),f.y);
   }
   half4 Frag(V i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float3 d=normalize(i.direction);
    // Four smooth altitude stops, derived from the existing authored palette.
    // Smoothstep removes the old pow curve's sharp slope at the horizon.
    float3 middle=lerp(_Horizon.rgb,_Zenith.rgb,.58);
    float3 sky=lerp(_Nadir.rgb,_Horizon.rgb,smoothstep(-.42,0,d.y));
    sky=lerp(sky,middle,smoothstep(0,.32,d.y));
    sky=lerp(sky,_Zenith.rgb,smoothstep(.18,.92,d.y));
    // Two static octaves, no texture fetches or longitude/pole discontinuity.
    float2 cloudUV=d.xz*float2(7,9)+d.y*float2(3,18);
    float noise=SkyNoise(cloudUV)*.65+SkyNoise(cloudUV*2.07+13.7)*.35;
    float cloudBand=smoothstep(-.03,.10,d.y)*(1-smoothstep(.36,.72,d.y));
    float cloud=smoothstep(.42,.76,noise)*cloudBand*saturate(_CloudStrength);
    sky=lerp(sky,lerp(_Horizon.rgb,_Zenith.rgb,.2),cloud);
    // Keep angular calculations in float: half precision loses a small disk.
    float3 sunDirection=_ToSun.xyz*rsqrt(max(dot(_ToSun.xyz,_ToSun.xyz),1e-6));
    float sunDistance=1-saturate(dot(d,sunDirection));
    float radius=max(_SunRadius,.1)*(PI/180);
    float diskEdge=.5*radius*radius;
    float edgeWidth=max(fwidth(sunDistance),2e-6);
    float disk=1-smoothstep(diskEdge-edgeWidth,diskEdge+edgeWidth,sunDistance);
    float halo=rcp(1+sunDistance*1800);halo*=halo;
    float aureole=saturate(1-sunDistance*12);aureole*=aureole;aureole*=aureole;
    sky+=_SunColor.rgb*max(_SunGlow,0)*(disk*1.8+halo*.55+aureole*.08)*(1-cloud*.65);
    float3 fog=lerp(unity_FogColor.rgb,_R25FogColor,saturate(_R25StyleEnabled));
    float haze=(1-smoothstep(0,.14,abs(d.y)))*saturate(_HorizonHaze);
    sky=lerp(sky,fog,haze);
    // A fixed sub-code grain breaks display banding without time-based sparkle.
    float dither=frac(52.9829189*frac(dot(i.position.xy,float2(.06711056,.00583715))))-.5;
    float ditherStep=1.0/255;
    #ifndef UNITY_COLORSPACE_GAMMA
     ditherStep=max(.0003,.009*sqrt(saturate(dot(sky,float3(.2126,.7152,.0722)))));
    #endif
    sky=max(sky+dither*ditherStep*_DitherStrength,0);
    return half4(sky,1);
   }
   ENDHLSL
  }
 }
}

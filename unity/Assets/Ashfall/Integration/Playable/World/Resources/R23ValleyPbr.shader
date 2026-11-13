Shader "Ashfall/R23ValleyPbr"
{
 Properties
 {
  _BaseMap("Source albedo",2D)="white"{}
  _NormalMap("Source normal",2D)="bump"{}
  _RoughnessMap("Source roughness",2D)="white"{}
  _MetallicMap("Source metallic",2D)="white"{}
  _OcclusionMap("Source AO",2D)="white"{}
  _EmissionMap("Source emission",2D)="white"{}
  _Tint("Source tint",Color)=(1,1,1,1)
  _EmissionColor("Source emission color",Color)=(0,0,0,1)
  _EmissionEnergy("Source emission energy",Float)=0
  _EmissionHasMap("Source emission texture present",Float)=0
  _EmissionOperator("Source emission operator",Int)=0
  _NormalStrength("Source normal scale",Float)=0
  _Metallic("Source metallic",Range(0,1))=0
  _Roughness("Source roughness",Range(0,1))=.7
  _UvScale("Source UV scale",Vector)=(1,1,1,0)
  _UvOffset("Source UV offset",Vector)=(0,0,0,0)
  _Triplanar("Source triplanar",Float)=0
  _WorldTriplanar("Source world triplanar",Float)=0
  _TriplanarSharpness("Source triplanar sharpness",Float)=1
  _VertexColor("Source vertex color",Float)=0
  _VertexColorSrgb("Source vertex color sRGB",Float)=0
  _Unshaded("Source unshaded",Float)=0
  _RoughnessChannel("Source roughness channel",Int)=0
  _MetallicChannel("Source metallic channel",Int)=0
  _OcclusionChannel("Source AO channel",Int)=0
  _Cutoff("Source alpha scissor",Float)=0
  [Enum(UnityEngine.Rendering.CullMode)] _Cull("Source cull",Float)=2
  [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source blend source",Float)=1
  [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Source blend destination",Float)=0
  _ZWrite("Source depth write",Float)=1
 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
  Pass
  {
   Name "SourceStandardForward"
   Tags{"LightMode"="UniversalForward"}
   Cull [_Cull] Blend [_SrcBlend] [_DstBlend] ZWrite [_ZWrite]
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #pragma multi_compile_fog
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
   #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
   TEXTURE2D(_NormalMap);SAMPLER(sampler_NormalMap);
   TEXTURE2D(_RoughnessMap);SAMPLER(sampler_RoughnessMap);
   TEXTURE2D(_MetallicMap);SAMPLER(sampler_MetallicMap);
   TEXTURE2D(_OcclusionMap);SAMPLER(sampler_OcclusionMap);
   TEXTURE2D(_EmissionMap);SAMPLER(sampler_EmissionMap);
   CBUFFER_START(UnityPerMaterial)
   float4 _Tint,_EmissionColor,_UvScale,_UvOffset;
   float _EmissionEnergy,_EmissionHasMap,_NormalStrength,_Metallic,_Roughness,_Triplanar,_WorldTriplanar,_TriplanarSharpness;
   float _VertexColor,_VertexColorSrgb,_Unshaded,_Cutoff;
   int _RoughnessChannel,_MetallicChannel,_OcclusionChannel,_EmissionOperator;
   CBUFFER_END
   struct Attributes
   {
    float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;float4 color:COLOR;
    float3 sourcePosition:TEXCOORD1;float3 sourceNormal:TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
   };
   struct Varyings
   {
    float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float2 uv:TEXCOORD2;
    float3 sourcePosition:TEXCOORD3;float3 sourceNormal:TEXCOORD4;float4 color:COLOR;float fog:TEXCOORD5;
    float3 weights:TEXCOORD6;float3 triTangent:TEXCOORD7;float3 triBinormal:TEXCOORD8;
    UNITY_VERTEX_OUTPUT_STEREO
   };
   Varyings Vert(Attributes a)
   {
    Varyings o=(Varyings)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.positionWS=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);
    o.normalWS=TransformObjectToWorldNormal(a.normalOS);o.uv=a.uv;o.color=a.color;
    float3 sourceN=_WorldTriplanar>.5?float3(o.normalWS.x,o.normalWS.y,-o.normalWS.z):a.sourceNormal;
    float3 magnitude=abs(sourceN);o.weights=pow(magnitude,max(.001,_TriplanarSharpness));o.weights/=max(dot(o.weights,1),1e-8);
    // Source StandardMaterial builds these axes before interpolation. Reflect
    // their Z once; world-triplanar axes are independent of each mesh instance.
    o.triTangent=normalize(float3(magnitude.y+magnitude.z,0,magnitude.x));
    o.triBinormal=normalize(float3(0,magnitude.x+magnitude.z,magnitude.y));
    o.sourcePosition=a.sourcePosition;o.sourceNormal=a.sourceNormal;o.fog=ComputeFogFactor(o.positionCS.z);return o;
   }
   // Godot textures use its source UV convention; importer already flipped UV0 V.
   // Apply source UV scale/offset before the equivalent single sampling V flip.
   float2 SourceUv(float2 uv){return float2(uv.x*_UvScale.x+_UvOffset.x,uv.y*_UvScale.y+1-_UvScale.y-_UvOffset.y);}
   float2 ImageUv(float2 uv){return float2(uv.x,1-uv.y);}
   float4 SourceTexture(TEXTURE2D_PARAM(tex,samp),float2 uv,float3 position,float3 weights)
   {
    float4 color=0;
    if(_Triplanar<.5)color=SAMPLE_TEXTURE2D(tex,samp,SourceUv(uv));
    else color=SAMPLE_TEXTURE2D(tex,samp,ImageUv(position.xy))*weights.z
          +SAMPLE_TEXTURE2D(tex,samp,ImageUv(position.xz))*weights.y
          +SAMPLE_TEXTURE2D(tex,samp,ImageUv(position.zy*float2(-1,1)))*weights.x;
    return color;
   }
   float Channel(float4 value,int channel)
   {if(channel==4)return dot(value.rgb,float3(.333333333,.333333333,.333333333));return channel==1?value.g:channel==2?value.b:channel==3?value.a:value.r;}
   half4 Frag(Varyings i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float3 n=normalize(i.normalWS);
    float3 sourceN=_WorldTriplanar>.5?float3(n.x,n.y,-n.z):normalize(i.sourceNormal);
    float3 position=_WorldTriplanar>.5?float3(i.positionWS.x,i.positionWS.y,-i.positionWS.z):i.sourcePosition;
    position=position*_UvScale.xyz+_UvOffset.xyz;position.y=-position.y;
    float3 weights=i.weights;
    half4 base=SourceTexture(TEXTURE2D_ARGS(_BaseMap,sampler_BaseMap),i.uv,position,weights)*_Tint;
    if(_VertexColor>.5){float4 vertex=i.color;if(_VertexColorSrgb>.5)vertex.rgb=SRGBToLinear(vertex.rgb);base*=vertex;}
    clip(base.a-_Cutoff);
    if(_NormalStrength>0)
    {
     if(_Triplanar>.5)
     {
      float3 tn=SourceTexture(TEXTURE2D_ARGS(_NormalMap,sampler_NormalMap),i.uv,position,weights).rgb;
      tn.xy=tn.xy*2-1;tn.z=sqrt(max(0,1-dot(tn.xy,tn.xy)));
      n=normalize(lerp(n,normalize(i.triTangent)*tn.x+normalize(i.triBinormal)*tn.y+n*tn.z,_NormalStrength));
     }
     else
     {
     float2 uv=SourceUv(i.uv);float3 dp1=ddx(i.positionWS),dp2=ddy(i.positionWS);float2 duv1=ddx(uv),duv2=ddy(uv);
     float3 q1=cross(dp2,n),q2=cross(n,dp1);float3 t=q1*duv1.x+q2*duv2.x,b=q1*duv1.y+q2*duv2.y;
     float inv=rsqrt(max(max(dot(t,t),dot(b,b)),1e-12));float3 tn=SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,uv).xyz*2-1;
     tn.z=sqrt(max(0,1-dot(tn.xy,tn.xy)));tn.y=-tn.y;
     n=normalize(lerp(n,tn.x*t*inv+tn.y*b*inv+tn.z*n,_NormalStrength));
     }
    }
    half3 emission=_EmissionColor.rgb;
    if(_EmissionHasMap>.5){half3 tex=SourceTexture(TEXTURE2D_ARGS(_EmissionMap,sampler_EmissionMap),i.uv,position,weights).rgb;emission=_EmissionOperator==0?emission+tex:emission*tex;}
    emission*=_EmissionEnergy;
    if(_Unshaded>.5)return half4(MixFog(base.rgb+emission,i.fog),base.a);
    SurfaceData s=(SurfaceData)0;s.albedo=base.rgb;s.alpha=base.a;
    s.metallic=saturate(_Metallic*Channel(SourceTexture(TEXTURE2D_ARGS(_MetallicMap,sampler_MetallicMap),i.uv,position,weights),_MetallicChannel));
    s.smoothness=1-saturate(_Roughness*Channel(SourceTexture(TEXTURE2D_ARGS(_RoughnessMap,sampler_RoughnessMap),i.uv,position,weights),_RoughnessChannel));
    s.occlusion=Channel(SourceTexture(TEXTURE2D_ARGS(_OcclusionMap,sampler_OcclusionMap),i.uv,position,weights),_OcclusionChannel);s.emission=emission;s.normalTS=half3(0,0,1);
    InputData input=(InputData)0;input.positionWS=i.positionWS;input.normalWS=n;input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    input.shadowCoord=ComputeScreenPos(i.positionCS);
    #else
    input.shadowCoord=TransformWorldToShadowCoord(i.positionWS);
    #endif
    input.fogCoord=i.fog;input.bakedGI=SampleSH(n);input.vertexLighting=VertexLighting(i.positionWS,n);input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);input.shadowMask=half4(1,1,1,1);
    half4 output=UniversalFragmentPBR(input,s);output.rgb=MixFog(output.rgb,i.fog);return output;
   }
   ENDHLSL
  }
  // Current source visible standard surfaces are opaque. Alpha-depth sparks
  // are hidden by the frozen builder; their full transparent shadow policy is
  // not claimed. This solid pass gives source architecture actual sun shadows.
  Pass
  {
   Name "ShadowCaster"
   Tags{"LightMode"="ShadowCaster"}
   ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex ShadowVert
   #pragma fragment ShadowFrag
   #pragma multi_compile_instancing
   #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
   float3 _LightDirection,_LightPosition;
   struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
   float4 ShadowVert(A a):SV_POSITION
   {
    UNITY_SETUP_INSTANCE_ID(a);
    float3 p=TransformObjectToWorld(a.positionOS.xyz),n=TransformObjectToWorldNormal(a.normalOS);
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 light=normalize(_LightPosition-p);
    #else
    float3 light=_LightDirection;
    #endif
    float4 clip=TransformWorldToHClip(ApplyShadowBias(p,n,light));
    #if UNITY_REVERSED_Z
    clip.z=min(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
    #else
    clip.z=max(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
    #endif
    return clip;
   }
   half4 ShadowFrag():SV_Target{return 0;}
   ENDHLSL
  }
 }
}
